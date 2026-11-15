using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using BepInEx.Bootstrap;
using HarmonyLib;

namespace WhySoLaggy
{
    internal static class PluginProfiler
    {
        public static bool Enabled=true;
        public static readonly HashSet<string> IgnoreGuids=new HashSet<string>(StringComparer.Ordinal);
        internal static readonly HookSession Session=new HookSession("PluginProfiler",typeof(PluginProfiler));
        private static volatile Dictionary<Type,Dictionary<MethodBase,TimingEntry>> instances=new Dictionary<Type,Dictionary<MethodBase,TimingEntry>>();
        internal static List<HookSession.Target> Targets()
        {
            var result=new List<HookSession.Target>();var next=new Dictionary<Type,Dictionary<MethodBase,TimingEntry>>();
            foreach(var pair in Chainloader.PluginInfos)
            {
                if(pair.Value.Instance==null||pair.Key==WhySoLaggyPlugin.PluginGuid||IgnoreGuids.Contains(pair.Key))continue;
                Type type=pair.Value.Instance.GetType();
                foreach(string name in new[]{"Update","LateUpdate","FixedUpdate"})
                {
                    MethodInfo method=null;
                    for(Type t=type;t!=null&&t!=typeof(UnityEngine.MonoBehaviour);t=t.BaseType)
                    {method=t.GetMethod(name,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly,null,Type.EmptyTypes,null);if(method!=null)break;}
                    if(method!=null&&ProfilerTargetResolver.TryResolve(method,out var declared,out _))
                    {
                        if(!next.TryGetValue(type,out var map))next[type]=map=new Dictionary<MethodBase,TimingEntry>();
                        map[declared]=TimingLedger.Register("PluginProfiler",declared,pair.Key,pair.Key);map[declared].Active=true;
                        result.Add(new HookSession.Target{Method=declared,Owners=pair.Key});
                    }
                }
            }
            instances=next;return result;
        }
        public static void Initialize(Harmony harmony){if(!Enabled||Session.Active)return;Session.Request(harmony.Id,Targets(),false);Session.Pump(true);}
        internal static void Request(bool enabled,bool automatic){if(!enabled)foreach(var e in TimingLedger.All("PluginProfiler")){e.Active=false;System.Threading.Interlocked.Increment(ref e.Generation);}Enabled=enabled;Session.Request(WhySoLaggyPlugin.PluginGuid,enabled?Targets():new List<HookSession.Target>(),automatic);}
        internal static void Pump(int operations=2,double budgetMs=2)=>Session.Pump(false,operations,budgetMs);
        public static void TimingPrefix(MethodBase __originalMethod,object __instance,out InvocationTiming __state)
        {__state=default;try{if(Enabled&&__instance!=null&&Session.Lookup.TryGetValue(__originalMethod,out var hook)&&hook.Active&&instances.TryGetValue(__instance.GetType(),out var map)&&map.TryGetValue(__originalMethod,out var entry))__state=TimingLedger.Enter(entry);}catch{}}
        public static Exception TimingFinalizer(Exception __exception,ref InvocationTiming __state)
        {try{TimingLedger.Exit(ref __state,__exception);}catch{}return __exception;}
        public static void WriteReport(int frames)=>ProfilerReports.Write("PluginProfiler",frames,30);
        public static void WriteSpikeDetail(float milliseconds){}
        public static void ResetFrameTimers(){}
        public static void Shutdown(){Enabled=false;Session.Stop();IgnoreGuids.Clear();}
    }
}