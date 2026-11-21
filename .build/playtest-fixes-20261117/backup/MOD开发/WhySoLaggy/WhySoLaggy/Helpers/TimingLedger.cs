using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;

namespace WhySoLaggy
{
    internal sealed class MethodFrame
    {
        internal string Id, Name, Scope, Owners, PluginGuid;
        internal long Calls, Samples, Exceptions;
        internal double Ms, BackgroundMs;
    }
    internal sealed class TimingEntry
    {
        internal string Id, Name, Scope, Owners, PluginGuid;
        internal MethodBase Target;
        internal long Calls, Samples, Ticks, BackgroundTicks, Exceptions, Entered;
        internal readonly object TotalsLock = new object();
        internal long FrameCalls, FrameSamples, FrameTicks, FrameExceptions;
        internal long Generation;
        internal volatile bool Active = true;
        internal volatile bool Adaptive;
        internal double Threshold,NextThreadCleanup;
        internal readonly object ThreadsLock = new object();
        internal readonly Dictionary<int, Tuple<WeakReference, long, double>> Threads = new Dictionary<int, Tuple<WeakReference, long, double>>();
    }
    internal struct InvocationTiming
    {
        internal TimingEntry Entry;
        internal long Start, Generation, Frame;
        internal bool MainThread, Sampled, Complete;
    }
    internal static class TimingLedger
    {
        private static readonly object RegistryLock = new object();
        private static readonly Dictionary<string, TimingEntry> Entries = new Dictionary<string, TimingEntry>();
        private struct Interval { internal long Start, End; internal Interval(long start,long end){Start=start;End=end;} }
        private static readonly List<Interval> Intervals = new List<Interval>(512);
        private static TimingEntry[] cached = new TimingEntry[0];
        private static readonly Comparison<Interval> ByStart=(a,b)=>a.Start.CompareTo(b.Start);
        private static long frame, selfTicks;
        private static bool intervalOverflow;
        internal static TimingEntry Register(string scope, MethodBase target, string owners, string pluginGuid = null)
        {
            string id = scope + ":" + ProfilerTargetResolver.Identity(target) + (string.IsNullOrEmpty(pluginGuid)?"":":"+pluginGuid);
            lock (RegistryLock)
            {
                if (Entries.TryGetValue(id, out TimingEntry existing)) return existing;
                if (Entries.Count >= 8192) throw new InvalidOperationException("Timing registry capacity reached");
                var entry = new TimingEntry { Id = id, Name = MethodKey.Canonical(target), Target = target,
                    Scope = scope, Owners = owners ?? "", PluginGuid = pluginGuid ?? "" };
                Entries.Add(id, entry); cached=Entries.Values.ToArray(); return entry;
            }
        }
        internal static InvocationTiming Enter(TimingEntry entry)
        {
            if (entry == null || !entry.Active) return default;
            long overhead = Stopwatch.GetTimestamp();
            long calls = Interlocked.Increment(ref entry.Entered);
            bool main = DiagnosticContext.IsMainThread;
            bool take = !entry.Adaptive || Interlocked.Read(ref entry.Samples) < 32 ||
                DiagnosticClock.Milliseconds(Interlocked.Read(ref entry.Ticks)) / Math.Max(1, Interlocked.Read(ref entry.Samples)) >= entry.Threshold || calls % 10 == 0;

            var state = new InvocationTiming { Entry = entry, Generation = Interlocked.Read(ref entry.Generation), Frame = frame,
                MainThread = main, Sampled = take, Start = take ? Stopwatch.GetTimestamp() : 0 };
            if (main) selfTicks += Stopwatch.GetTimestamp() - overhead;
            return state;
        }
        internal static void Exit(ref InvocationTiming state, Exception exception)
        {
            long end = Stopwatch.GetTimestamp();
            TimingEntry entry = state.Entry;
            if (state.Complete || entry == null) return;
            state.Complete = true;
            if (!entry.Active || state.Generation != Interlocked.Read(ref entry.Generation)) return;
            lock(entry.TotalsLock)
            {
                entry.Calls++;
                if(exception!=null)entry.Exceptions++;
                if(state.Sampled){entry.Samples++;entry.Ticks+=Math.Max(0,end-state.Start);if(!state.MainThread)entry.BackgroundTicks+=Math.Max(0,end-state.Start);}
            }
            if(state.MainThread && state.Frame==frame){entry.FrameCalls++;if(exception!=null)entry.FrameExceptions++;}
            if (state.Sampled)
            {
                long elapsed = Math.Max(0, end - state.Start);

                if (state.MainThread && state.Frame == frame)
                {
                    entry.FrameTicks += elapsed; entry.FrameSamples++;

                    if (Intervals.Count < 60000) Intervals.Add(new Interval(state.Start, end)); else intervalOverflow = true;
                }
                else if (!state.MainThread)
                {

                    lock (entry.ThreadsLock)
                    {
                        int tid = Thread.CurrentThread.ManagedThreadId;
                        if(DiagnosticClock.Now>=entry.NextThreadCleanup){entry.NextThreadCleanup=DiagnosticClock.Now+5;foreach (var key in entry.Threads.Where(x => !x.Value.Item1.IsAlive || DiagnosticClock.Now - x.Value.Item3 > 60).Select(x => x.Key).ToArray()) entry.Threads.Remove(key);}
                        if (entry.Threads.ContainsKey(tid) || entry.Threads.Count < 64)
                        {
                            entry.Threads.TryGetValue(tid, out var old);
                            entry.Threads[tid] = Tuple.Create(old?.Item1 ?? new WeakReference(Thread.CurrentThread), (old?.Item2 ?? 0) + elapsed, DiagnosticClock.Now);
                        }
                    }
                }
            }
            if (state.MainThread) selfTicks += Stopwatch.GetTimestamp() - end;
        }
        internal static TimingEntry[] All(string scope = null)
        {
            lock (RegistryLock) return Entries.Values.Where(x => scope == null || x.Scope == scope).ToArray();
        }
        internal static List<MethodFrame> Snapshot(string scope, bool reset)
        {
            var result = new List<MethodFrame>();
            foreach (var e in All(scope))
            {
                long calls,samples,ticks,bg,errors;
                lock(e.TotalsLock){calls=e.Calls;samples=e.Samples;ticks=e.Ticks;bg=e.BackgroundTicks;errors=e.Exceptions;if(reset)e.Calls=e.Samples=e.Ticks=e.BackgroundTicks=e.Exceptions=0;}
                if (calls > 0) result.Add(new MethodFrame { Id=e.Id, Name=e.Name, Scope=e.Scope, Owners=e.Owners, PluginGuid=e.PluginGuid,
                    Calls=calls, Samples=samples, Exceptions=errors, Ms=DiagnosticClock.Milliseconds(Math.Max(0,ticks-bg)), BackgroundMs=DiagnosticClock.Milliseconds(bg) });
            }
            return result;
        }
        internal static void Begin(long number)
        {
            frame = number; Intervals.Clear(); selfTicks = 0; intervalOverflow = false;
            foreach (var entry in cached) { entry.FrameCalls = entry.FrameSamples = entry.FrameTicks = entry.FrameExceptions = 0; }
        }
        internal static double UnionMilliseconds(IEnumerable<Tuple<long, long>> intervals, long start, long end)
        {
            long total = 0, left = start, right = start;
            foreach (var item in intervals.OrderBy(x => x.Item1))
            {
                long a = Math.Max(start, item.Item1), b = Math.Min(end, item.Item2);
                if (b <= a) continue;
                if (a > right) { total += right - left; left = a; right = b; } else right = Math.Max(right, b);
            }
            return DiagnosticClock.Milliseconds(total + right - left);
        }
        internal static FrameRecord Seal(long number, long start, long end)
        {
            var record = new FrameRecord { Frame = number, Time = DiagnosticClock.Now, DurationMs = DiagnosticClock.Milliseconds(end-start),
                CoveredMs = FrameUnion(start, end), SelfMs = DiagnosticClock.Milliseconds(selfTicks), Complete = !intervalOverflow };
            foreach (var e in cached) if (e.FrameCalls > 0) record.Methods.Add(new MethodFrame { Id=e.Id, Name=e.Name, Scope=e.Scope, Owners=e.Owners,
                PluginGuid=e.PluginGuid, Calls=e.FrameCalls, Samples=e.FrameSamples, Exceptions=e.FrameExceptions, Ms=DiagnosticClock.Milliseconds(e.FrameTicks) });
            return record;
        }
        private static double FrameUnion(long start,long end)
        {
            Intervals.Sort(ByStart);long total=0,left=start,right=start;
            foreach(var i in Intervals){long a=Math.Max(start,i.Start),b=Math.Min(end,i.End);if(b<=a)continue;if(a>right){total+=right-left;left=a;right=b;}else right=Math.Max(right,b);}
            return DiagnosticClock.Milliseconds(total+right-left);
        }
        internal static void Remove(string scope,MethodBase target=null)
        {
            lock(RegistryLock){foreach(var pair in Entries.Where(x=>x.Value.Scope==scope&&(target==null||x.Value.Target==target)).ToArray())
            {pair.Value.Active=false;Interlocked.Increment(ref pair.Value.Generation);Entries.Remove(pair.Key);}cached=Entries.Values.ToArray();}
        }
        internal static void Clear() { foreach (string scope in All().Select(x=>x.Scope).Distinct().ToArray()) Remove(scope); Intervals.Clear(); }
    }
}
