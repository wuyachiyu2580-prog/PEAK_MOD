using System;
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
        internal static bool HasPendingCleanup=>Session.Faulted && Session.Count!=0;
        internal static string TimingHarmonyId=>Session.Owner;
        public struct TimingState { internal InvocationTiming Invocation; }
        internal static bool IsOwnOwner(string owner)
        { string root=OwnHarmonyId??WhySoLaggyPlugin.PluginGuid; return owner==root || new[]{"PatchProfiler","PluginProfiler","RpcMonitor","AbuseDetection","MethodTracer","FieldProbe","MethodTracer.Staging","FieldProbe.Staging"}.Any(x=>owner==root+"."+x); }
        internal static List<HookSession.Target> Targets(HashSet<string> owners=null,int limit=int.MaxValue)
        {
            var map=new Dictionary<string,HookSession.Target>();
            foreach(var source in Harmony.GetAllPatchedMethods().ToArray())
            {
                if(!ProfilerTargetResolver.TryResolve(source,out var target,out _))continue;
                if(MethodKey.IsIgnored(source,IgnoreMethods)||MethodKey.IsIgnored(target,IgnoreMethods))continue;
                var info=Harmony.GetPatchInfo(source);if(info==null)continue;
                var external=info.Owners.Where(x=>!IsOwnOwner(x)).ToArray();
                if(external.Length==0)continue;
                if(owners!=null)
                {
                    var modules=BepInEx.Bootstrap.Chainloader.PluginInfos.Where(x=>owners.Contains(x.Key)&&x.Value.Instance!=null).Select(x=>x.Value.Instance.GetType().Assembly).Distinct().ToArray();
                    bool linked=info.Prefixes.Concat(info.Postfixes).Concat(info.Transpilers).Concat(info.Finalizers).Concat(info.ILManipulators).Any(p=>!IsOwnOwner(p.owner)&&p.PatchMethod!=null&&modules.Contains(p.PatchMethod.Module.Assembly));
                    if(!linked)continue;
                }
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
        internal static void Request(bool enabled,HashSet<string> owners=null,bool automatic=false,MethodBase[] extra=null)
        {Enabled=enabled;var targets=enabled?Targets(owners,automatic?50:int.MaxValue):new List<HookSession.Target>();
            if(enabled&&extra!=null)foreach(var method in extra)if(targets.Count<50&&!targets.Any(x=>x.Method==method))targets.Add(new HookSession.Target{Method=method,Owners="unattributed exception target"});
            Session.Request(OwnHarmonyId??WhySoLaggyPlugin.PluginGuid,targets,automatic);}
        internal static void Pump(int operations=2,double budgetMs=2){Session.Pump(false,operations,budgetMs);ApplySampling();}
        private static long samplingRevision=-1;
        private static bool previousAdaptive;
        private static float previousThreshold=-1;
        private static void ApplySampling()
        {bool adaptive=Adaptive&&!Session.Automatic;if(samplingRevision==Session.Revision&&adaptive==previousAdaptive&&MinReportMs==previousThreshold)return;samplingRevision=Session.Revision;previousAdaptive=adaptive;previousThreshold=MinReportMs;foreach(var e in TimingLedger.All("PatchProfiler")){e.Adaptive=adaptive;e.Threshold=MinReportMs;}}
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
        internal static bool Stop(){return Session.Stop();}
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
}
