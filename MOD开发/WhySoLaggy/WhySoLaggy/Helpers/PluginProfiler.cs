using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using BepInEx;
using BepInEx.Bootstrap;
using HarmonyLib;
using UnityEngine;

namespace WhySoLaggy
{
    /// <summary>
    /// 插件性能分析器：测量每个 BepInEx 插件的 Update/LateUpdate/FixedUpdate 回调耗时。
    /// 通过 Harmony 动态注入计时前缀/后缀，按插件归属聚合统计。
    /// </summary>
    internal static class PluginProfiler
    {
        // ── 配置 ──
        public static bool Enabled = true;

        /// <summary>1.0.3：跳过扫描的插件 GUID 列表（来自配置 IgnorePluginGuids）。</summary>
        public static readonly HashSet<string> IgnoreGuids = new HashSet<string>(StringComparer.Ordinal);

        // ── 数据 ──
        // key = 插件类型全名, value = 累计数据
        private static readonly Dictionary<string, PluginTimingData> _timings
            = new Dictionary<string, PluginTimingData>();

        // 类型全名 → 友好显示名 (插件 GUID 或类名)
        private static readonly Dictionary<string, string> _displayNames
            = new Dictionary<string, string>();

        // 每帧线程局部计时栈 (key = 类型全名)
        // 由于 Unity 主线程单线程，使用静态字典即可
        private static readonly Dictionary<string, long> _frameTimers
            = new Dictionary<string, long>();

        private static bool _initialized;
        private static int _patchedCount;

        // 1.0.3：复用缓冲
        private static readonly StringBuilder _reportSb = new StringBuilder(1024);
        private static readonly List<KeyValuePair<string, PluginTimingData>> _reportSorted
            = new List<KeyValuePair<string, PluginTimingData>>(32);
        private static readonly List<KeyValuePair<string, long>> _spikeSorted
            = new List<KeyValuePair<string, long>>(32);

        /// <summary>
        /// 扫描所有已加载插件并注入计时补丁。
        /// </summary>
        public static void Initialize(Harmony harmony)
        {
            if (_initialized || !Enabled) return;
            _initialized = true;

            var sb = new StringBuilder();
            sb.AppendLine("[WHY_LAG] -- PluginProfiler: scanning plugins --");

            foreach (var kv in Chainloader.PluginInfos)
            {
                var info = kv.Value;
                if (info.Instance == null) continue;

                // 1.0.3：按 GUID 黑名单跳过
                if (IgnoreGuids.Count > 0 && IgnoreGuids.Contains(kv.Key))
                {
                    sb.AppendLine($"[WHY_LAG]   Skipped (by IgnorePluginGuids): {kv.Key}");
                    continue;
                }

                Type pluginType = info.Instance.GetType();
                string typeName = pluginType.FullName ?? pluginType.Name;
                string displayName = info.Metadata?.Name ?? kv.Key;

                // 跳过自身
                if (pluginType == typeof(WhySoLaggyPlugin)) continue;

                _displayNames[typeName] = displayName;

                // 检查并补丁 Update / LateUpdate / FixedUpdate
                string[] methodNames = { "Update", "LateUpdate", "FixedUpdate" };
                foreach (string methodName in methodNames)
                {
                    MethodInfo method = pluginType.GetMethod(methodName,
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);

                    if (method == null) continue;

                    try
                    {
                        harmony.Patch(method,
                            prefix: new HarmonyMethod(typeof(PluginProfiler), nameof(TimingPrefix))
                                { priority = Priority.First },
                            postfix: new HarmonyMethod(typeof(PluginProfiler), nameof(TimingPostfix))
                                { priority = Priority.Last });

                        _patchedCount++;
                        sb.AppendLine($"[WHY_LAG]   Hooked {displayName}.{methodName}");
                    }
                    catch (Exception ex)
                    {
                        // 1.0.3：不再吞错
                        WhySoLaggyPlugin.Log?.LogWarning($"[WHY_LAG] PluginProfiler hook FAILED {displayName}.{methodName}: {ex.Message}");
                        sb.AppendLine($"[WHY_LAG]   FAILED {displayName}.{methodName}: {ex.Message}");
                    }
                }
            }

            sb.Append($"[WHY_LAG] PluginProfiler: {_patchedCount} methods hooked across {_displayNames.Count} plugins");
            LagLogger.Info(sb.ToString());
        }

        // ── Harmony 注入方法 ──

        public static void TimingPrefix(MonoBehaviour __instance, out long __state)
        {
            __state = Stopwatch.GetTimestamp();
        }

        public static void TimingPostfix(MonoBehaviour __instance, long __state)
        {
            long elapsed = Stopwatch.GetTimestamp() - __state;
            if (__instance == null) return;
            string typeName = __instance.GetType().FullName ?? __instance.GetType().Name;
            Record(typeName, elapsed);
        }

        // ── 数据记录 ──

        private static void Record(string typeName, long elapsedTicks)
        {
            if (!_timings.TryGetValue(typeName, out var data))
            {
                data = new PluginTimingData();
                _timings[typeName] = data;
            }
            data.TotalTicks += elapsedTicks;
            data.CallCount++;

            // 记录当前帧即时耗时（用于尖峰报告）
            if (!_frameTimers.ContainsKey(typeName))
                _frameTimers[typeName] = 0;
            _frameTimers[typeName] += elapsedTicks;
        }

        // ── 报告 ──

        /// <summary>
        /// 输出周期性汇总报告。
        /// </summary>
        public static void WriteReport(int totalFrames)
        {
            if (!_initialized || _timings.Count == 0) return;

            double tickFreq = Stopwatch.Frequency / 1000.0; // ticks per ms

            _reportSorted.Clear();
            foreach (var kv in _timings) _reportSorted.Add(kv);
            _reportSorted.Sort((a, b) => b.Value.TotalTicks.CompareTo(a.Value.TotalTicks));

            _reportSb.Clear();
            _reportSb.AppendLine("[WHY_LAG] -- Plugin Update Time (per-frame avg) --");

            double totalPluginMs = 0;
            foreach (var kv in _reportSorted)
            {
                string name = _displayNames.TryGetValue(kv.Key, out var dn) ? dn : kv.Key;
                double totalMs = kv.Value.TotalTicks / tickFreq;
                double avgMs = totalFrames > 0 ? totalMs / totalFrames : totalMs;
                totalPluginMs += totalMs;

                string warn = avgMs > 1.0 ? " !!!" : avgMs > 0.5 ? " !" : "";
                _reportSb.AppendLine($"[WHY_LAG]   {name}: {avgMs:F2}ms/frame (total={totalMs:F1}ms, calls={kv.Value.CallCount}){warn}");

                // 1.0.3：结构化事件
                try
                {
                    StructuredLogger.WriteEvent(new StructuredEvent
                    {
                        Timestamp = StructuredLogger.NowStamp(),
                        FrameNumber = Time.frameCount,
                        Type = EventType.PluginTiming,
                        Fields = new Dictionary<string, object>
                        {
                            { "Name", name },
                            { "AvgMs", Math.Round(avgMs, 3) },
                            { "TotalMs", Math.Round(totalMs, 3) },
                            { "CallCount", kv.Value.CallCount },
                        },
                    });
                }
                catch (Exception ex)
                {
                    WhySoLaggyPlugin.Log?.LogWarning($"[WHY_LAG] PluginProfiler WriteEvent failed: {ex.Message}");
                }
            }

            double totalAvgMs = totalFrames > 0 ? totalPluginMs / totalFrames : totalPluginMs;
            _reportSb.Append($"[WHY_LAG]   Total MOD overhead: {totalAvgMs:F2}ms/frame");

            LagLogger.Write(_reportSb.ToString());

            // 重置
            foreach (var data in _timings.Values)
            {
                data.TotalTicks = 0;
                data.CallCount = 0;
            }
        }

        /// <summary>
        /// 输出当前帧的即时耗时（尖峰时调用）。
        /// </summary>
        public static void WriteSpikeDetail(float frameMs)
        {
            if (!_initialized || _frameTimers.Count == 0) return;

            double tickFreq = Stopwatch.Frequency / 1000.0;

            _spikeSorted.Clear();
            foreach (var kv in _frameTimers)
                if (kv.Value > 0) _spikeSorted.Add(kv);
            _spikeSorted.Sort((a, b) => b.Value.CompareTo(a.Value));

            _reportSb.Clear();
            _reportSb.Append($"[WHY_LAG] !!! SPIKE {frameMs:F0}ms ({(frameMs > 0 ? 1000f / frameMs : 0):F1} FPS) !!! Plugins: ");

            foreach (var kv in _spikeSorted)
            {
                string name = _displayNames.TryGetValue(kv.Key, out var dn) ? dn : kv.Key;
                double ms = kv.Value / tickFreq;
                if (ms < 0.1) continue;
                _reportSb.Append($"{name}={ms:F1}ms | ");
            }

            LagLogger.Write(_reportSb.ToString());

            // 1.0.3：SpikeFrame 结构化事件
            // 1.0.4：追加 Top1 插件归因（TopPluginName/TopPluginMs），与文本日志里打印的 spike 明细一致
            string topPluginName = null; double topPluginMs = 0;
            if (_spikeSorted.Count > 0)
            {
                var topKv = _spikeSorted[0];
                topPluginName = _displayNames.TryGetValue(topKv.Key, out var dn) ? dn : topKv.Key;
                topPluginMs = topKv.Value / tickFreq;
            }
            try
            {
                var spikeFields = new Dictionary<string, object>
                {
                    { "AvgFrameMs", Math.Round((double)frameMs, 2) },
                    { "SpikeThresholdMs", FpsTracker.SpikeThresholdMs },
                };
                if (!string.IsNullOrEmpty(topPluginName))
                {
                    spikeFields["TopPluginName"] = topPluginName;
                    spikeFields["TopPluginMs"] = Math.Round(topPluginMs, 2);
                }
                StructuredLogger.WriteEvent(new StructuredEvent
                {
                    Timestamp = StructuredLogger.NowStamp(),
                    FrameNumber = Time.frameCount,
                    Type = EventType.SpikeFrame,
                    Fields = spikeFields,
                });
            }
            catch (Exception ex)
            {
                WhySoLaggyPlugin.Log?.LogWarning($"[WHY_LAG] PluginProfiler SpikeEvent failed: {ex.Message}");
            }
        }

        /// <summary>
        /// 每帧末尾重置即时计时器。
        /// </summary>
        public static void ResetFrameTimers()
        {
            _frameTimers.Clear();
        }

        private class PluginTimingData
        {
            public long TotalTicks;
            public int CallCount;
        }
    }
}
