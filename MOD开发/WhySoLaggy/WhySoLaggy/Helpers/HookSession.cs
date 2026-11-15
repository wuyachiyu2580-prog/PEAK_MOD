using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using HarmonyLib;

namespace WhySoLaggy
{
    internal sealed class HookSession
    {
        internal sealed class Target
        {
            internal MethodBase Method;
            internal string Owners, Plugin;
        }
        private readonly string scope;
        private readonly Type callbacks;
        private Harmony harmony;
        private readonly Dictionary<MethodBase, TimingEntry> installed = new Dictionary<MethodBase, TimingEntry>();
        private readonly Queue<Target> pending = new Queue<Target>();
        private double nextCleanup;
        private readonly Queue<MethodBase> removing = new Queue<MethodBase>();
        internal readonly ConcurrentDictionary<MethodBase, TimingEntry> Lookup = new ConcurrentDictionary<MethodBase, TimingEntry>();
        internal bool Automatic, Faulted;
        internal bool Active => installed.Values.Any(x => x.Active);
        internal bool Busy => pending.Count != 0 || removing.Count != 0;
        internal string Owner => harmony?.Id;
        internal int Count => installed.Count;
        internal double LastOperationMs;
        internal long Revision;
        internal HookSession(string scope, Type callbacks) { this.scope=scope; this.callbacks=callbacks; }
        internal void Request(string root, IEnumerable<Target> targets, bool automatic)
        {
            harmony = harmony ?? new Harmony(root + "." + scope);
            Automatic=automatic; Faulted=false;
            pending.Clear(); removing.Clear();
            var desired = new Dictionary<MethodBase,Target>();
            foreach(var target in targets)
                if (ProfilerTargetResolver.TryResolve(target.Method,out var declared,out _))
                { target.Method=declared; desired[declared]=target; }
            foreach(var pair in installed)
            {
                bool wanted=desired.ContainsKey(pair.Key);
                if(!wanted) { pair.Value.Active=false; System.Threading.Interlocked.Increment(ref pair.Value.Generation); removing.Enqueue(pair.Key); }
                else pair.Value.Active=true;
            }
            foreach(var pair in desired) if(!installed.ContainsKey(pair.Key)) pending.Enqueue(pair.Value);
        }
        internal void Pump(bool unlimited=false,int operationLimit=2,double budgetMs=2)
        {
            if(!Busy&&Faulted&&DiagnosticClock.Now>=nextCleanup){nextCleanup=DiagnosticClock.Now+5;foreach(var p in installed)if(!p.Value.Active)removing.Enqueue(p.Key);}
            long begin=Stopwatch.GetTimestamp(); int operations=0;
            while(Busy && (unlimited || (operations<operationLimit && DiagnosticClock.Milliseconds(Stopwatch.GetTimestamp()-begin)<budgetMs)))
            {
                long start=Stopwatch.GetTimestamp();
                if(removing.Count>0)
                {
                    var method=removing.Dequeue();
                    try { harmony.Unpatch(method,HarmonyPatchType.All,harmony.Id); installed.Remove(method); Lookup.TryRemove(method,out _);TimingLedger.Remove(scope,method); }
                    catch(Exception ex) { Faulted=true; Warn("Cleanup retained for retry: "+ex.GetType().Name); }
                }
                else
                {
                    var target=pending.Dequeue(); TimingEntry entry=null;
                    try
                    {
                        entry=TimingLedger.Register(scope,target.Method,target.Owners,target.Plugin); entry.Active=true;
                        Lookup[target.Method]=entry; installed[target.Method]=entry;
                        harmony.Patch(target.Method,prefix:new HarmonyMethod(callbacks,"TimingPrefix"){priority=Priority.First},
                            finalizer:new HarmonyMethod(callbacks,"TimingFinalizer"){priority=Priority.Last});
                    }
                    catch(Exception ex)
                    {
                        if(Automatic)Faulted=true;
                        if(entry!=null)entry.Active=false;
                        try { harmony.Unpatch(target.Method,HarmonyPatchType.All,harmony.Id); installed.Remove(target.Method); Lookup.TryRemove(target.Method,out _);TimingLedger.Remove(scope,target.Method); }
                        catch { Faulted=true; }
                        Warn("Skipped timing target: "+ex.GetType().Name+": "+ex.Message);
                    }
                }
                LastOperationMs=DiagnosticClock.Milliseconds(Stopwatch.GetTimestamp()-start);
                SelfMetrics.Add("HookInstall",LastOperationMs);
                Revision++;operations++;
                if(Automatic && LastOperationMs>20) { pending.Clear(); Faulted=true; Warn("Automatic expansion stopped: single hook operation exceeded 20 ms"); break; }
            }
        }
        internal bool Stop()
        {
            pending.Clear(); removing.Clear();
            foreach(var e in installed.Values) {e.Active=false;System.Threading.Interlocked.Increment(ref e.Generation);}
            foreach(var method in installed.Keys.ToArray())
                try {harmony.Unpatch(method,HarmonyPatchType.All,harmony.Id);installed.Remove(method);Lookup.TryRemove(method,out _);} catch{Faulted=true;}
            if(installed.Count==0){TimingLedger.Remove(scope);Lookup.Clear();Faulted=false;harmony=null;return true;}
            return false;
        }
        internal InvocationTiming Enter(MethodBase method)
        {
            if(method!=null && Lookup.TryGetValue(method,out var e))return TimingLedger.Enter(e);
            return default;
        }
        private void Warn(string message) { LagLogger.Info("[WHY_LAG] "+scope+" "+message); }
    }

    internal static class SelfMetrics
    {
        private static readonly object gate=new object();
        private static readonly Dictionary<string,Tuple<double,double,long>> values=new Dictionary<string,Tuple<double,double,long>>();
        private static double frameMs;
        internal static void Add(string name,double ms,bool countFrame=true)
        {
            lock(gate){values.TryGetValue(name,out var v);values[name]=Tuple.Create((v?.Item1??0)+ms,Math.Max(v?.Item2??0,ms),(v?.Item3??0)+1);}
            if(countFrame && DiagnosticContext.IsMainThread && (name=="MainUpdate"||name=="FrameBoundary"||name=="Hud"))frameMs+=ms;
        }
        internal static double TakeFrame(){double v=frameMs;frameMs=0;return v;}
        internal static Dictionary<string,object> Snapshot(bool reset=false)
        {
            lock(gate){var result=new Dictionary<string,object>();foreach(var p in values){result[p.Key+"TotalMs"]=p.Value.Item1;result[p.Key+"MaxMs"]=p.Value.Item2;result[p.Key+"Count"]=p.Value.Item3;}if(reset)values.Clear();return result;}
        }
    }
}
