# -*- coding: utf-8 -*-
from pathlib import Path
r=Path(__file__).resolve().parents[2]/'MOD开发/WhySoLaggy/WhySoLaggy'
p=r/'Helpers/CaptureController.cs';s=p.read_text(encoding='utf-8')
a=s.index('    internal sealed class EventRing');b=s.index('    internal sealed class TriggerPolicy',a)
s=s[:a]+'''    // Charge shared event references again in each retained record. The remaining
    // 24 MiB of the total payload budget covers IO, RPC, error and watched queues.
    internal static class CapturePayloadBudget
    {
        internal const long Limit=40L*1024*1024;
        private static long bytes;
        internal static long Bytes=>System.Threading.Interlocked.Read(ref bytes);
        internal static bool Reserve(long size)
        {long old;do{old=Bytes;if(size<0||old+size>Limit)return false;}while(System.Threading.Interlocked.CompareExchange(ref bytes,old+size,old)!=old);return true;}
        internal static void Release(long size)=>System.Threading.Interlocked.Add(ref bytes,-size);
    }
    internal sealed class EventRing
    {
        private readonly Queue<Tuple<double,StructuredEvent,int>> items=new Queue<Tuple<double,StructuredEvent,int>>();
        internal long Bytes{get;private set;}
        internal long Evicted{get;private set;}
        internal readonly int Limit;
        internal EventRing(int bytes){Limit=bytes;}
        private void Evict(){Bytes-=items.Peek().Item3;CapturePayloadBudget.Release(items.Dequeue().Item3);Evicted++;}
        internal void Add(StructuredEvent e,double time,double window)
        {
            int size=EventCodec.Size(e);if(size>Limit){Evicted++;return;}
            while(items.Count>0&&(Bytes+size>Limit||items.Peek().Item1<time-window))Evict();
            while(!CapturePayloadBudget.Reserve(size)){if(items.Count==0){Evicted++;return;}Evict();}
            items.Enqueue(Tuple.Create(time,e,size));Bytes+=size;
        }
        internal StructuredEvent[] Snapshot()=>items.Select(x=>x.Item2).ToArray();
        internal CaptureRecord Detach()
        {var record=new CaptureRecord{Events=Snapshot(),Bytes=Bytes,Evicted=Evicted};items.Clear();Bytes=0;Evicted=0;return record;}
        internal void Clear(){CapturePayloadBudget.Release(Bytes);items.Clear();Bytes=0;Evicted=0;}
    }
    internal sealed class CaptureRecord
    {
        internal string Id,Reason;
        internal double Started,Ended;
        internal StructuredEvent[] Events;
        internal long Evicted,Bytes;
        private int references=1;
        internal void Retain()=>System.Threading.Interlocked.Increment(ref references);
        internal void Release(){if(System.Threading.Interlocked.Decrement(ref references)==0){CapturePayloadBudget.Release(Bytes);Events=new StructuredEvent[0];}}
    }
'''+s[b:]
s=s.replace('lock(gate){recording=new EventRing(16*1024*1024);foreach(var e in rolling.Snapshot())recording.Add(e,started,90);}','lock(gate){while(saved.Count>1)saved.Dequeue().Release();recording=new EventRing(16*1024*1024);foreach(var e in rolling.Snapshot())recording.Add(e,started,90);}')
s=s.replace('record=new CaptureRecord{Id=DiagnosticContext.CaptureId,Reason=reason,Started=started,Ended=DiagnosticClock.Now,Events=recording.Snapshot(),Evicted=recording.Evicted};\n                recording=null;while(saved.Count>=2)saved.Dequeue();saved.Enqueue(record);','record=recording.Detach();record.Id=DiagnosticContext.CaptureId;record.Reason=reason;record.Started=started;record.Ended=DiagnosticClock.Now;\n                recording=null;while(saved.Count>=2)saved.Dequeue().Release();saved.Enqueue(record);')
a=s.index('            CaptureRecord record;\n            lock(gate)record=saved.Count');b=s.index('\n        private static void Export(',a)
s=s[:a]+'''            CaptureRecord record;bool temporary=false;
            lock(gate)
            {
                if(saved.Count>0)record=saved.Last();
                else{temporary=true;var copy=new EventRing(16*1024*1024);foreach(var e in rolling.Snapshot())copy.Add(e,DiagnosticClock.Now,20);record=copy.Detach();record.Id=Guid.NewGuid().ToString("N");record.Reason="Manual rolling export";record.Started=DiagnosticClock.Now-20;record.Ended=DiagnosticClock.Now;}
                Export(record,complete,raw);if(temporary)record.Release();
            }
        }
'''+s[b:]
s=s.replace('string patchMap=HarmonyScanner.LatestCsv;','record.Retain();\n            string patchMap=HarmonyScanner.LatestCsv;')
s=s.replace('ExportStatus="Exporting";','ExportStatus="Exporting";string summaryText=UiText.Get("Coverage is partial; associated owners are not blame attribution.\\nPre-trigger method timing exists only when already instrumented.","采集覆盖范围有限，关联插件不能直接认定为根因。\\n触发前只有已经安装计时的方法才有耗时数据。");')
s=s.replace('''                try
                {
                    Directory.CreateDirectory''','''                string tmp=null;
                try
                {
                    Directory.CreateDirectory''')
s=s.replace('string tmp=path+".partial";','tmp=path+".partial";')
s=s.replace('\\nCoverage is partial; associated owners are not blame attribution.\\nPre-trigger method timing exists only when already instrumented.\\nPrivacy: ','\\n"+summaryText+"\\nPrivacy: ')
s=s.replace('                        Put(zip,"harmony_patches.csv",patchMap);','''                        foreach(var kind in new[]{Tuple.Create("frames.csv",new[]{EventType.FrameSample,EventType.SpikeFrame}),Tuple.Create("calls.csv",new[]{EventType.PluginTiming,EventType.PatchTiming}),Tuple.Create("network.csv",new[]{EventType.NetworkSample,EventType.RpcCall}),Tuple.Create("exceptions.csv",new[]{EventType.ExceptionSummary})})
                        {
                            using(var writer=new StreamWriter(zip.CreateEntry(kind.Item1,CompressionLevel.Fastest).Open(),new UTF8Encoding(false)))
                            {writer.WriteLine("Frame,Timestamp,Event,DataJson");foreach(var e in record.Events.Where(x=>kind.Item2.Contains(x.Type))){var safe=complete?e:anonymizer.Sanitize(e);writer.WriteLine(e.FrameNumber+","+Csv(e.Timestamp)+","+e.Type+","+Csv(EventCodec.Json(safe.Fields)));}}
                        }
                        Put(zip,"harmony_patches.csv",patchMap);''')
s=s.replace('finally{System.Threading.Interlocked.Exchange(ref exports,0);}','finally{if(tmp!=null&&File.Exists(tmp))try{File.Delete(tmp);}catch{}record.Release();System.Threading.Interlocked.Exchange(ref exports,0);}')
s=s.replace('},1024,true)){System.Threading.Interlocked.Exchange(ref exports,0);','},1024,true)){record.Release();System.Threading.Interlocked.Exchange(ref exports,0);')
s=s.replace('        private static void Put(','''        private static string Csv(string s)=>"\\\""+(s??"").Replace("\\\"","\\\"\\\"")+"\\\"";
        private static void Put(''')
s=s.replace('rolling.Clear();recording=null;saved.Clear();','rolling.Clear();recording?.Clear();recording=null;while(saved.Count>0)saved.Dequeue().Release();')
a=s.index('    internal sealed class DiagnosticAnonymizer');s=s[:a]+'''    internal sealed class DiagnosticAnonymizer
    {
        private readonly Dictionary<string,string> aliases=new Dictionary<string,string>();
        private string Alias(string kind,string key)
        {key=kind+":"+key;if(!aliases.TryGetValue(key,out string alias))aliases[key]=alias=kind+"-"+(aliases.Count+1);return alias;}
        private static readonly HashSet<string> Symbols=new HashSet<string>{"MethodId","Owner","PluginGuid","RpcMethod","ExceptionKind","TargetMethod"};
        private static readonly HashSet<string> SafeStrings=new HashSet<string>{"SessionId","CaptureId","Validity","SceneState","Phase","GcAllocationStatus","CpuGpuStatus","TimingKind"};
        internal static string SafeStack(string source)
        {
            return string.Join("\\n",(source??"").Split(new[]{'\\r','\\n'},StringSplitOptions.RemoveEmptyEntries).Take(6).Select(line=>
            {var m=System.Text.RegularExpressions.Regex.Match(line,@"^\\s*(?:at )?([A-Za-z_][A-Za-z0-9_.+<>]*)\\s*\\(");return m.Success?m.Groups[1].Value+"()":"[unresolved frame]";}));
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
                    if(SafeStrings.Contains(key)||symbol&&!value.Contains("\\\\")&&!value.Contains("/"))fields[key]=EventCodec.Limit(value,512);
                }
            }
            return new StructuredEvent{Timestamp=source.Timestamp,FrameNumber=source.FrameNumber,Type=source.Type,Fields=fields};
        }
    }
}
'''
p.write_text(s,encoding='utf-8')
print('Capture memory reservations and anonymous export implemented')
