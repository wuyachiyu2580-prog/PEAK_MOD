using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using BepInEx.Bootstrap;

namespace WhySoLaggy
{
    internal enum CapturePhase { Standby, Plugins, Targeted, Manual, Saving, Cooldown, Paused, Fault }
    // Charge shared event references again in each retained record. The remaining
    // 32 MiB of the total payload budget covers IO, RPC, error and watched queues.
    internal static class CapturePayloadBudget
    {
        internal const long Limit=32L*1024*1024;
        private static long bytes;
        internal static long Bytes=>System.Threading.Interlocked.Read(ref bytes);
        internal static bool Reserve(long size)
        {long old;do{old=Bytes;if(size<0||old+size>Limit)return false;}while(System.Threading.Interlocked.CompareExchange(ref bytes,old+size,old)!=old);return true;}
        internal static void Release(long size)=>System.Threading.Interlocked.Add(ref bytes,-size);
    }
    internal sealed class TriggerPolicy
    {
        private readonly Queue<double> spikes=new Queue<double>();
        private readonly Queue<double> starts=new Queue<double>();
        internal double CooldownUntil;
        internal bool Observe(double now,double ms,double median,int threshold,bool eligible)
        {
            while(spikes.Count>0&&now-spikes.Peek()>2)spikes.Dequeue();
            while(starts.Count>0&&now-starts.Peek()>=3600)starts.Dequeue();
            if(!eligible){spikes.Clear();return false;}
            if(ms>threshold&&ms>=median*3)spikes.Enqueue(now);
            return now>=CooldownUntil&&starts.Count<10&&median>0&&(ms>=100&&ms>=median*5||spikes.Count>=3);
        }
        internal void Started(double now){starts.Enqueue(now);spikes.Clear();}
        internal void Ended(double now){CooldownUntil=now+120;spikes.Clear();}
    }
    internal static class CaptureController
    {
        private static readonly object gate=new object();
        // Begin keeps only one saved record. At most rolling + active + saved
        // + an older in-flight export are charged: 4 + 8 + 8 + 8 MiB.
        private static readonly EventRing rolling=new EventRing(4*1024*1024);
        private static readonly CaptureTimingWindow timing=new CaptureTimingWindow();
        private static EventRing recording;
        private static readonly Queue<CaptureRecord> saved=new Queue<CaptureRecord>();
        private static readonly TriggerPolicy trigger=new TriggerPolicy();
        private static readonly Dictionary<string,Tuple<double,int,int>> candidates=new Dictionary<string,Tuple<double,int,int>>();
        private static double started,ends,pluginStarted,budgetWindow,budgetSum;
        private static int budgetFrames,overBudgetSeconds,epoch,observationFrames;
        private static bool manual,targeted,degraded;
        internal static bool AutoEnabled=true,Paused;
        internal static bool Busy=>Phase==CapturePhase.Plugins||Phase==CapturePhase.Targeted||Phase==CapturePhase.Manual;
        internal static bool WantsPlugin=>Busy&&!degraded;
        internal static HashSet<string> TargetOwners;
        internal static MethodBase[] ExceptionTargets=new MethodBase[0];
        internal static CapturePhase Phase=CapturePhase.Standby;
        internal static string Detail="",ExportStatus="",DirectoryPath;
        internal static double SecondsLeft=>Busy?Math.Max(0,ends-DiagnosticClock.Now):0;
        internal static Action<bool,HashSet<string>,MethodBase[]> ApplyHooks;
        private static string[] manifest=new string[0];
        private static Dictionary<string,object> config=new Dictionary<string,object>();
        private static int exports;
        internal static void Initialize(string dir,Dictionary<string,object> configuration,string[] loadedPlugins=null)
        {
            DirectoryPath=Path.Combine(dir,"WhySoLaggy","diagnostics");config=configuration;
            manifest=loadedPlugins??ReadManifest();
            StructuredLogger.EventWritten+=OnEvent;
        }
        internal static void UpdateConfiguration(Dictionary<string,object> values){config=values;}
        private static string[] ReadManifest()=>Chainloader.PluginInfos.Select(x=>x.Key+" | "+x.Value.Metadata.Name+" | "+x.Value.Metadata.Version+" | "+x.Value.Instance?.GetType().Assembly.ManifestModule.ModuleVersionId).ToArray();
        private static void OnEvent(StructuredEvent e)
        {
            lock(gate){rolling.Add(e,DiagnosticClock.Now,20);recording?.Add(e,DiagnosticClock.Now,90);}
        }
        internal static void OnFrame(FrameRecord frame)
        {
            double now=frame.Time;
            frame.SelfMs+=SelfMetrics.TakeFrame();
            FpsTracker.Observe(frame);
            StructuredLogger.WriteEvent(new StructuredEvent{Timestamp=StructuredLogger.NowStamp(),FrameNumber=frame.Frame,Type=EventType.FrameSample,
                Fields=new Dictionary<string,object>{{"AvgFrameMs",frame.DurationMs},{"CoveredMs",frame.CoveredMs},{"UnattributedMs",Math.Max(0,frame.DurationMs-frame.CoveredMs)},
                    {"SelfMainMs",frame.SelfMs},{"WriterDropped",DiagnosticIO.Dropped},{"DiskDroppedBatches",StructuredLogger.DiskDropped},{"WriterQueueBytes",DiagnosticIO.PendingBytes},{"CapturePayloadBytes",CapturePayloadBudget.Bytes},{"MethodRecordOmitted",0},{"SceneState",frame.State},{"Scene",frame.Scene},{"Validity",frame.Complete?"Complete":"Incomplete"}}});
            if(Busy)
            {
                if(!PluginProfiler.Session.Busy)
                {
                    if(observationFrames==0)pluginStarted=now;observationFrames++;
                    foreach(var group in frame.Methods.Where(x=>x.Scope=="PluginProfiler"&&!string.IsNullOrEmpty(x.PluginGuid)).GroupBy(x=>x.PluginGuid))
                    {double ms=group.Sum(x=>x.Ms);candidates.TryGetValue(group.Key,out var c);candidates[group.Key]=Tuple.Create((c?.Item1??0)+ms,observationFrames,(c?.Item3??0)+(frame.DurationMs>FpsTracker.SpikeThresholdMs&&ms>=2?1:0));}
                }
                timing.Observe(frame,StructuredLogger.WriteEvent);
                budgetSum+=frame.SelfMs;budgetFrames++;
                if(now-budgetWindow>=1){overBudgetSeconds=budgetSum/Math.Max(1,budgetFrames)>1?overBudgetSeconds+1:0;budgetWindow=now;budgetSum=0;budgetFrames=0;}
                if(!manual&&overBudgetSeconds>=3&&targeted){targeted=false;TargetOwners=null;ExceptionTargets=new MethodBase[0];Detail="Automatic targets removed: self-overhead budget exceeded";Phase=CapturePhase.Plugins;ApplyHooks?.Invoke(true,null,ExceptionTargets);}
                if(frame.Epoch!=epoch){Finish("Session changed");return;}
                if(!manual&&!degraded&&(PatchProfiler.Session.Faulted||PluginProfiler.Session.Faulted))
                {degraded=true;targeted=false;TargetOwners=null;ExceptionTargets=new MethodBase[0];Phase=CapturePhase.Plugins;Detail="Automatic expansion stopped after a hook failure or long installation";ApplyHooks?.Invoke(false,null,ExceptionTargets);}
                if(!manual&&!degraded&&!targeted&&overBudgetSeconds<3&&observationFrames>0&&now-pluginStarted>=5&&!PluginProfiler.Session.Busy)
                {
                    TargetOwners=new HashSet<string>(candidates.Where(x=>x.Value.Item1/Math.Max(1,observationFrames)>=.5||x.Value.Item3>=2).OrderByDescending(x=>x.Value.Item1).Take(3).Select(x=>x.Key));
                    ExceptionTargets=ExceptionObserver.Candidates();targeted=true;
                    if(TargetOwners.Count>0||ExceptionTargets.Length>0){Phase=CapturePhase.Targeted;ApplyHooks?.Invoke(true,TargetOwners,ExceptionTargets);}
                    else Detail="No verified target; retaining plugin, network and error evidence";
                }
                if(now>=ends){Finish("Completed");return;}
            }
            if(Paused){if(!Busy)Phase=CapturePhase.Paused;return;}
            bool eligible=AutoEnabled&&frame.Complete&&(frame.State=="Gameplay"||frame.State=="Airport")&&now-FrameCoordinator.StableSince>=5&&FpsTracker.NormalStatistics.Count>=100;
            if(trigger.Observe(now,frame.DurationMs,FpsTracker.NormalStatistics.Median,FpsTracker.SpikeThresholdMs,eligible))
            {if(!Busy)Begin(false,"Frame spike");else if(!manual)ends=Math.Min(started+60,now+30);}
            if(!Busy)Phase=now<trigger.CooldownUntil?CapturePhase.Cooldown:CapturePhase.Standby;
        }
        internal static void Begin(bool isManual,string reason)
        {
            if(Busy)return;
            started=pluginStarted=budgetWindow=DiagnosticClock.Now;ends=started+30;manual=isManual;targeted=degraded=false;epoch=DiagnosticContext.RoomEpoch;
            overBudgetSeconds=budgetFrames=observationFrames=0;budgetSum=0;candidates.Clear();TargetOwners=null;ExceptionTargets=new MethodBase[0];
            DiagnosticContext.CaptureId=Guid.NewGuid().ToString("N");timing.Reset(started);
            lock(gate){while(saved.Count>1)saved.Dequeue().Release();recording=new EventRing(8*1024*1024);rolling.CopyTo(recording,started,90);}
            Phase=manual?CapturePhase.Manual:CapturePhase.Plugins;Detail=reason+"; pre-trigger methods may not have been instrumented";
            if(!manual)trigger.Started(started);
            EmitState();ApplyHooks?.Invoke(true,null,ExceptionTargets);
        }
        internal static void Mark(){StructuredLogger.WriteEvent(new StructuredEvent{Timestamp=StructuredLogger.NowStamp(),FrameNumber=DiagnosticContext.Frame,Type=EventType.Marker,Fields=new Dictionary<string,object>{{"Reason","User marker"}}});}
        internal static void Finish(string reason)
        {
            if(!Busy)return;timing.Flush(StructuredLogger.WriteEvent);Phase=CapturePhase.Saving;Detail=reason;EmitState();CaptureRecord record;
            lock(gate)
            {
                record=recording.Detach();record.Id=DiagnosticContext.CaptureId;record.Reason=reason;record.Started=started;record.Ended=DiagnosticClock.Now;
                recording=null;while(saved.Count>=2)saved.Dequeue().Release();saved.Enqueue(record);
            }
            TargetOwners=null;ExceptionTargets=new MethodBase[0];ApplyHooks?.Invoke(false,null,ExceptionTargets);
            DiagnosticContext.CaptureId="";trigger.Ended(DiagnosticClock.Now);Phase=CapturePhase.Cooldown;
            Export(record,false,false);
        }
        private static void EmitState()=>StructuredLogger.WriteEvent(new StructuredEvent{Timestamp=StructuredLogger.NowStamp(),FrameNumber=DiagnosticContext.Frame,Type=EventType.CaptureState,
            Fields=new Dictionary<string,object>{{"Phase",Phase.ToString()},{"Reason",Detail},{"PluginTimingMode","Exact"},{"PatchTimingMode",PatchProfiler.Adaptive?"Adaptive":"Exact"}}});
        internal static void ExportLatest(bool complete,bool raw)
        {
            CaptureRecord record;bool temporary=false;
            lock(gate)
            {
                if(saved.Count>0)record=saved.Last();
                else{temporary=true;var copy=new EventRing(8*1024*1024);rolling.CopyTo(copy,DiagnosticClock.Now,20);record=copy.Detach();record.Id=Guid.NewGuid().ToString("N");record.Reason="Manual rolling export";record.Started=DiagnosticClock.Now-20;record.Ended=DiagnosticClock.Now;}
                Export(record,complete,raw);if(temporary)record.Release();
            }
        }

        private static void Export(CaptureRecord record,bool complete,bool raw)
        {
            if(System.Threading.Interlocked.CompareExchange(ref exports,1,0)!=0){ExportStatus="Export already running";return;}
            record.Retain();
            string patchMap=HarmonyScanner.LatestCsv;string[] plugins=manifest.ToArray();var settings=new Dictionary<string,object>(config);
            ExportStatus="Exporting";string durationLabel=UiText.Get("Duration","持续时间"),recordsLabel=UiText.Get("Records","记录条数"),evictedLabel=UiText.Get("Evicted","省略条数"),privacyLabel=UiText.Get("Privacy","隐私模式"),privacyValue=UiText.Get(complete?"Complete identities":"Anonymous",complete?"完整身份":"匿名"),reasonText=UiText.Runtime(record.Reason);string summaryText=UiText.Get("Coverage is partial; associated owners are not blame attribution.\nPre-trigger method timing exists only when already instrumented.","采集覆盖范围有限，关联插件不能直接认定为根因。\n触发前只有已经安装计时的方法才有耗时数据。");
            string retainedLabel=UiText.Get("Retained event range (monotonic seconds)","保留事件时间范围（单调秒）"),frameRangeLabel=UiText.Get("Retained frame range (monotonic seconds)","保留帧时间范围（单调秒）"),framesLabel=UiText.Get("Retained frames","保留帧数"),dropsLabel=UiText.Get("Dropped frames / critical events / details","丢弃帧／关键事件／明细"),unavailable=UiText.Get("unavailable","不可用");
            string timingText=UiText.Get("Method timings are window totals; spike details are subsets and must not be added again. Sampled time is observed time, not extrapolated.","方法耗时按窗口汇总；尖峰明细属于窗口汇总的一部分，不可重复相加。采样耗时未外推。");
            if(!DiagnosticIO.Enqueue(()=>
            {
                string tmp=null;
                try
                {
                    Directory.CreateDirectory(DirectoryPath);string path=Path.Combine(DirectoryPath,"wsl-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")+"-"+record.Id.Substring(0,8)+(complete?"-full":"-anonymous")+".zip");
                    if(File.Exists(path))path=Path.Combine(DirectoryPath,"wsl-"+Guid.NewGuid().ToString("N")+".zip");
                    tmp=path+".partial";var anonymizer=new DiagnosticAnonymizer();
                    using(var file=File.Create(tmp))using(var zip=new ZipArchive(file,ZipArchiveMode.Create))
                    {
                        double? retainedStart=record.RetainedStart,retainedEnd=record.RetainedEnd;
                        double? frameStart=record.PackedFrames.Length==0?(double?)null:record.PackedFrames[0].Time,frameEnd=record.PackedFrames.Length==0?(double?)null:record.PackedFrames[record.PackedFrames.Length-1].Time;
                        string retainedRange=retainedStart.HasValue?retainedStart.Value.ToString("F3")+" - "+retainedEnd.Value.ToString("F3"):unavailable;
                        string frameRange=frameStart.HasValue?frameStart.Value.ToString("F3")+" - "+frameEnd.Value.ToString("F3"):unavailable;
                        Put(zip,"summary.txt","WhySoLaggy "+DiagnosticContext.BuildId+"\n"+reasonText+"\n"+durationLabel+": "+(record.Ended-record.Started).ToString("F2")+"s\n"+recordsLabel+": "+record.Count+"\n"+evictedLabel+": "+record.Evicted+"\n"+retainedLabel+": "+retainedRange+"\n"+frameRangeLabel+": "+frameRange+"\n"+framesLabel+": "+record.PackedFrames.Length+"\n"+dropsLabel+": "+record.DroppedFrames+" / "+record.DroppedCritical+" / "+record.DroppedDetails+"\n"+timingText+"\n"+summaryText+"\n"+privacyLabel+": "+privacyValue);
                        Put(zip,"manifest.json",EventCodec.Json(new Dictionary<string,object>{{"SchemaVersion",2},{"Build",DiagnosticContext.BuildId},{"SessionId",DiagnosticContext.SessionId},{"CaptureId",record.Id},{"Plugins",plugins},{"Config",settings},
                            {"RequestedStartSeconds",record.Started},{"RequestedEndSeconds",record.Ended},{"RetainedStartSeconds",retainedStart},{"RetainedEndSeconds",retainedEnd},
                            {"RetainedFrameStartSeconds",frameStart},{"RetainedFrameEndSeconds",frameEnd},
                            {"RetainedFrameCount",record.PackedFrames.Length},{"DroppedFrames",record.DroppedFrames},{"DroppedCriticalEvents",record.DroppedCritical},{"DroppedDetailEvents",record.DroppedDetails},
                            {"TimingAggregation","window inclusive; spike records are subsets, never add them to window totals; sampled time is observed time, not extrapolated"},
                            {"Omitted","Raw parameter text, field probes and unstructured messages omitted in anonymous exports"}}));
                        var entry=zip.CreateEntry("events.jsonl",CompressionLevel.Fastest);
                        using(var writer=new StreamWriter(entry.Open(),new UTF8Encoding(false)))foreach(var e in record.Enumerate())writer.WriteLine(EventCodec.EventJson(complete?e:anonymizer.Sanitize(e)));
                        foreach(var kind in new[]{Tuple.Create("frames.csv",new[]{EventType.FrameSample,EventType.SpikeFrame}),Tuple.Create("calls.csv",new[]{EventType.PluginTiming,EventType.PatchTiming}),Tuple.Create("network.csv",new[]{EventType.NetworkSample,EventType.RpcCall}),Tuple.Create("exceptions.csv",new[]{EventType.ExceptionSummary})})
                        {
                            using(var writer=new StreamWriter(zip.CreateEntry(kind.Item1,CompressionLevel.Fastest).Open(),new UTF8Encoding(false)))
                            {writer.WriteLine("Frame,Timestamp,Event,DataJson");foreach(var e in record.Enumerate().Where(x=>kind.Item2.Contains(x.Type))){var safe=complete?e:anonymizer.Sanitize(e);writer.WriteLine(e.FrameNumber+","+Csv(e.Timestamp)+","+e.Type+","+Csv(EventCodec.Json(safe.Fields)));}}
                        }
                        Put(zip,"harmony_patches.csv",patchMap);
                        if(raw&&complete)
                        {
                            string root=Path.GetFullPath(Path.Combine(DirectoryPath,"..",".."));
                            foreach(string name in new[]{"WhySoLaggy.log","WhySoLaggy_Abuse.log","LogOutput.log"})
                            {string source=Path.Combine(root,name);if(File.Exists(source))CopyTail(zip,source,"raw/"+name);}
                            string playerLog=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"AppData","LocalLow","LandCrab","PEAK","Player.log");
                            if(File.Exists(playerLog))CopyTail(zip,playerLog,"raw/Player.log");
                        }
                    }
                    File.Move(tmp,path);ExportStatus=path;
                    var files=new DirectoryInfo(DirectoryPath).GetFiles("wsl-*.zip").OrderByDescending(x=>x.LastWriteTimeUtc).ToArray();long total=0;
                    for(int i=0;i<files.Length;i++){total+=files[i].Length;if(i>=10||total>500L*1024*1024)files[i].Delete();}
                }
                catch(Exception ex){ExportStatus="Export failed: "+ex.GetType().Name;}
                finally{if(tmp!=null&&File.Exists(tmp))try{File.Delete(tmp);}catch{}record.Release();System.Threading.Interlocked.Exchange(ref exports,0);}
            },1024+patchMap.Length*2+plugins.Sum(x=>x.Length*2),true)){record.Release();System.Threading.Interlocked.Exchange(ref exports,0);ExportStatus="Writer queue full";}
        }
        private static string Csv(string s)=>"\""+(s??"").Replace("\"","\"\"")+"\"";
        private static void Put(ZipArchive zip,string name,string value){using(var writer=new StreamWriter(zip.CreateEntry(name,CompressionLevel.Fastest).Open(),new UTF8Encoding(false)))writer.Write(value);}
        private static void CopyTail(ZipArchive zip,string path,string name)
        {
            using(var source=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite))using(var dest=zip.CreateEntry(name,CompressionLevel.Fastest).Open())
            {source.Position=Math.Max(0,source.Length-2*1024*1024);source.CopyTo(dest);}
        }
        internal static void Shutdown(){if(Busy)Finish("Shutdown");StructuredLogger.EventWritten-=OnEvent;lock(gate){rolling.Clear();recording?.Clear();recording=null;while(saved.Count>0)saved.Dequeue().Release();timing.Reset(0);}}
    }

    internal sealed class DiagnosticAnonymizer
    {
        private readonly Dictionary<string,string> aliases=new Dictionary<string,string>();
        private string Alias(string kind,string key)
        {key=kind+":"+key;if(!aliases.TryGetValue(key,out string alias))aliases[key]=alias=kind+"-"+(aliases.Count+1);return alias;}
        private static readonly HashSet<string> Symbols=new HashSet<string>{"MethodId","Owner","PluginGuid","RpcMethod","ExceptionKind","TargetMethod"};
        private static readonly HashSet<string> SafeStrings=new HashSet<string>{"SessionId","CaptureId","Validity","SceneState","Phase","GcAllocationStatus","CpuGpuStatus","TimingKind","PluginTimingMode","PatchTimingMode"};
        internal static string SafeStack(string source)
        {
            return string.Join("\n",(source??"").Split(new[]{'\r','\n'},StringSplitOptions.RemoveEmptyEntries).Take(6).Select(line=>
            {var m=System.Text.RegularExpressions.Regex.Match(line,@"^\s*(?:at )?([A-Za-z_][A-Za-z0-9_.+<>]*)\s*\(");return m.Success?m.Groups[1].Value+"()":"[unresolved frame]";}));
        }
        internal StructuredEvent Sanitize(StructuredEvent source)
        {
            var input=source.Fields??new Dictionary<string,object>();var fields=new Dictionary<string,object>();
            input.TryGetValue("RoomEpoch",out var epoch);string prefix=Convert.ToString(epoch)+":";
            foreach(var p in input)
            {
                string key=p.Key;
                if(key=="RoomName"){fields[key]=Alias("Room",prefix);continue;}
                if(key=="SenderName"||key=="TopActorName"||key=="SuspectedRequesterName"||key=="TargetName")
                {string actorKey=key=="SenderName"?"SenderActor":key=="TopActorName"?"TopActor":key=="TargetName"?"TargetActor":"SuspectedRequesterActor";fields[key]=input.TryGetValue(actorKey,out var actor)?Alias("Player",prefix+Convert.ToString(actor)):"[identity omitted]";continue;}
                if(key=="Snapshot"||key=="ArgsSummary"||key=="Message"||key=="SpecificDesc"||key=="TraceCaller"||key.IndexOf("Path",StringComparison.OrdinalIgnoreCase)>=0)continue;
                if(key=="TraceStack"){fields[key]=SafeStack(p.Value as string);continue;}
                if(p.Value==null||p.Value.GetType().IsPrimitive||p.Value is decimal){fields[key]=p.Value;continue;}
                if(p.Value is string value)
                {
                    bool symbol=Symbols.Contains(key)||(key=="Name"&&(source.Type==EventType.PluginTiming||source.Type==EventType.PatchTiming));
                    if(SafeStrings.Contains(key)||symbol&&!value.Contains("\\")&&!value.Contains("/"))fields[key]=EventCodec.Limit(value,512);
                }
            }
            return new StructuredEvent{Timestamp=source.Timestamp,FrameNumber=source.FrameNumber,Type=source.Type,Fields=fields};
        }
    }
}
