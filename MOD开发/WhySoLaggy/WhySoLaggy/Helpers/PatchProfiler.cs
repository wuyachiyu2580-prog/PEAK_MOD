using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
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
        public static bool Enabled = true;
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

        private static bool _initialized;
        private static int _patchedCount;

        // 1.0.3：复用的临时缓冲
        private static readonly StringBuilder _reportSb = new StringBuilder(1024);
        private static readonly List<KeyValuePair<string, MethodTimingData>> _reportSorted
            = new List<KeyValuePair<string, MethodTimingData>>(64);
        private static readonly List<KeyValuePair<string, FrameMethodData>> _spikeSorted
            = new List<KeyValuePair<string, FrameMethodData>>(32);

        /// <summary>
        /// 扫描所有被 Harmony 补丁的方法并注入计时包裹。
        /// </summary>
        public static void Initialize(Harmony harmony)
        {
            if (_initialized || !Enabled) return;
            _initialized = true;

            var sb = new StringBuilder();
            sb.AppendLine("[WHY_LAG] -- PatchProfiler: scanning patched methods --");

            var allPatched = Harmony.GetAllPatchedMethods().ToList();
            sb.AppendLine($"[WHY_LAG]   Found {allPatched.Count} patched methods in total");

            int skipped = 0;
            int failed = 0;
            int ignoredByConfig = 0;

            foreach (MethodBase method in allPatched)
            {
                if (method == null) continue;

                string methodKey = GetMethodKey(method);

                // 1.0.3：按配置黑名单跳过
                if (IgnoreMethods.Count > 0 && IgnoreMethods.Contains(methodKey))
                {
                    ignoredByConfig++;
                    continue;
                }

                try
                {
                    Patches patchInfo = Harmony.GetPatchInfo(method);
                    if (patchInfo == null) continue;

                    // 获取所有 owner
                    var owners = patchInfo.Owners?.ToList() ?? new List<string>();

                    // 跳过只有自身补丁的方法
                    if (owners.Count == 0) continue;
                    var externalOwners = owners.Where(o => o != OwnHarmonyId).ToList();
                    if (externalOwners.Count == 0)
                    {
                        skipped++;
                        continue;
                    }

                    _ownerMap[methodKey] = string.Join(", ", externalOwners);

                    // 注入计时
                    harmony.Patch(method,
                        prefix: new HarmonyMethod(typeof(PatchProfiler), nameof(TimingPrefix))
                            { priority = Priority.First },
                        postfix: new HarmonyMethod(typeof(PatchProfiler), nameof(TimingPostfix))
                            { priority = Priority.Last });

                    _patchedCount++;
                }
                catch (Exception ex)
                {
                    failed++;
                    string name = method.DeclaringType?.Name + "." + method.Name;
                    // 1.0.3：异常不再吞错
                    WhySoLaggyPlugin.Log?.LogWarning($"[WHY_LAG] PatchProfiler hook FAILED {name}: {ex.Message}");
                    sb.AppendLine($"[WHY_LAG]   FAILED to hook {name}: {ex.Message}");
                }
            }

            sb.AppendLine($"[WHY_LAG]   Hooked: {_patchedCount} | Skipped(self-only): {skipped} | IgnoredByConfig: {ignoredByConfig} | Failed: {failed}");
            sb.Append($"[WHY_LAG] PatchProfiler initialized (MinReportMs={MinReportMs:F2})");
            LagLogger.Info(sb.ToString());
        }

        // ── Harmony 注入方法 ──

        public static void TimingPrefix(MethodBase __originalMethod, out long __state)
        {
            __state = Stopwatch.GetTimestamp();
        }

        public static void TimingPostfix(MethodBase __originalMethod, long __state)
        {
            long elapsed = Stopwatch.GetTimestamp() - __state;
            // 1.0.3：__originalMethod 判空，防止极少数 Harmony 内部路径拿不到
            string key = __originalMethod != null ? GetMethodKey(__originalMethod) : "(unknown)";
            Record(key, elapsed);
        }

        // ── 数据记录 ──

        private static void Record(string methodKey, long elapsedTicks)
        {
            // 累计统计
            if (!_timings.TryGetValue(methodKey, out var data))
            {
                data = new MethodTimingData();
                _timings[methodKey] = data;
            }

            // 1.0.3：低耗时方法节流（平均低于 MinReportMs 则每 10 次才实际记一次）
            if (MinReportMs > 0f && data.CallCount > 20)
            {
                double tickFreq = Stopwatch.Frequency / 1000.0;
                double avgMs = data.TotalTicks / (double)data.CallCount / tickFreq;
                if (avgMs < MinReportMs)
                {
                    _skipCounter.TryGetValue(methodKey, out int sc);
                    sc++;
                    if (sc % 10 != 0)
                    {
                        _skipCounter[methodKey] = sc;
                        return;
                    }
                    _skipCounter[methodKey] = sc;
                }
            }

            data.TotalTicks += elapsedTicks;
            data.CallCount++;

            // 当前帧即时
            if (!_frameTimers.TryGetValue(methodKey, out var fd))
            {
                fd = new FrameMethodData();
                _frameTimers[methodKey] = fd;
            }
            fd.Ticks += elapsedTicks;
            fd.Calls++;
        }

        // ── 报告 ──

        /// <summary>
        /// 输出周期性汇总报告（Top N 慢方法）。
        /// </summary>
        public static void WriteReport(int totalFrames)
        {
            if (!_initialized || _timings.Count == 0) return;

            double tickFreq = Stopwatch.Frequency / 1000.0;

            _reportSorted.Clear();
            foreach (var kv in _timings)
                if (kv.Value.CallCount > 0) _reportSorted.Add(kv);
            _reportSorted.Sort((a, b) => b.Value.TotalTicks.CompareTo(a.Value.TotalTicks));

            _reportSb.Clear();
            _reportSb.AppendLine("[WHY_LAG] -- Top Slow Patched Methods (per-frame avg) --");

            int shown = 0;
            foreach (var kv in _reportSorted)
            {
                if (shown >= TopMethodCount) break;
                double totalMs = kv.Value.TotalTicks / tickFreq;
                double avgMs = totalFrames > 0 ? totalMs / totalFrames : totalMs;
                int avgCalls = totalFrames > 0 ? kv.Value.CallCount / totalFrames : kv.Value.CallCount;
                string owners = _ownerMap.TryGetValue(kv.Key, out var o) ? o : "?";

                string warn = avgMs > 2.0 ? " !!!" : avgMs > 0.5 ? " !" : "";
                _reportSb.AppendLine($"[WHY_LAG]   {kv.Key}: {avgMs:F2}ms x{avgCalls} [{owners}]{warn}");

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
                            { "Name", kv.Key },
                            { "AvgMs", Math.Round(avgMs, 3) },
                            { "TotalMs", Math.Round(totalMs, 3) },
                            { "CallCount", kv.Value.CallCount },
                            { "Owner", owners },
                        },
                    });
                }
                catch (Exception ex)
                {
                    WhySoLaggyPlugin.Log?.LogWarning($"[WHY_LAG] PatchProfiler WriteEvent failed: {ex.Message}");
                }

                shown++;
            }

            LagLogger.Write(_reportSb.ToString().TrimEnd());

            // 重置
            foreach (var data in _timings.Values)
            {
                data.TotalTicks = 0;
                data.CallCount = 0;
            }
            _skipCounter.Clear();
        }

        /// <summary>
        /// 输出当前帧尖峰时的方法耗时明细。
        /// </summary>
        public static void WriteSpikeDetail()
        {
            if (!_initialized || _frameTimers.Count == 0) return;

            double tickFreq = Stopwatch.Frequency / 1000.0;

            _spikeSorted.Clear();
            foreach (var kv in _frameTimers)
                if (kv.Value.Ticks > 0) _spikeSorted.Add(kv);
            _spikeSorted.Sort((a, b) => b.Value.Ticks.CompareTo(a.Value.Ticks));

            _reportSb.Clear();
            _reportSb.Append("[WHY_LAG]   Methods: ");

            int shown = 0;
            bool anyOver = false;
            foreach (var kv in _spikeSorted)
            {
                if (shown >= 5) break;
                double ms = kv.Value.Ticks / tickFreq;
                if (ms < 0.5) continue;
                anyOver = true;
                string owners = _ownerMap.TryGetValue(kv.Key, out var o) ? o : "?";
                _reportSb.Append($"{kv.Key}={ms:F1}ms x{kv.Value.Calls} [{owners}] | ");
                shown++;
            }

            if (anyOver)
                LagLogger.Write(_reportSb.ToString());
        }

        /// <summary>
        /// 每帧末尾重置即时计时器。
        /// </summary>
        public static void ResetFrameTimers()
        {
            foreach (var fd in _frameTimers.Values)
            {
                fd.Ticks = 0;
                fd.Calls = 0;
            }
        }

        // ── 辅助 ──

        private static string GetMethodKey(MethodBase method)
        {
            if (method == null) return "(null)";
            string typeName = method.DeclaringType?.Name ?? "?";
            return $"{typeName}.{method.Name}";
        }

        private class MethodTimingData
        {
            public long TotalTicks;
            public int CallCount;
        }

        private class FrameMethodData
        {
            public long Ticks;
            public int Calls;
        }
    }
}
