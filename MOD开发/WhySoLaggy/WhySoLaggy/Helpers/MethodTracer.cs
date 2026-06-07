using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using HarmonyLib;

namespace WhySoLaggy
{
    /// <summary>
    /// 可配置调用栈追踪器（1.0.3 新增）。
    /// 通过 Harmony Prefix 挂在配置指定的方法上，每次触发时抓 Environment.StackTrace，
    /// 过滤 UnityEngine.*/HarmonyLib.* 帧，限流后写入 StructuredLogger(EventType.MethodTrace)。
    /// 仅按需启用，关闭时零开销。
    /// </summary>
    internal static class MethodTracer
    {
        // ── 配置 ──
        public static string TraceMethodNames = "";   // 逗号分隔，格式如 "Player.Update,ColdComponent.Apply"
        public static int TraceMaxDepth = 5;
        public static int TraceRateLimit = 100;       // 同一方法每秒最多记录次数

        // ── 运行时 ──
        // 使用静态字段（Harmony 回调可能来自非主线程但本 MOD 追踪目标通常在主线程；用 lock 保证一致）
        private static readonly object _rateLock = new object();
        private static readonly Dictionary<string, int> _counter = new Dictionary<string, int>(StringComparer.Ordinal);
        private static readonly Dictionary<string, long> _windowStartTicks = new Dictionary<string, long>(StringComparer.Ordinal);
        private static readonly HashSet<string> _overflowWarnedInWindow = new HashSet<string>(StringComparer.Ordinal);
        private static readonly long _ticksPerSecond = TimeSpan.TicksPerSecond;
        private static bool _inited;
        private static int _hooked;

        public static void Initialize(Harmony harmony)
        {
            if (_inited) return;
            _inited = true;

            if (string.IsNullOrWhiteSpace(TraceMethodNames)) return;

            var wanted = new HashSet<string>(StringComparer.Ordinal);
            foreach (var part in TraceMethodNames.Split(','))
            {
                var s = part.Trim();
                if (!string.IsNullOrEmpty(s)) wanted.Add(s);
            }
            if (wanted.Count == 0) return;

            var prefix = new HarmonyMethod(typeof(MethodTracer), nameof(OnPrefix))
            {
                priority = Priority.First,   // Harmony 最大优先级
            };

            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types = null;
                try { types = asm.GetTypes(); }
                catch (Exception ex)
                {
                    WhySoLaggyPlugin.Log?.LogWarning($"[WHY_LAG] MethodTracer GetTypes failed in asm={asm.GetName().Name}: {ex.Message}");
                    continue;
                }
                foreach (var t in types)
                {
                    string typeName = t.Name;
                    foreach (var target in wanted)
                    {
                        int dot = target.IndexOf('.');
                        if (dot <= 0) continue;
                        string wantType = target.Substring(0, dot);
                        string wantMethod = target.Substring(dot + 1);
                        if (typeName != wantType) continue;

                        MethodInfo mi = null;
                        try
                        {
                            mi = t.GetMethod(wantMethod,
                                BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                        }
                        catch { }
                        if (mi == null) continue;

                        try
                        {
                            harmony.Patch(mi, prefix: prefix);
                            _hooked++;
                            AbuseLogger.Write($"[METHOD_TRACE] Hooked {target}");
                        }
                        catch (Exception ex)
                        {
                            WhySoLaggyPlugin.Log?.LogWarning($"[WHY_LAG] MethodTracer Patch failed on {target}: {ex.Message}");
                        }
                    }
                }
            }

            AbuseLogger.Write($"[METHOD_TRACE] Initialized (hooked: {_hooked}, maxDepth: {TraceMaxDepth}, rateLimit: {TraceRateLimit}/s)");
        }

        // Harmony Prefix 回调（参数名固定 __originalMethod 才能被 Harmony 注入）
        public static void OnPrefix(MethodBase __originalMethod)
        {
            if (__originalMethod == null) return;

            string key = (__originalMethod.DeclaringType?.Name ?? "?") + "." + __originalMethod.Name;

            if (!CheckRateLimit(key)) return;

            string stack;
            try { stack = Environment.StackTrace; }
            catch { return; }

            string filtered = FilterStack(stack, TraceMaxDepth);
            string caller = ExtractFirstFrame(filtered);

            try
            {
                StructuredLogger.WriteEvent(new StructuredEvent
                {
                    Timestamp = StructuredLogger.NowStamp(),
                    FrameNumber = UnityEngine.Time.frameCount,
                    Type = EventType.MethodTrace,
                    Fields = new Dictionary<string, object>
                    {
                        { "TargetMethod", key },
                        { "TraceStack", Truncate(filtered, 1200) },
                        { "TraceCaller", caller ?? "" },
                    },
                });
            }
            catch { }
        }

        private static bool CheckRateLimit(string key)
        {
            lock (_rateLock)
            {
                long nowTicks = DateTime.UtcNow.Ticks;
                _windowStartTicks.TryGetValue(key, out long start);
                if (start == 0 || nowTicks - start >= _ticksPerSecond)
                {
                    _windowStartTicks[key] = nowTicks;
                    _counter[key] = 1;
                    _overflowWarnedInWindow.Remove(key);
                    return true;
                }
                _counter.TryGetValue(key, out int c);
                c++;
                _counter[key] = c;
                if (c > TraceRateLimit)
                {
                    if (_overflowWarnedInWindow.Add(key))
                    {
                        WhySoLaggyPlugin.Log?.LogWarning($"[WHY_LAG] MethodTracer rate limit exceeded for {key} (>{TraceRateLimit}/s); suppressing additional traces this second");
                    }
                    return false;
                }
                return true;
            }
        }

        private static string FilterStack(string stack, int maxDepth)
        {
            if (string.IsNullOrEmpty(stack)) return "";
            var lines = stack.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var sb = new StringBuilder(256);
            int kept = 0;
            foreach (var raw in lines)
            {
                var line = raw.TrimStart();
                if (!line.StartsWith("at ", StringComparison.Ordinal)) continue;
                // 过滤 Unity 引擎/Harmony 内部帧
                if (line.IndexOf("UnityEngine.", StringComparison.Ordinal) >= 0) continue;
                if (line.IndexOf("HarmonyLib.", StringComparison.Ordinal) >= 0) continue;
                if (line.IndexOf("MonoMod.", StringComparison.Ordinal) >= 0) continue;
                if (line.IndexOf("System.Environment.", StringComparison.Ordinal) >= 0) continue;
                if (line.IndexOf("WhySoLaggy.MethodTracer", StringComparison.Ordinal) >= 0) continue;

                if (kept > 0) sb.Append(" | ");
                sb.Append(line);
                kept++;
                if (kept >= maxDepth) break;
            }
            return sb.ToString();
        }

        private static string ExtractFirstFrame(string filtered)
        {
            if (string.IsNullOrEmpty(filtered)) return null;
            int pipe = filtered.IndexOf('|');
            return pipe > 0 ? filtered.Substring(0, pipe).Trim() : filtered;
        }

        private static string Truncate(string s, int max)
        {
            if (string.IsNullOrEmpty(s) || s.Length <= max) return s;
            return s.Substring(0, max) + "...";
        }
    }
}
