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
        internal long Calls, Samples, Exceptions, AdaptiveCalls;
        internal string SamplingMode => AdaptiveCalls == 0 ? "Exact" : AdaptiveCalls == Calls ? "Adaptive" : "Mixed";
        internal double Ms, BackgroundMs;
    }
    internal sealed class TimingEntry
    {
        internal string Id, Name, Scope, Owners, PluginGuid;
        internal MethodBase Target;
        internal long Calls, Samples, Ticks, BackgroundTicks, Exceptions, AdaptiveCalls, Entered;
        // Main-thread completions are published once per touched entry at the frame boundary.
        internal long MainCalls, MainSamples, MainTicks, MainExceptions, MainAdaptiveCalls;
        internal long MainEntered, MainObservedSamples, MainObservedTicks;
        internal long BackgroundObservedSamples, BackgroundObservedTicks;
        internal readonly object TotalsLock = new object();
        internal long FrameCalls, FrameSamples, FrameTicks, FrameExceptions;
        internal long Generation;
        internal volatile bool Active = true;
        internal volatile bool Adaptive;
        internal double Threshold;
        internal long FrameAdaptiveCalls;
        internal bool Touched;

    }
    internal struct InvocationTiming
    {
        internal TimingEntry Entry;
        internal long Start, Generation, Frame;
        internal bool MainThread, Sampled, Complete, Adaptive;
    }
    internal static class TimingLedger
    {
        private static readonly object RegistryLock = new object();
        private static readonly Dictionary<string, TimingEntry> Entries = new Dictionary<string, TimingEntry>();
        private struct Interval { internal long Start, End; internal Interval(long start,long end){Start=start;End=end;} }
        private static readonly List<Interval> Intervals = new List<Interval>(512);
        private static readonly List<TimingEntry> touched = new List<TimingEntry>();
        private static long boundaryCalls, boundaryExceptions;
        internal static long BoundaryCalls => Interlocked.Read(ref boundaryCalls);
        internal static long BoundaryExceptions => Interlocked.Read(ref boundaryExceptions);
        private static readonly Comparison<Interval> ByStart=(a,b)=>a.Start.CompareTo(b.Start);
        private static long frame, selfTicks;
        private static int intervalsOmitted;
        internal static TimingEntry Register(string scope, MethodBase target, string owners, string pluginGuid = null)
        {
            string id = scope + ":" + ProfilerTargetResolver.Identity(target) + (string.IsNullOrEmpty(pluginGuid)?"":":"+pluginGuid);
            lock (RegistryLock)
            {
                if (Entries.TryGetValue(id, out TimingEntry existing)) return existing;
                if (Entries.Count >= 8192) throw new InvalidOperationException("Timing registry capacity reached");
                var entry = new TimingEntry { Id = id, Name = MethodKey.Canonical(target), Target = target,
                    Scope = scope, Owners = owners ?? "", PluginGuid = pluginGuid ?? "" };
                Entries.Add(id, entry);  return entry;
            }
        }
        internal static InvocationTiming Enter(TimingEntry entry)
        {
            if (entry == null || !entry.Active) return default;
            long overhead = Stopwatch.GetTimestamp();
            bool main = DiagnosticContext.IsMainThread;
            bool adaptive = entry.Adaptive;
            bool take = true;
            if (adaptive)
            {
                long calls = main ? ++entry.MainEntered : Interlocked.Increment(ref entry.Entered);
                long samples = main ? entry.MainObservedSamples : Interlocked.Read(ref entry.BackgroundObservedSamples);
                long ticks = main ? entry.MainObservedTicks : Interlocked.Read(ref entry.BackgroundObservedTicks);
                take = samples < 32 || calls % 10 == 0 || DiagnosticClock.Milliseconds(ticks) >= entry.Threshold * samples;
            }
            var state = new InvocationTiming { Entry = entry, Generation = Interlocked.Read(ref entry.Generation), Frame = frame,
                MainThread = main, Adaptive = adaptive, Sampled = take, Start = take ? Stopwatch.GetTimestamp() : 0 };
            if (main) selfTicks += Stopwatch.GetTimestamp() - overhead;
            return state;
        }
        internal static void Exit(ref InvocationTiming state, Exception exception)
        {
            long end = Stopwatch.GetTimestamp();
            TimingEntry entry = state.Entry;
            if (state.Complete || entry == null) return;
            state.Complete = true;
            long elapsed = state.Sampled ? Math.Max(0, end - state.Start) : 0;
            if (state.MainThread)
            {
                if (CrossedBoundary(entry, state, exception)) return;
                if (!entry.Touched) { touched.Add(entry); entry.Touched = true; }
                entry.MainCalls++;
                if (exception != null) entry.MainExceptions++;
                if (state.Adaptive) entry.MainAdaptiveCalls++;
                if (state.Sampled)
                {
                    entry.MainSamples++; entry.MainTicks += elapsed;
                    entry.MainObservedSamples++; entry.MainObservedTicks += elapsed;
                }
                if (state.Frame == frame)
                {
                    entry.FrameCalls++;
                    if (exception != null) entry.FrameExceptions++;
                    if (state.Adaptive) entry.FrameAdaptiveCalls++;
                    if (state.Sampled)
                    {
                        entry.FrameSamples++; entry.FrameTicks += elapsed;
                        if (Intervals.Count < 60000) Intervals.Add(new Interval(state.Start, end));
                        else intervalsOmitted++;
                    }
                }
                else CountBoundary(exception);
                selfTicks += Stopwatch.GetTimestamp() - end;
            }
            else lock (entry.TotalsLock)
            {
                if (CrossedBoundary(entry, state, exception)) return;
                entry.Calls++;
                if (exception != null) entry.Exceptions++;
                if (state.Adaptive) entry.AdaptiveCalls++;
                if (state.Sampled)
                {
                    entry.Samples++; entry.Ticks += elapsed; entry.BackgroundTicks += elapsed;
                    entry.BackgroundObservedSamples++; entry.BackgroundObservedTicks += elapsed;
                }
            }
        }
        private static bool CrossedBoundary(TimingEntry entry, InvocationTiming state, Exception error)
        {
            if (entry.Active && state.Generation == Interlocked.Read(ref entry.Generation)) return false;
            CountBoundary(error);
            return true;
        }
        private static void CountBoundary(Exception error)
        {
            Interlocked.Increment(ref boundaryCalls);
            if (error != null) Interlocked.Increment(ref boundaryExceptions);
        }
        private static void PublishMain(TimingEntry e)
        {
            if (e.MainCalls == 0) return;
            lock (e.TotalsLock)
            {
                e.Calls += e.MainCalls; e.Samples += e.MainSamples; e.Ticks += e.MainTicks;
                e.Exceptions += e.MainExceptions; e.AdaptiveCalls += e.MainAdaptiveCalls;
                e.MainCalls = e.MainSamples = e.MainTicks = e.MainExceptions = e.MainAdaptiveCalls = 0;
            }
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
                if (DiagnosticContext.IsMainThread) PublishMain(e);
                long calls,samples,ticks,bg,errors,adaptive;
                lock(e.TotalsLock){calls=e.Calls;samples=e.Samples;ticks=e.Ticks;bg=e.BackgroundTicks;errors=e.Exceptions;adaptive=e.AdaptiveCalls;if(reset)e.AdaptiveCalls=e.Calls=e.Samples=e.Ticks=e.BackgroundTicks=e.Exceptions=0;}
                if (calls > 0) result.Add(new MethodFrame { Id=e.Id, Name=e.Name, Scope=e.Scope, Owners=e.Owners, PluginGuid=e.PluginGuid,
                    Calls=calls, AdaptiveCalls=adaptive, Samples=samples, Exceptions=errors, Ms=DiagnosticClock.Milliseconds(Math.Max(0,ticks-bg)), BackgroundMs=DiagnosticClock.Milliseconds(bg) });
            }
            return result;
        }
        internal static void Begin(long number)
        {
            frame = number; Intervals.Clear(); selfTicks = 0; intervalsOmitted = 0;
            foreach (var entry in touched)
            {
                PublishMain(entry);
                entry.FrameCalls = entry.FrameSamples = entry.FrameTicks = entry.FrameExceptions = entry.FrameAdaptiveCalls = 0;
                entry.Touched = false;
            }
            touched.Clear();
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
            using(DiagnosticPhases.Measure(DiagnosticPhases.Stage.Seal))
            {
                var record = new FrameRecord { Frame = number, Time = DiagnosticClock.Now, DurationMs = DiagnosticClock.Milliseconds(end-start),
                    CoveredMs = FrameUnion(start, end), SelfMs = DiagnosticClock.Milliseconds(selfTicks), IntervalsOmitted = intervalsOmitted, Complete = intervalsOmitted == 0 };
                foreach (var e in touched) if (e.FrameCalls > 0) record.Methods.Add(new MethodFrame { Id=e.Id, Name=e.Name, Scope=e.Scope, Owners=e.Owners,
                    PluginGuid=e.PluginGuid, Calls=e.FrameCalls, AdaptiveCalls=e.FrameAdaptiveCalls, Samples=e.FrameSamples, Exceptions=e.FrameExceptions, Ms=DiagnosticClock.Milliseconds(e.FrameTicks) });
                return record;
            }
        }
        private static double FrameUnion(long start,long end)
        {
            Intervals.Sort(ByStart);long total=0,left=start,right=start;
            foreach(var i in Intervals){long a=Math.Max(start,i.Start),b=Math.Min(end,i.End);if(b<=a)continue;if(a>right){total+=right-left;left=a;right=b;}else right=Math.Max(right,b);}
            return DiagnosticClock.Milliseconds(total+right-left);
        }
        internal static void Remove(string scope,MethodBase target=null)
        {
            lock (RegistryLock)
            {
                foreach (var pair in Entries.Where(x => x.Value.Scope == scope && (target == null || x.Value.Target == target)).ToArray())
                {
                    var entry = pair.Value;
                    lock (entry.TotalsLock)
                    {
                        entry.Active = false;
                        Interlocked.Increment(ref entry.Generation);
                        PublishMain(entry);
                        // Completed calls still waiting for a periodic report are disclosed at removal.
                        Interlocked.Add(ref retiredCalls, entry.Calls);
                        Interlocked.Add(ref retiredExceptions, entry.Exceptions);
                        Entries.Remove(pair.Key);
                    }
                }
            }
        }
        private static long retiredCalls, retiredExceptions;
        internal static long RetiredCalls => Interlocked.Read(ref retiredCalls);
        internal static long RetiredExceptions => Interlocked.Read(ref retiredExceptions);
        internal static void Clear() { foreach (string scope in All().Select(x=>x.Scope).Distinct().ToArray()) Remove(scope); Begin(0); }
    }
}
