from pathlib import Path
root=Path(__file__).resolve().parents[2]/'MOD开发/WhySoLaggy/WhySoLaggy'
files={
'Helpers/PatchProfiler.cs':r'''using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;

namespace WhySoLaggy
{
    internal static class PatchProfiler
    {
        public static volatile bool Enabled=true;
        public static int TopMethodCount=10;
        public static float MinReportMs=.1f;
        internal static bool Adaptive;
        public static string OwnHarmonyId;
        public static readonly HashSet<string> IgnoreMethods=new HashSet<string>(StringComparer.Ordinal);
        internal static readonly HookSession Session=new HookSession("PatchProfiler",typeof(PatchProfiler));
        internal static bool IsActive=>Enabled&&Session.Active;
        internal static bool HasPendingCleanup=>Session.Count!=0;
        internal static string TimingHarmonyId=>Session.Owner;
        public struct TimingState { internal InvocationTiming Invocation; }
        internal static List<HookSession.Target> Targets(HashSet<string> owners=null,int limit=int.MaxValue)
        {
            var map=new Dictionary<string,HookSession.Target>();
            foreach(var source in Harmony.GetAllPatchedMethods().ToArray())
            {
                if(!ProfilerTargetResolver.TryResolve(source,out var target,out _))continue;
                if(MethodKey.IsIgnored(source,IgnoreMethods)||MethodKey.IsIgnored(target,IgnoreMethods))continue;
                var info=Harmony.GetPatchInfo(source);if(info==null)continue;
                var external=info.Owners.Where(x=>x!=OwnHarmonyId && !(x??"").StartsWith((OwnHarmonyId??WhySoLaggyPlugin.PluginGuid)+".",StringComparison.Ordinal)).ToArray();
                if(external.Length==0 || (owners!=null&&!external.Any(owners.Contains)))continue;
                string id=ProfilerTargetResolver.Identity(target);
                if(!map.TryGetValue(id,out var value))map[id]=value=new HookSession.Target{Method=target,Owners=""};
                value.Owners=string.Join(", ",value.Owners.Split(new[]{", "},StringSplitOptions.RemoveEmptyEntries).Concat(external).Distinct().OrderBy(x=>x));
            }
            return map.Values.OrderBy(x=>ProfilerTargetResolver.Identity(x.Method),StringComparer.Ordinal).Take(limit).ToList();
        }
        public static void Initialize(Harmony harmony)
        {
            if(!Enabled||IsActive||harmony==null)return;
            OwnHarmonyId=OwnHarmonyId??harmony.Id;Session.Request(OwnHarmonyId,Targets(),false);Session.Pump(true);ApplySampling();
        }
        internal static void Request(bool enabled,HashSet<string> owners=null,bool automatic=false)
        {Enabled=enabled;Session.Request(OwnHarmonyId??WhySoLaggyPlugin.PluginGuid,enabled?Targets(owners,automatic?50:int.MaxValue):new List<HookSession.Target>(),automatic);}
        internal static void Pump(){Session.Pump();ApplySampling();}
        private static void ApplySampling(){foreach(var e in TimingLedger.All("PatchProfiler")){e.Adaptive=Adaptive&&!Session.Automatic;e.Threshold=MinReportMs;}}
        public static void TimingPrefix(MethodBase __originalMethod,out TimingState __state)
        {__state=default;try{if(Enabled)__state.Invocation=Session.Enter(__originalMethod);}catch{}}
        public static Exception TimingFinalizer(Exception __exception,ref TimingState __state)
        {try{TimingLedger.Exit(ref __state.Invocation,__exception);}catch{}return __exception;}
        internal sealed class TimingSample
        {public string Key,Owners;public int Calls,FrameCalls;public long FrameTicks;public double EstimatedTicks;public long Samples,Exceptions;}
        internal static List<TimingSample> TakeSnapshot(bool reset)
        {
            return TimingLedger.Snapshot("PatchProfiler",reset).Select(x=>new TimingSample{Key=x.Name,Owners=x.Owners,Calls=(int)x.Calls,Samples=x.Samples,Exceptions=x.Exceptions,
                EstimatedTicks=EstimateTotalTicks((long)(x.Ms*System.Diagnostics.Stopwatch.Frequency/1000), (int)x.Samples,(int)x.Calls)}).ToList();
        }
        internal static double EstimateTotalTicks(long ticks,int samples,int calls)=>samples>0?ticks/(double)samples*calls:0;
        public static void WriteReport(int frames){ProfilerReports.Write("PatchProfiler",frames,TopMethodCount);}
        public static void WriteSpikeDetail(){}
        public static void ResetFrameTimers(){} // FrameCoordinator owns the boundary.
        internal static bool Stop(){Enabled=false;return Session.Stop();}
        public static void Shutdown(){Stop();IgnoreMethods.Clear();}
    }
    internal static class ProfilerReports
    {
        internal static void Write(string scope,int frames,int count)
        {
            foreach(var r in TimingLedger.Snapshot(scope,true).OrderByDescending(x=>x.Ms).Take(count))
            {
                var fields=new Dictionary<string,object>{{"Name",r.Name},{"MethodId",r.Id},{"Owner",r.Owners},{"PluginGuid",r.PluginGuid},
                    {"AvgMs",r.Ms/Math.Max(1,frames)},{"TotalMs",r.Ms},{"CallCount",r.Calls},{"SampleCount",r.Samples},{"Exceptions",r.Exceptions},
                    {"BackgroundMs",r.BackgroundMs},{"SampleCoverage",r.Calls>0?r.Samples/(double)r.Calls:0},{"TimingKind","inclusive observed chain"}};
                StructuredLogger.WriteEvent(new StructuredEvent{Timestamp=StructuredLogger.NowStamp(),FrameNumber=DiagnosticContext.Frame,
                    Type=scope=="PluginProfiler"?EventType.PluginTiming:EventType.PatchTiming,Fields=fields});
                LagLogger.Write($"[WHY_LAG] {scope}: {r.Name} observed={r.Ms/Math.Max(1,frames):F3}ms/frame calls={r.Calls} sampled={r.Samples} errors={r.Exceptions}; associated owners=[{r.Owners}]");
            }
        }
    }
}''',
'Helpers/PluginProfiler.cs':r'''using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Bootstrap;
using HarmonyLib;

namespace WhySoLaggy
{
    internal static class PluginProfiler
    {
        public static bool Enabled=true;
        public static readonly HashSet<string> IgnoreGuids=new HashSet<string>(StringComparer.Ordinal);
        internal static readonly HookSession Session=new HookSession("PluginProfiler",typeof(PluginProfiler));
        internal static List<HookSession.Target> Targets()
        {
            var result=new List<HookSession.Target>();
            foreach(var pair in Chainloader.PluginInfos)
            {
                if(pair.Value.Instance==null||pair.Key==WhySoLaggyPlugin.PluginGuid||IgnoreGuids.Contains(pair.Key))continue;
                Type type=pair.Value.Instance.GetType();
                foreach(string name in new[]{"Update","LateUpdate","FixedUpdate"})
                {
                    MethodInfo method=null;
                    for(Type t=type;t!=null&&t!=typeof(UnityEngine.MonoBehaviour);t=t.BaseType)
                    {method=t.GetMethod(name,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly,null,Type.EmptyTypes,null);if(method!=null)break;}
                    if(method!=null)result.Add(new HookSession.Target{Method=method,Plugin=pair.Key,Owners=pair.Key});
                }
            }
            return result;
        }
        public static void Initialize(Harmony harmony){if(!Enabled||Session.Active)return;Session.Request(harmony.Id,Targets(),false);Session.Pump(true);}
        internal static void Request(bool enabled,bool automatic){Enabled=enabled;Session.Request(WhySoLaggyPlugin.PluginGuid,enabled?Targets():new List<HookSession.Target>(),automatic);}
        internal static void Pump()=>Session.Pump();
        public static void TimingPrefix(MethodBase __originalMethod,out InvocationTiming __state)
        {__state=default;try{if(Enabled)__state=Session.Enter(__originalMethod);}catch{}}
        public static Exception TimingFinalizer(Exception __exception,ref InvocationTiming __state)
        {try{TimingLedger.Exit(ref __state,__exception);}catch{}return __exception;}
        public static void WriteReport(int frames)=>ProfilerReports.Write("PluginProfiler",frames,30);
        public static void WriteSpikeDetail(float milliseconds){}
        public static void ResetFrameTimers(){}
        public static void Shutdown(){Enabled=false;Session.Stop();IgnoreGuids.Clear();}
    }
}''',
'Helpers/FpsTracker.cs':r'''using System;
using System.Collections.Generic;
namespace WhySoLaggy
{
    internal static class FpsTracker
    {
        public static int SpikeThresholdMs=50,ReportIntervalSeconds=10;
        public static bool EnableMemoryMonitor=true;
        public static bool IsSpikeFrame{get;private set;}
        public static float CurrentFrameMs{get;private set;}
        internal static FrameStatistics Statistics=new FrameStatistics(),NormalStatistics=new FrameStatistics();
        private static readonly FrameWindow all=new FrameWindow(),normal=new FrameWindow();
        private static double duration,sum,min=double.MaxValue,max,lastSnapshot,continuous,longest;
        private static int frames,spikes,runs;
        internal static void Observe(FrameRecord frame)
        {
            CurrentFrameMs=(float)frame.DurationMs;IsSpikeFrame=frame.DurationMs>SpikeThresholdMs;
            all.Add(frame.Time,frame.DurationMs);
            if(frame.Complete&&(frame.State=="Gameplay"||frame.State=="Airport")&&frame.Time-FrameCoordinator.StableSince>=5)normal.Add(frame.Time,frame.DurationMs);
            if(frame.Time-lastSnapshot>=.25){Statistics=all.Snapshot(frame.Time);NormalStatistics=normal.Snapshot(frame.Time);lastSnapshot=frame.Time;}
            duration+=frame.DurationMs/1000;sum+=frame.DurationMs;frames++;min=Math.Min(min,frame.DurationMs);max=Math.Max(max,frame.DurationMs);
            if(IsSpikeFrame){spikes++;if(continuous==0)runs++;continuous+=frame.DurationMs;longest=Math.Max(longest,continuous);}else continuous=0;
            if(IsSpikeFrame)StructuredLogger.WriteEvent(new StructuredEvent{Timestamp=StructuredLogger.NowStamp(),FrameNumber=frame.Frame,Type=EventType.SpikeFrame,
                Fields=new Dictionary<string,object>{{"AvgFrameMs",frame.DurationMs},{"CoveredMs",frame.CoveredMs},{"UnattributedMs",Math.Max(0,frame.DurationMs-frame.CoveredMs)},{"Validity",frame.Complete?"Complete":"Incomplete"},{"SceneState",frame.State}}});
            if(duration>=ReportIntervalSeconds){Report();duration=sum=max=longest=0;min=double.MaxValue;frames=spikes=runs=0;}
        }
        public static void Tick(){MemoryMetrics.Tick(DiagnosticClock.Now);}
        public static float GetWindowAvgMs()=>(float)Statistics.Mean;
        public static float GetAllocRateKBps()=>float.NaN;
        private static void Report()
        {
            var rpc=RpcMonitor.TakeRuntimeMetrics();var writer=StructuredLogger.TakeRuntimeMetrics();
            var fields=new Dictionary<string,object>{{"AvgFps",frames/Math.Max(.001,duration)},{"MinFps",1000/Math.Max(.001,max)},{"MaxFps",1000/Math.Max(.001,min)},
                {"AvgFrameMs",sum/Math.Max(1,frames)},{"SpikeThresholdMs",SpikeThresholdMs},{"SpikeCount",spikes},{"ReportDuration",duration},
                {"P95FrameMs",Statistics.P95},{"P99FrameMs",Statistics.P99},{"Low1Fps",Statistics.Low1},{"LongestStallMs",longest},{"StallRuns",runs},
                {"UnityAllocatedBytes",MemoryMetrics.UnityBytes},{"ManagedUsedBytes",MemoryMetrics.ManagedUsedBytes},{"ManagedCapacityBytes",MemoryMetrics.ManagedCapacityBytes},
                {"MemoryNetKBps",MemoryMetrics.NetKBps},{"GcAllocatedFrameBytes",MemoryMetrics.GcAllocatedBytes},{"GcAllocationStatus",MemoryMetrics.AllocationStatus},{"GcCollections",MemoryMetrics.GcCollections},
                {"RpcQueueDepth",rpc.QueueDepth},{"RpcQueuePeak",rpc.QueuePeak},{"RpcQueueDropped",rpc.QueueDropped},{"RpcProcessedCount",rpc.ProcessedCount},{"RpcPumpMs",rpc.PumpMs},
                {"StructuredEventsWritten",writer.EventsWritten},{"StructuredFlushMs",writer.LastFlushMs}};
            foreach(var pair in SelfMetrics.Snapshot(true))fields["Self"+pair.Key]=pair.Value;
            StructuredLogger.WriteEvent(new StructuredEvent{Timestamp=StructuredLogger.NowStamp(),FrameNumber=DiagnosticContext.Frame,Type=EventType.FpsReport,Fields=fields});
            LagLogger.Write($"[WHY_LAG] FPS {frames/Math.Max(.001,duration):F1}; p95={Statistics.P95:F2}ms p99={Statistics.P99:F2}ms; spikes={spikes}; window={duration:F2}s");
        }
        public static void Reset(){all.Clear();normal.Clear();Statistics=new FrameStatistics();NormalStatistics=new FrameStatistics();frames=spikes=runs=0;duration=sum=max=continuous=longest=lastSnapshot=0;min=double.MaxValue;}
    }
}'''
}
for rel,text in files.items(): (root/rel).write_text(text,encoding='utf-8')
print('Replaced timing adapters and FPS tracker:',len(files))
