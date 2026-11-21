using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using System.Runtime.CompilerServices;
using BepInEx.Bootstrap;
using HarmonyLib;

namespace WhySoLaggy
{
    internal static class PluginProfiler
    {
        public static bool Enabled=true;
        public static readonly HashSet<string> IgnoreGuids=new HashSet<string>(StringComparer.Ordinal);
        internal static readonly HookSession Session=new HookSession("PluginProfiler",typeof(PluginProfiler));
        private sealed class InstanceTargets
        {
            internal readonly Dictionary<MethodBase,TimingEntry> Methods=new Dictionary<MethodBase,TimingEntry>();
        }
        private static volatile ConditionalWeakTable<object,InstanceTargets> instances=new ConditionalWeakTable<object,InstanceTargets>();
        private static TimingEntry[] activeEntries=new TimingEntry[0];
        internal static List<HookSession.Target> Targets()
        {
            return BuildTargets(Chainloader.PluginInfos.Select(pair=>Tuple.Create(pair.Key,(object)pair.Value.Instance)));
        }
        internal static List<HookSession.Target> BuildTargets(IEnumerable<Tuple<string,object>> plugins)
        {
            var next=new ConditionalWeakTable<object,InstanceTargets>();
            var nextEntries=new HashSet<TimingEntry>();
            var targets=new Dictionary<MethodBase,HashSet<string>>();
            foreach(var plugin in plugins)
            {
                string guid=plugin.Item1;object instance=plugin.Item2;
                if(instance==null||guid==WhySoLaggyPlugin.PluginGuid||IgnoreGuids.Contains(guid))continue;
                Type type=instance.GetType();
                var instanceTargets=new InstanceTargets();
                foreach(string name in new[]{"Update","LateUpdate","FixedUpdate"})
                {
                    MethodInfo method=null;
                    for(Type t=type;t!=null&&t!=typeof(UnityEngine.MonoBehaviour);t=t.BaseType)
                    {method=t.GetMethod(name,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly,null,Type.EmptyTypes,null);if(method!=null)break;}
                    if(method!=null&&ProfilerTargetResolver.TryResolve(method,out var declared,out _))
                    {
                        TimingEntry entry=TimingLedger.Register("PluginProfiler",declared,guid,guid);
                        entry.Owners=guid;entry.PluginGuid=guid;entry.Active=true;
                        instanceTargets.Methods[declared]=entry;nextEntries.Add(entry);
                        if(!targets.TryGetValue(declared,out var owners))targets[declared]=owners=new HashSet<string>(StringComparer.Ordinal);
                        owners.Add(guid);
                    }
                }
                next.Add(instance,instanceTargets);
            }
            foreach(var entry in activeEntries)
                if(!nextEntries.Contains(entry)){entry.Active=false;System.Threading.Interlocked.Increment(ref entry.Generation);}
            activeEntries=nextEntries.ToArray();instances=next;
            return targets.OrderBy(x=>ProfilerTargetResolver.Identity(x.Key),StringComparer.Ordinal)
                .Select(x=>{var owners=x.Value.OrderBy(g=>g,StringComparer.Ordinal).ToArray();return new HookSession.Target{Method=x.Key,Owners=string.Join(", ",owners),Plugin=owners[0]};}).ToList();
        }
        internal static bool TryGetEntry(object instance,MethodBase method,out TimingEntry entry)
        {
            entry=null;
            return instance!=null&&instances.TryGetValue(instance,out var targets)&&targets.Methods.TryGetValue(method,out entry);
        }
        public static void Initialize(Harmony harmony){if(!Enabled||Session.Active)return;Session.Request(harmony.Id,Targets(),false);Session.Pump(true);}
        internal static void Request(bool enabled,bool automatic){if(!enabled)foreach(var e in activeEntries){e.Active=false;System.Threading.Interlocked.Increment(ref e.Generation);}Enabled=enabled;Session.Request(WhySoLaggyPlugin.PluginGuid,enabled?Targets():new List<HookSession.Target>(),automatic);}
        internal static void Pump(int operations=2,double budgetMs=2)=>Session.Pump(false,operations,budgetMs);
        public static void TimingPrefix(MethodBase __originalMethod,object __instance,out InvocationTiming __state)
        {__state=default;try{if(Enabled&&__instance!=null&&Session.Lookup.TryGetValue(__originalMethod,out var hook)&&hook.Active&&TryGetEntry(__instance,__originalMethod,out var entry))__state=TimingLedger.Enter(entry);}catch{}}
        public static Exception TimingFinalizer(Exception __exception,ref InvocationTiming __state)
        {try{TimingLedger.Exit(ref __state,__exception);}catch{}return __exception;}
        public static void WriteReport(int frames)=>ProfilerReports.Write("PluginProfiler",frames,30);
        public static void WriteSpikeDetail(float milliseconds){}
        public static void ResetFrameTimers(){}
        public static void Shutdown(){Enabled=false;Session.Stop();foreach(var entry in activeEntries){entry.Active=false;System.Threading.Interlocked.Increment(ref entry.Generation);}activeEntries=new TimingEntry[0];instances=new ConditionalWeakTable<object,InstanceTargets>();IgnoreGuids.Clear();}
    }
}
