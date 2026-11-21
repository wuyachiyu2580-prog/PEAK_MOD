using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using UnityEngine;
using UnityEngine.LowLevel;
using UnityEngine.PlayerLoop;
using UnityEngine.SceneManagement;
using UnityEngine.Profiling;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;

namespace WhySoLaggy
{
    internal static class DiagnosticClock
    {
        internal static readonly long Origin = Stopwatch.GetTimestamp();
        internal static double Now => (Stopwatch.GetTimestamp() - Origin) / (double)Stopwatch.Frequency;
        internal static double Milliseconds(long ticks) => ticks * 1000d / Stopwatch.Frequency;
    }

    internal static class DiagnosticContext
    {
        internal static string SessionId = Guid.NewGuid().ToString("N");
        internal static string CaptureId = "";
        internal static int RoomEpoch;
        internal static int MainThreadId;
        internal static long Frame;
        internal static string State = "Startup";
        internal static string Scene = "";
        internal static bool Valid;
        internal static bool IsMainThread => Thread.CurrentThread.ManagedThreadId == MainThreadId;
        internal static string BuildId => typeof(DiagnosticContext).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "1.0.5-dev";
    }

    internal sealed class FrameRecord
    {
        internal long Frame;
        internal double Time, DurationMs, CoveredMs, SelfMs;
        internal string State, Scene;
        internal bool Complete;
        internal int Epoch, IntervalsOmitted;
        internal List<MethodFrame> Methods = new List<MethodFrame>();
    }

    internal sealed class FrameWindow
    {
        private struct Sample { internal double Time, Ms; }
        private readonly Queue<Sample> samples = new Queue<Sample>();
        private double[] scratch = new double[256];
        internal long CapacityEvictions { get; private set; }
        internal const int MaxSamples = 60000;
        internal void Add(double now, double milliseconds)
        {
            if (milliseconds <= 0 || double.IsNaN(milliseconds) || double.IsInfinity(milliseconds)) return;
            samples.Enqueue(new Sample { Time=now, Ms=milliseconds });
            while (samples.Count > 0 && samples.Peek().Time < now - 30) samples.Dequeue();
            if (samples.Count > MaxSamples) { samples.Dequeue(); CapacityEvictions++; }
        }
        internal FrameStatistics Snapshot(double now)
        {
            using(DiagnosticPhases.Measure(DiagnosticPhases.Stage.Statistics))
            {
                while (samples.Count > 0 && samples.Peek().Time < now - 30) samples.Dequeue();
                if(scratch.Length<samples.Count)Array.Resize(ref scratch,Math.Min(MaxSamples,Math.Max(samples.Count,scratch.Length*2)));
                int count=0;foreach(var sample in samples)scratch[count++]=sample.Ms;
                return CalculateSorted(scratch,count);
            }
        }
        internal static FrameStatistics Calculate(IEnumerable<double> values)
        {
            double[] data = values.Where(x => x > 0 && !double.IsNaN(x) && !double.IsInfinity(x)).ToArray();
            return CalculateSorted(data,data.Length);
        }
        private static FrameStatistics CalculateSorted(double[] data,int count)
        {
            if(count==0)return new FrameStatistics();Array.Sort(data,0,count);
            int slow=Math.Max(1,(int)Math.Ceiling(count*.01));double sum=0,slowSum=0;
            for(int i=0;i<count;i++){sum+=data[i];if(i>=count-slow)slowSum+=data[i];}
            return new FrameStatistics{Count=count,Mean=sum/count,Median=data[(count-1)/2],P95=data[(int)Math.Ceiling(count*.95)-1],P99=data[(int)Math.Ceiling(count*.99)-1],Low1=count>=100?(double?)(1000d*slow/slowSum):null};
        }
        internal void Clear() { samples.Clear(); CapacityEvictions = 0; }
    }
    internal sealed class FrameStatistics
    {
        internal int Count;
        internal double Mean, Median, P95, P99;
        internal double? Low1;
    }

    internal static class MemoryMetrics
    {
        internal static long? UnityBytes, ManagedUsedBytes, ManagedCapacityBytes, GcAllocatedBytes;
        internal static double? NetKBps, ManagedUsedNetKBps;
        internal static int GcCollections, GcCollectionsGen1, GcCollectionsGen2;
        internal static string AllocationStatus => !FpsTracker.EnableMemoryMonitor ? "Disabled" : recorderValid ? "Available" : "Unavailable";
        private static long? previous;
        private static long? previousManagedUsed;
        private static int[] collections;
        private static double last;
        private static ProfilerRecorder allocation;
        private static bool recorderValid;
        internal static double? NetRate(long? before, long current, double seconds) => before.HasValue && seconds > 0 ? (double?)((current - before.Value) / 1024d / seconds) : null;
        internal static void Initialize()
        {
            Shutdown();
            try
            {
                var handles = new List<ProfilerRecorderHandle>();
                ProfilerRecorderHandle.GetAvailable(handles);
                foreach (var handle in handles)
                {
                    var description = ProfilerRecorderHandle.GetDescription(handle);
                    if (description.Name != "GC Allocated In Frame" || description.UnitType.ToString() != "Bytes") continue;
                    allocation = new ProfilerRecorder(handle, 1, ProfilerRecorderOptions.Default | ProfilerRecorderOptions.StartImmediately);
                    recorderValid = allocation.Valid;
                    break;
                }
            }
            catch { recorderValid = false; }
        }
        internal static void ReadFrame()
        {
            GcAllocatedBytes = null;
            if (!recorderValid || !FpsTracker.EnableMemoryMonitor) return;
            try { recorderValid = allocation.Valid; if (recorderValid && allocation.Count > 0) GcAllocatedBytes = allocation.LastValue; }
            catch { recorderValid = false; }
        }
        internal static void Tick(double now)
        {
            if (now - last < 1) return;
            double elapsed = now - last; last = now;
            if (!FpsTracker.EnableMemoryMonitor) { UnityBytes = ManagedUsedBytes = ManagedCapacityBytes = null; NetKBps = ManagedUsedNetKBps = null; previous=previousManagedUsed=null;collections=null;return; }
            try
            {
                long value = Profiler.GetTotalAllocatedMemoryLong();
                UnityBytes = value; NetKBps = NetRate(previous, value, elapsed); previous = value;
                long managedUsed = Profiler.GetMonoUsedSizeLong();
                ManagedUsedBytes = managedUsed; ManagedUsedNetKBps = NetRate(previousManagedUsed, managedUsed, elapsed); previousManagedUsed = managedUsed;
                ManagedCapacityBytes = Profiler.GetMonoHeapSizeLong();
                int[] current = Enumerable.Range(0, GC.MaxGeneration + 1).Select(GC.CollectionCount).ToArray();
                if (collections != null)
                {
                    GcCollections += CollectionDelta(collections, current, 0);
                    if (current.Length > 1) GcCollectionsGen1 += CollectionDelta(collections, current, 1);
                    if (current.Length > 2) GcCollectionsGen2 += CollectionDelta(collections, current, 2);
                }
                collections = current;
            }
            catch { UnityBytes = ManagedUsedBytes = ManagedCapacityBytes = null; NetKBps = ManagedUsedNetKBps = null; }
        }
        internal static void Shutdown()
        {
            try { allocation.Dispose(); } catch { }
            allocation = default; recorderValid = false; previous = previousManagedUsed = null; collections = null; last = 0;GcCollections=GcCollectionsGen1=GcCollectionsGen2=0;
            UnityBytes = ManagedUsedBytes = ManagedCapacityBytes = GcAllocatedBytes = null; NetKBps = ManagedUsedNetKBps = null;
        }

        internal static int CollectionDelta(int[] before, int[] after, int generation)
        {
            if (before == null || after == null || generation < 0 || generation >= before.Length || generation >= after.Length) return 0;
            return Math.Max(0, after[generation] - before[generation]);
        }
    }

    internal static class FrameCoordinator
    {
        private struct WhySoLaggyFrameBoundary { }
        private static long started;
        private static int frameEpoch;
        private static double lastCheck;
        private static bool active;
        private static PropertyInfo loading;
        internal static event Action<FrameRecord> Completed;
        internal static double StableSince;
        internal static bool Transition;
        internal static void Initialize()
        {
            DiagnosticContext.MainThreadId = Thread.CurrentThread.ManagedThreadId;
            DiagnosticContext.Frame = 0; DiagnosticContext.Valid = false; started = 0; active = true;
            loading = HarmonyLib.AccessTools.TypeByName("LoadingScreenHandler")?.GetProperty("loading", BindingFlags.Public | BindingFlags.Static);
            SceneManager.activeSceneChanged += OnSceneChanged;
            Install(); MemoryMetrics.Initialize();
        }
        private static void OnSceneChanged(Scene oldScene, Scene newScene)
        {
            StableSince = DiagnosticClock.Now; DiagnosticContext.Scene = newScene.name;
            RpcMonitor.ResetEpoch();NetworkAbuseDetector.ResetEpoch();
            DiagnosticContext.RoomEpoch++; DiagnosticContext.Valid = false;
        }
        internal static bool Insert(ref PlayerLoopSystem node)
        {
            if (node.type == typeof(Initialization))
            {
                var children = (node.subSystemList ?? new PlayerLoopSystem[0]).Where(x => x.type != typeof(WhySoLaggyFrameBoundary)).ToList();
                children.Insert(0, new PlayerLoopSystem { type = typeof(WhySoLaggyFrameBoundary), updateDelegate = Boundary });
                node.subSystemList = children.ToArray(); return true;
            }
            if (node.subSystemList == null) return false;
            for (int i = 0; i < node.subSystemList.Length; i++) if (Insert(ref node.subSystemList[i])) return true;
            return false;
        }
        private static bool Contains(PlayerLoopSystem node) => node.type == typeof(WhySoLaggyFrameBoundary) || (node.subSystemList?.Any(Contains) ?? false);
        private static void Remove(ref PlayerLoopSystem node)
        {
            if (node.subSystemList == null) return;
            node.subSystemList = node.subSystemList.Where(x => x.type != typeof(WhySoLaggyFrameBoundary)).ToArray();
            for (int i = 0; i < node.subSystemList.Length; i++) Remove(ref node.subSystemList[i]);
        }
        private static void Install()
        {
            try { var loop = PlayerLoop.GetCurrentPlayerLoop(); if (Insert(ref loop)) PlayerLoop.SetPlayerLoop(loop); }
            catch { DiagnosticContext.Valid = false; }
        }
        internal static void Check()
        {
            if (!active || DiagnosticClock.Now - lastCheck < 2) return;
            lastCheck = DiagnosticClock.Now;
            try { if (!Contains(PlayerLoop.GetCurrentPlayerLoop())) { DiagnosticContext.Valid = false; StableSince=DiagnosticClock.Now;started = 0; Install(); } }
            catch { DiagnosticContext.Valid = false; }
        }
        private static void Boundary()
        {
            using(DiagnosticPhases.Measure(DiagnosticPhases.Stage.FrameBoundary))
            {
                if (!active) return;
                long now = Stopwatch.GetTimestamp();
                try
                {
                    if (started != 0)
                    {
                        var frame = TimingLedger.Seal(DiagnosticContext.Frame, started, now);
                        frame.State = DiagnosticContext.State; frame.Scene = DiagnosticContext.Scene; frame.Epoch = frameEpoch;
                        frame.Complete &= DiagnosticContext.Valid;
                        MemoryMetrics.ReadFrame();
                        Completed?.Invoke(frame);
                    }
                    string scene = SceneManager.GetActiveScene().name;
                    bool isLoading = loading != null && (bool)loading.GetValue(null, null);
                    string state = !Application.isFocused ? "Unfocused" : Transition||PluginProfiler.Session.Busy||PatchProfiler.Session.Busy ? "Transition" : isLoading ? "Loading" :
                        scene.IndexOf("Island", StringComparison.OrdinalIgnoreCase) >= 0 || scene.IndexOf("Level_", StringComparison.OrdinalIgnoreCase) >= 0 ? "Gameplay" :
                        scene.IndexOf("Airport", StringComparison.OrdinalIgnoreCase) >= 0 ? "Airport" : "Menu";
                    if (state != DiagnosticContext.State || scene != DiagnosticContext.Scene) StableSince = DiagnosticClock.Now;
                    DiagnosticContext.State = state; DiagnosticContext.Scene = scene; DiagnosticContext.Frame++;
                    frameEpoch=DiagnosticContext.RoomEpoch;DiagnosticContext.Valid = true;
                }
                catch { DiagnosticContext.Valid = false; }
                finally { started = now; TimingLedger.Begin(DiagnosticContext.Frame);SelfMetrics.Add("FrameBoundary",DiagnosticClock.Milliseconds(Stopwatch.GetTimestamp()-now)); }
            }
        }
        internal static void Shutdown()
        {
            active = false; SceneManager.activeSceneChanged -= OnSceneChanged;
            try { var loop = PlayerLoop.GetCurrentPlayerLoop(); Remove(ref loop); PlayerLoop.SetPlayerLoop(loop); } catch { }
            Completed = null; started = 0; MemoryMetrics.Shutdown(); TimingLedger.Clear();
        }
    }
}
