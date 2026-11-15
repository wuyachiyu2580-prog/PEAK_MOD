using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using HarmonyLib;

namespace WhySoLaggy
{
    /// <summary>
    /// Harmony 补丁方法性能分析器：测量所有被 Harmony 补丁的游戏方法的耗时，
    /// 通过 Patches.Owners 反查归属 MOD。
    /// </summary>
    internal static class PatchProfiler
    {
        // ── 配置 ──
        public static volatile bool Enabled = true;
        public static int TopMethodCount = 10;

        /// <summary>1.0.3：忽略平均耗时 &lt; MinReportMs 的方法（每 10 次采一次），降低自身开销。</summary>
        public static float MinReportMs = 0.1f;

        /// <summary>1.0.3：要跳过 Hook 的方法全名集合（来自配置 IgnorePatchMethods）。</summary>
        public static readonly HashSet<string> IgnoreMethods = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>自身 Harmony ID，用于跳过自己的补丁。</summary>
        public static string OwnHarmonyId;

        // ── 数据 ──
        private static readonly Dictionary<string, MethodTimingData> _timings
            = new Dictionary<string, MethodTimingData>();

        // 方法标识 → 归属 MOD 列表
        private static readonly Dictionary<string, string> _ownerMap
            = new Dictionary<string, string>();

        // 当前帧即时计时
        private static readonly Dictionary<string, FrameMethodData> _frameTimers
            = new Dictionary<string, FrameMethodData>();

        // 1.0.3：低耗时方法节流计数器（每 10 次 Record 实际只记 1 次）
        private static readonly Dictionary<string, int> _skipCounter
            = new Dictionary<string, int>();

        private static volatile bool _initialized;
        private static int _patchedCount;
        private static readonly object Sync = new object();
        private static readonly Dictionary<MethodBase, string> TargetKeys = new Dictionary<MethodBase, string>();
        private static readonly HashSet<MethodBase> Installed = new HashSet<MethodBase>();
        private static Harmony _timingHarmony;
        private static int _generation;
        private static int _recordFaults;
        internal static bool IsActive => _initialized && Enabled;
        internal static bool HasPendingCleanup => Installed.Count != 0;
        internal static string TimingHarmonyId => _timingHarmony?.Id;

        public struct TimingState
        {
            public long Started;
            public int Generation;
        }

        private sealed class TargetGroup
        {
            public MethodBase Target;
            public readonly HashSet<MethodBase> Aliases = new HashSet<MethodBase>();
            public readonly HashSet<string> Owners = new HashSet<string>(StringComparer.Ordinal);
            public bool Ignored;
        }

        /// <summary>
        /// 扫描所有被 Harmony 补丁的方法并注入计时包裹。
        /// </summary>
        public static void Initialize(Harmony harmony)
        {
            if (_initialized || !Enabled || harmony == null) return;
            if (Installed.Count != 0 && !Stop()) return;
            string rootId = OwnHarmonyId ?? harmony.Id;
            _timingHarmony = new Harmony(rootId + ".PatchProfiler");
            var groups = new Dictionary<string, TargetGroup>(StringComparer.Ordinal);
            MethodBase[] methods = Harmony.GetAllPatchedMethods().ToArray();
            var sb = new StringBuilder("[WHY_LAG] -- PatchProfiler: scanning patched methods --\n");
            int unsupported = 0, duplicate = 0, skipped = 0, ignored = 0, failed = 0;
            foreach (MethodBase source in methods)
            {
                try
                {
                    if (!ProfilerTargetResolver.TryResolve(source, out MethodBase target, out string reason))
                    { unsupported++; sb.AppendLine("[WHY_LAG]   Skipped unsupported " + source + ": " + reason); continue; }
                    string identity = ProfilerTargetResolver.Identity(target);
                    if (!groups.TryGetValue(identity, out TargetGroup group))
                        groups[identity] = group = new TargetGroup { Target = target };
                    else duplicate++;
                    group.Aliases.Add(source);
                    group.Aliases.Add(target);
                    group.Ignored |= MethodKey.IsIgnored(source, IgnoreMethods) || MethodKey.IsIgnored(target, IgnoreMethods);
                    Patches info = Harmony.GetPatchInfo(source);
                    if (info != null)
                        foreach (string owner in info.Owners)
                            if (owner != rootId && owner != _timingHarmony.Id) group.Owners.Add(owner);
                }
                catch (Exception ex) { unsupported++; sb.AppendLine("[WHY_LAG]   Skipped target: " + ex.Message); }
            }
            Interlocked.Increment(ref _generation);
            _patchedCount = 0;
            foreach (TargetGroup group in groups.Values)
            {
                if (group.Ignored) { ignored++; continue; }
                if (group.Owners.Count == 0) { skipped++; continue; }
                string key = MethodKey.Canonical(group.Target);
                if (group.Target is MethodInfo generic && generic.IsGenericMethod)
                    key += " [" + string.Join(",", generic.GetGenericArguments().Select(t => t.AssemblyQualifiedName)) + "]";
                lock (Sync)
                {
                    _ownerMap[key] = string.Join(", ", group.Owners.OrderBy(x => x, StringComparer.Ordinal));
                    foreach (MethodBase alias in group.Aliases) TargetKeys[alias] = key;
                }
                Installed.Add(group.Target); // Keep cleanup ownership even if installation only partially succeeds.
                try
                {
                    _timingHarmony.Patch(group.Target,
                        prefix: new HarmonyMethod(typeof(PatchProfiler), nameof(TimingPrefix)) { priority = Priority.First },
                        postfix: new HarmonyMethod(typeof(PatchProfiler), nameof(TimingPostfix)) { priority = Priority.Last });
                    _patchedCount++;
                }
                catch (Exception ex)
                {
                    failed++;
                    sb.AppendLine("[WHY_LAG]   Hook failed " + key + ": " + ex.Message);
                    Warn("Hook failed " + key + ": " + ex.Message);
                    try
                    {
                        _timingHarmony.Unpatch(group.Target, HarmonyPatchType.All, _timingHarmony.Id);
                        Installed.Remove(group.Target);
                        lock (Sync)
                        {
                            foreach (MethodBase alias in group.Aliases) TargetKeys.Remove(alias);
                            _ownerMap.Remove(key);
                        }
                    }
                    catch (Exception cleanup)
                    {
                        Warn("Partial hook cleanup failed; profiler stopped: " + cleanup.Message);
                        Stop();
                        return;
                    }
                }
            }
            _initialized = true;
            sb.AppendLine($"[WHY_LAG] Found: {methods.Length} | Unique: {groups.Count} | Merged: {duplicate} | Hooked: {_patchedCount} | Self-only: {skipped} | Ignored: {ignored} | Unsupported: {unsupported} | Failed: {failed}");
            LagLogger.Info(sb.ToString());
        }

        // ── Harmony 注入方法 ──

        public static void TimingPrefix(out TimingState __state)
        {
            __state = default;
            int generation = Volatile.Read(ref _generation);
            if (!IsActive) return;
            __state.Generation = generation;
            __state.Started = Stopwatch.GetTimestamp();
        }

        public static void TimingPostfix(MethodBase __originalMethod, TimingState __state)
        {
            if (__state.Started == 0 || !IsActive) return;
            try
            {
                long elapsed = Stopwatch.GetTimestamp() - __state.Started;
                lock (Sync)
                {
                    if (!IsActive || __state.Generation != _generation) return;
                    if (__originalMethod != null && TargetKeys.TryGetValue(__originalMethod, out string key))
                        RecordUnsafe(key, elapsed);
                    else Interlocked.Increment(ref _recordFaults);
                }
            }
            catch { Interlocked.Increment(ref _recordFaults); } // Never throw a diagnostic failure into the observed MOD.
        }

        // ── 数据记录 ──

        private static void Record(string methodKey, long elapsedTicks)
        {
            lock (Sync) RecordUnsafe(methodKey, elapsedTicks);
        }

        private static void RecordUnsafe(string methodKey, long elapsedTicks)
        {
            // 累计统计
            if (!_timings.TryGetValue(methodKey, out var data))
            {
                data = new MethodTimingData();
                _timings[methodKey] = data;
            }

            data.ExactCallCount++;

            // 帧级数据始终精确记录，采样只影响周期累计估算。
            if (!_frameTimers.TryGetValue(methodKey, out var fd))
            {
                fd = new FrameMethodData();
                _frameTimers[methodKey] = fd;
            }
            fd.Ticks += elapsedTicks;
            fd.Calls++;

            bool takeSample = true;
            if (MinReportMs > 0f && data.SampledCallCount > 20)
            {
                double tickFreq = Stopwatch.Frequency / 1000.0;
                double avgMs = data.SampledTicks / (double)data.SampledCallCount / tickFreq;
                if (avgMs < MinReportMs)
                {
                    _skipCounter.TryGetValue(methodKey, out int sc);
                    sc++;
                    _skipCounter[methodKey] = sc;
                    takeSample = sc % 10 == 0;
                }
            }

            if (takeSample)
            {
                data.SampledTicks += elapsedTicks;
                data.SampledCallCount++;
            }
        }

        // ── 报告 ──

        /// <summary>
        /// 输出周期性汇总报告（Top N 慢方法）。
        /// </summary>
        public static void WriteReport(int totalFrames)
        {
            int faults = Interlocked.Exchange(ref _recordFaults, 0);
            if (faults > 0) Warn("Skipped " + faults + " timing records (missing target or recording failure).");
            if (!IsActive) return;
            List<TimingSample> samples = TakeSnapshot(true);
            if (samples.Count == 0) return;

            double tickFreq = Stopwatch.Frequency / 1000.0;
            samples.Sort((a,b) => b.EstimatedTicks.CompareTo(a.EstimatedTicks));
            var report = new StringBuilder("[WHY_LAG] -- Top Slow Patched Methods (per-frame avg) --\n");

            int shown = 0;
            foreach (TimingSample sample in samples)
            {
                if (shown >= TopMethodCount) break;
                double totalMs = sample.EstimatedTicks / tickFreq;
                double avgMs = totalFrames > 0 ? totalMs / totalFrames : totalMs;
                double avgCalls = totalFrames > 0 ? sample.Calls / (double)totalFrames : sample.Calls;

                string warn = avgMs > 2.0 ? " !!!" : avgMs > 0.5 ? " !" : "";
                report.AppendLine($"[WHY_LAG]   {sample.Key}: {avgMs:F2}ms x{avgCalls:F1} [{sample.Owners}]{warn}");

                // 1.0.3：结构化事件
                try
                {
                    StructuredLogger.WriteEvent(new StructuredEvent
                    {
                        Timestamp = StructuredLogger.NowStamp(),
                        FrameNumber = UnityEngine.Time.frameCount,
                        Type = EventType.PatchTiming,
                        Fields = new Dictionary<string, object>
                        {
                            { "Name", sample.Key },
                            { "AvgMs", Math.Round(avgMs, 3) },
                            { "TotalMs", Math.Round(totalMs, 3) },
                            { "CallCount", sample.Calls },
                            { "Owner", sample.Owners },
                        },
                    });
                }
                catch (Exception ex)
                {
                    WhySoLaggyPlugin.Log?.LogWarning($"[WHY_LAG] PatchProfiler WriteEvent failed: {ex.Message}");
                }

                shown++;
            }

            LagLogger.Write(report.ToString().TrimEnd());
        }

        /// <summary>
        /// 输出当前帧尖峰时的方法耗时明细。
        /// </summary>
        public static void WriteSpikeDetail()
        {
            if (!IsActive) return;

            double tickFreq = Stopwatch.Frequency / 1000.0;
            List<TimingSample> samples = TakeSnapshot(false);
            samples.Sort((a,b) => b.FrameTicks.CompareTo(a.FrameTicks));
            var report = new StringBuilder("[WHY_LAG]   Methods: ");

            int shown = 0;
            bool anyOver = false;
            foreach (TimingSample sample in samples)
            {
                if (shown >= 5) break;
                double ms = sample.FrameTicks / tickFreq;
                if (ms < 0.5) continue;
                anyOver = true;
                report.Append($"{sample.Key}={ms:F1}ms x{sample.FrameCalls} [{sample.Owners}] | ");
                shown++;
            }

            if (anyOver)
                LagLogger.Write(report.ToString());
        }

        /// <summary>
        /// 每帧末尾重置即时计时器。
        /// </summary>
        public static void ResetFrameTimers()
        {
            lock (Sync)
            {
                foreach (var fd in _frameTimers.Values)
                {
                    fd.Ticks = 0;
                    fd.Calls = 0;
                }
            }
        }

        // ── 辅助 ──

        internal static double EstimateTotalTicks(long sampledTicks, int sampledCalls, int exactCalls)
        {
            if (sampledCalls <= 0 || exactCalls <= 0) return 0d;
            return sampledTicks / (double)sampledCalls * exactCalls;
        }

        private static double EstimateTotalTicks(MethodTimingData data)
        {
            return EstimateTotalTicks(data.SampledTicks, data.SampledCallCount, data.ExactCallCount);
        }

        public static void Shutdown()
        {
            Stop();
            IgnoreMethods.Clear();
        }

        // Called on the main thread. Never hold Sync while Harmony rebuilds a target.
        internal static bool Stop()
        {
            lock (Sync)
            {
                _initialized = false;
                Interlocked.Increment(ref _generation);
            }
            foreach (MethodBase target in Installed.ToArray())
            {
                try
                {
                    _timingHarmony.Unpatch(target, HarmonyPatchType.All, _timingHarmony.Id);
                    Installed.Remove(target);
                }
                catch (Exception ex) { Warn("Timing cleanup failed; retained for retry: " + target + ": " + ex.Message); }
            }
            if (Installed.Count != 0) return false;
            lock (Sync)
            {
                TargetKeys.Clear();
                _timings.Clear();
                _ownerMap.Clear();
                _frameTimers.Clear();
                _skipCounter.Clear();
                _patchedCount = 0;
                _recordFaults = 0;
            }
            _timingHarmony = null;
            return true;
        }

        private static void Warn(string message)
        {
            try { WhySoLaggyPlugin.Log?.LogWarning("[WHY_LAG] PatchProfiler " + message); }
            catch { }
            LagLogger.Info("[WHY_LAG] PatchProfiler " + message);
        }

        internal sealed class TimingSample
        {
            public string Key, Owners;
            public int Calls, FrameCalls;
            public long FrameTicks;
            public double EstimatedTicks;
        }

        internal static List<TimingSample> TakeSnapshot(bool reset)
        {
            var samples = new List<TimingSample>();
            lock (Sync)
            {
                foreach (var pair in _timings)
                {
                    MethodTimingData data = pair.Value;
                    _frameTimers.TryGetValue(pair.Key, out FrameMethodData frame);
                    if (data.ExactCallCount > 0 || (!reset && frame?.Calls > 0))
                        samples.Add(new TimingSample
                        {
                            Key = pair.Key,
                            Owners = _ownerMap.TryGetValue(pair.Key, out string owners) ? owners : "?",
                            Calls = data.ExactCallCount,
                            EstimatedTicks = EstimateTotalTicks(data),
                            FrameTicks = frame?.Ticks ?? 0,
                            FrameCalls = frame?.Calls ?? 0
                        });
                    if (reset) { data.SampledTicks = 0; data.SampledCallCount = 0; data.ExactCallCount = 0; }
                }
                if (reset) _skipCounter.Clear();
            }
            return samples;
        }

        private class MethodTimingData
        {
            public long SampledTicks;
            public int SampledCallCount;
            public int ExactCallCount;
        }

        private class FrameMethodData
        {
            public long Ticks;
            public int Calls;
        }
    }
}
