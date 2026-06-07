using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using HarmonyLib;

namespace WhySoLaggy
{
    /// <summary>
    /// 字段探针（1.0.3 新增）。
    ///
    /// 通过读取外部 JSON 规则文件，在任意方法被调用时反射快照任意字段 /参数 / 返回值，
    /// 并可选附带调用栈。规则文件路径由 Plugin 透传，缺失或解析失败均不影响其他模块。
    ///
    /// 规则文件结构（详见 examples/WhySoLaggy.fieldprobe.json）：
    /// {
    ///   "enabled": true,
    ///   "rateLimitPerRule": 60,
    ///   "maxValueLen": 128,
    ///   "includeStack": false,
    ///   "stackMaxDepth": 5,
    ///   "rules": [
    ///     {
    ///       "target": "CharacterAfflictions.AddStatus",
    ///       "fields": ["__arg0", "__instance.character.data.isInvincibleMilk", "__result"],
    ///       "rateLimit": 120,
    ///       "includeStack": true,
    ///       "note": "..."
    ///     }
    ///   ]
    /// }
    ///
    /// Hook 类型自动推断：
    ///   表达式含 __result     → Postfix
    ///   表达式含 __exception  → Finalizer
    ///   否则                   → Prefix
    /// 同一方法可同时出现在多条规则中（Prefix + Postfix 可共存）。
    /// </summary>
    internal static class FieldProbe
    {
        // ── 对外配置（由 Plugin 传入）──
        public static bool Enabled = false;
        public static string RulesFilePath = "";
        public static int DefaultRateLimit = 60;
        public static int DefaultMaxValueLen = 128;
        public static bool DefaultIncludeStack = false;
        public static int DefaultStackMaxDepth = 5;

        // ── 内部数据 ──
        private sealed class Rule
        {
            public int Id;
            public string TargetTypeName;    // "CharacterAfflictions"
            public string TargetMethodName;  // "AddStatus"
            public string TargetKey;         // "CharacterAfflictions.AddStatus"
            public string[] RawFields;
            public ExpressionEvaluator.CompiledExpr[] Compiled;
            public int RateLimit;
            public int MaxValueLen;
            public bool IncludeStack;
            public int StackMaxDepth;
            public string Note;
            public bool Enabled;

            public bool NeedsPostfix;     // 有 __result
            public bool NeedsFinalizer;   // 有 __exception
            public bool NeedsPrefix;      // 其他
        }

        private static readonly Dictionary<MethodBase, List<Rule>> _methodToRules
            = new Dictionary<MethodBase, List<Rule>>();
        private static readonly object _rateLock = new object();
        private static readonly Dictionary<int, int> _counter = new Dictionary<int, int>();
        private static readonly Dictionary<int, long> _windowStart = new Dictionary<int, long>();
        private static readonly HashSet<int> _overflowWarned = new HashSet<int>();
        private static readonly long _ticksPerSec = TimeSpan.TicksPerSecond;
        private static int _hookedRules = 0;
        private static bool _inited;

        public static void Initialize(Harmony harmony)
        {
            if (_inited) return;
            _inited = true;
            if (!Enabled || string.IsNullOrWhiteSpace(RulesFilePath))
            {
                WhySoLaggyPlugin.Log?.LogInfo("[WHY_LAG] FieldProbe disabled or rules file empty, skip");
                return;
            }
            if (!File.Exists(RulesFilePath))
            {
                WhySoLaggyPlugin.Log?.LogWarning($"[WHY_LAG] FieldProbe rules file not found: {RulesFilePath}");
                return;
            }

            string text;
            try { text = File.ReadAllText(RulesFilePath, Encoding.UTF8); }
            catch (Exception ex)
            {
                WhySoLaggyPlugin.Log?.LogWarning($"[WHY_LAG] FieldProbe read rules failed: {ex.Message}");
                return;
            }

            if (!MiniJson.TryParse(text, out object rootObj, out string err))
            {
                WhySoLaggyPlugin.Log?.LogWarning($"[WHY_LAG] FieldProbe JSON parse failed: {err}");
                return;
            }
            var root = MiniJson.AsObject(rootObj);
            if (root == null)
            {
                WhySoLaggyPlugin.Log?.LogWarning("[WHY_LAG] FieldProbe rules root is not object");
                return;
            }

            bool fileEnabled = MiniJson.GetBool(root, "enabled", true);
            if (!fileEnabled)
            {
                WhySoLaggyPlugin.Log?.LogInfo("[WHY_LAG] FieldProbe rules file disabled by 'enabled:false'");
                return;
            }

            int fileRate = MiniJson.GetInt(root, "rateLimitPerRule", DefaultRateLimit);
            int fileMaxLen = MiniJson.GetInt(root, "maxValueLen", DefaultMaxValueLen);
            bool fileStack = MiniJson.GetBool(root, "includeStack", DefaultIncludeStack);
            int fileStackDepth = MiniJson.GetInt(root, "stackMaxDepth", DefaultStackMaxDepth);

            var rulesArr = MiniJson.AsArray(root.TryGetValue("rules", out var rv) ? rv : null);
            if (rulesArr == null || rulesArr.Count == 0)
            {
                WhySoLaggyPlugin.Log?.LogInfo("[WHY_LAG] FieldProbe: no rules in file");
                return;
            }

            int ruleId = 0;
            var parsed = new List<Rule>();
            foreach (var raw in rulesArr)
            {
                var ro = MiniJson.AsObject(raw);
                if (ro == null) continue;
                bool enabled = MiniJson.GetBool(ro, "enabled", true);
                if (!enabled) continue;
                string target = MiniJson.GetString(ro, "target");
                if (string.IsNullOrEmpty(target) || target.IndexOf('.') <= 0)
                {
                    WhySoLaggyPlugin.Log?.LogWarning($"[WHY_LAG] FieldProbe rule skipped: bad target '{target}'");
                    continue;
                }
                var fieldsArr = MiniJson.AsArray(ro.TryGetValue("fields", out var fv) ? fv : null);
                if (fieldsArr == null || fieldsArr.Count == 0)
                {
                    WhySoLaggyPlugin.Log?.LogWarning($"[WHY_LAG] FieldProbe rule skipped (no fields): {target}");
                    continue;
                }
                int dot = target.IndexOf('.');
                var rule = new Rule
                {
                    Id = ++ruleId,
                    TargetTypeName = target.Substring(0, dot),
                    TargetMethodName = target.Substring(dot + 1),
                    TargetKey = target,
                    RateLimit = MiniJson.GetInt(ro, "rateLimit", fileRate),
                    MaxValueLen = MiniJson.GetInt(ro, "maxValueLen", fileMaxLen),
                    IncludeStack = MiniJson.GetBool(ro, "includeStack", fileStack),
                    StackMaxDepth = MiniJson.GetInt(ro, "stackMaxDepth", fileStackDepth),
                    Note = MiniJson.GetString(ro, "note", ""),
                    Enabled = true,
                };
                var raws = new List<string>();
                var comps = new List<ExpressionEvaluator.CompiledExpr>();
                bool hasResult = false, hasException = false;
                foreach (var f in fieldsArr)
                {
                    var fs = f as string;
                    if (string.IsNullOrEmpty(fs)) continue;
                    fs = fs.Trim();
                    var ce = ExpressionEvaluator.Compile(fs);
                    if (!string.IsNullOrEmpty(ce.CompileError))
                    {
                        WhySoLaggyPlugin.Log?.LogWarning($"[WHY_LAG] FieldProbe compile err ({target}): '{fs}' → {ce.CompileError}");
                        continue;
                    }
                    if (ce.Root == ExpressionEvaluator.RootKind.Result) hasResult = true;
                    if (ce.Root == ExpressionEvaluator.RootKind.Exception) hasException = true;
                    raws.Add(fs);
                    comps.Add(ce);
                }
                if (comps.Count == 0) continue;
                rule.RawFields = raws.ToArray();
                rule.Compiled = comps.ToArray();
                rule.NeedsFinalizer = hasException;
                rule.NeedsPostfix = hasResult && !hasException;
                rule.NeedsPrefix = !rule.NeedsPostfix && !rule.NeedsFinalizer;
                parsed.Add(rule);
            }

            if (parsed.Count == 0)
            {
                WhySoLaggyPlugin.Log?.LogInfo("[WHY_LAG] FieldProbe: no valid rules after parsing");
                return;
            }

            // 挂 Harmony
            // 注意：Postfix/Finalizer 若声明 object[] __args，Harmony 会对 out/ref 参数
            // 做 box→unbox 回写，导致原函数写入的 out 值被覆盖成 default（Bug A 根因）。
            // 解决方案：Prefix 里把 __args 克隆到 object[] __state；Postfix/Finalizer 只读 __state，不再声明 __args。
            var hPrefix = new HarmonyMethod(typeof(FieldProbe), nameof(OnPrefix))
            { priority = Priority.First };
            var hArgsCapture = new HarmonyMethod(typeof(FieldProbe), nameof(OnArgsCapture))
            { priority = Priority.First };
            var hPostfix = new HarmonyMethod(typeof(FieldProbe), nameof(OnPostfix))
            { priority = Priority.First };
            var hFinalizer = new HarmonyMethod(typeof(FieldProbe), nameof(OnFinalizer))
            { priority = Priority.First };

            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try { types = asm.GetTypes(); } catch { continue; }
                foreach (var t in types)
                {
                    if (t == null) continue;
                    string typeName = t.Name;
                    foreach (var rule in parsed)
                    {
                        if (typeName != rule.TargetTypeName) continue;
                        // 搜所有同名重载（避免 GetMethod 多重载抛 AmbiguousMatchException）
                        MethodInfo[] all;
                        try
                        {
                            all = t.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                        }
                        catch { continue; }
                        int matchedInType = 0;
                        foreach (var mi in all)
                        {
                            if (mi == null || mi.Name != rule.TargetMethodName) continue;
                            // 泛型 / abstract 无法 Patch，跳过
                            if (mi.IsAbstract || mi.ContainsGenericParameters) continue;
                            try
                            {
                                // Postfix/Finalizer 需要读参数时，必须有一个 Prefix 写 __state。
                                // 优先用 OnPrefix（它顺便 Dispatch Prefix 规则）；若没有 Prefix 规则，退化为纯 capture。
                                HarmonyMethod chosenPrefix = null;
                                if (rule.NeedsPrefix) chosenPrefix = hPrefix;
                                else if (rule.NeedsPostfix || rule.NeedsFinalizer) chosenPrefix = hArgsCapture;

                                harmony.Patch(mi,
                                    prefix: chosenPrefix,
                                    postfix: rule.NeedsPostfix ? hPostfix : null,
                                    finalizer: rule.NeedsFinalizer ? hFinalizer : null);
                                if (!_methodToRules.TryGetValue(mi, out var list))
                                {
                                    list = new List<Rule>();
                                    _methodToRules[mi] = list;
                                }
                                list.Add(rule);
                                _hookedRules++;
                                matchedInType++;
                                string sig = FormatSig(mi);
                                AbuseLogger.Write($"[FIELD_PROBE] Hooked {rule.TargetKey}{sig} " +
                                                  $"(mode={(rule.NeedsPostfix ? "Postfix" : rule.NeedsFinalizer ? "Finalizer" : "Prefix")}, " +
                                                  $"fields={rule.Compiled.Length}, rate={rule.RateLimit}/s, stack={rule.IncludeStack})");
                            }
                            catch (Exception ex)
                            {
                                WhySoLaggyPlugin.Log?.LogWarning($"[WHY_LAG] FieldProbe Patch failed on {rule.TargetKey}{FormatSig(mi)}: {ex.Message}");
                            }
                        }
                        if (matchedInType == 0)
                        {
                            // 类名匹配但方法未找到时输出提示（不重复警告：同类名多程序集下只报首次权宜由用户自查）
                            WhySoLaggyPlugin.Log?.LogInfo($"[WHY_LAG] FieldProbe: type {typeName} matched in asm {asm.GetName().Name} but no method '{rule.TargetMethodName}' found");
                        }
                    }
                }
            }

            AbuseLogger.Write($"[FIELD_PROBE] Initialized (rules={parsed.Count}, hooks={_hookedRules}, file={Path.GetFileName(RulesFilePath)})");
        }

        // ── Harmony 回调 ──
        // Prefix 声明 object[] __args 仍会触发 box/unbox，但 Prefix 在原函数之前执行：
        // 1) out 参数入口未定义（Harmony box default），Prefix 不改，退出 unbox 写回 default，
        //    随后原函数覆盖写入真值 —— 无污染
        // 2) ref 参数入口为原值，Prefix 不改，退出写回原值 —— 无污染
        // 因此 Prefix 可以安全使用 __args，并顺便把参数克隆到 __state 供 Postfix 读。
        public static void OnPrefix(MethodBase __originalMethod, object[] __args, object __instance, out object[] __state)
        {
            __state = __args != null ? (object[])__args.Clone() : null;
            Dispatch(__originalMethod, __args, __instance, null, null, kind: 0);
        }

        // 纯 capture：仅在没有 Prefix 规则、但 Postfix/Finalizer 需要读参数时挂载。
        public static void OnArgsCapture(object[] __args, out object[] __state)
        {
            __state = __args != null ? (object[])__args.Clone() : null;
        }

        // Postfix 不声明 object[] __args —— 这是修复 Bug A 的关键：
        // Harmony 不会对 out/ref 参数做 box/unbox 回写。改为读 Prefix 克隆好的 __state。
        public static void OnPostfix(MethodBase __originalMethod, object __instance, object __result, object[] __state)
        {
            Dispatch(__originalMethod, __state, __instance, __result, null, kind: 1);
        }

        public static Exception OnFinalizer(MethodBase __originalMethod, object __instance, object __result, Exception __exception, object[] __state)
        {
            Dispatch(__originalMethod, __state, __instance, __result, __exception, kind: 2);
            return __exception; // 不吞异常
        }

        private static void Dispatch(MethodBase mb, object[] args, object instance, object result, Exception ex, int kind)
        {
            if (mb == null) return;
            if (!_methodToRules.TryGetValue(mb, out var rules)) return;
            for (int i = 0; i < rules.Count; i++)
            {
                var rule = rules[i];
                if (!rule.Enabled) continue;
                // 路由：仅让对应 kind 的 rule 执行
                bool match = (kind == 0 && rule.NeedsPrefix) ||
                             (kind == 1 && rule.NeedsPostfix) ||
                             (kind == 2 && rule.NeedsFinalizer);
                if (!match) continue;
                if (!CheckRate(rule)) continue;
                WriteSnapshot(rule, instance, args, result, ex, mb);
            }
        }

        private static void WriteSnapshot(Rule rule, object instance, object[] args, object result, Exception ex, MethodBase mb)
        {
            var sb = new StringBuilder(128);
            for (int i = 0; i < rule.Compiled.Length; i++)
            {
                if (i > 0) sb.Append(";");
                sb.Append(rule.RawFields[i]);
                sb.Append('=');
                string val;
                try { val = ExpressionEvaluator.Evaluate(rule.Compiled[i], instance, args, result, ex, rule.MaxValueLen); }
                catch (Exception e) { val = "err:" + e.GetType().Name; }
                sb.Append(val);
            }

            string stack = null, caller = null;
            if (rule.IncludeStack)
            {
                string raw;
                try { raw = Environment.StackTrace; } catch { raw = null; }
                stack = FilterStack(raw, rule.StackMaxDepth);
                caller = ExtractFirstFrame(stack);
            }

            try
            {
                StructuredLogger.WriteEvent(new StructuredEvent
                {
                    Timestamp = StructuredLogger.NowStamp(),
                    FrameNumber = UnityEngine.Time.frameCount,
                    Type = EventType.MethodTrace,
                    Fields = new Dictionary<string, object>
                    {
                        { "TargetMethod", rule.TargetKey },
                        { "Snapshot", Truncate(sb.ToString(), 2000) },
                        { "TraceStack", stack ?? "" },
                        { "TraceCaller", caller ?? "" },
                        { "PatchType", kindToStr(rule) },
                    },
                });
            }
            catch { }
        }

        private static string kindToStr(Rule r)
            => r.NeedsPostfix ? "Postfix" : r.NeedsFinalizer ? "Finalizer" : "Prefix";

        private static bool CheckRate(Rule rule)
        {
            lock (_rateLock)
            {
                long now = DateTime.UtcNow.Ticks;
                _windowStart.TryGetValue(rule.Id, out long start);
                if (start == 0 || now - start >= _ticksPerSec)
                {
                    _windowStart[rule.Id] = now;
                    _counter[rule.Id] = 1;
                    _overflowWarned.Remove(rule.Id);
                    return true;
                }
                _counter.TryGetValue(rule.Id, out int c);
                c++;
                _counter[rule.Id] = c;
                if (c > rule.RateLimit)
                {
                    if (_overflowWarned.Add(rule.Id))
                        WhySoLaggyPlugin.Log?.LogWarning(
                            $"[WHY_LAG] FieldProbe rate limit hit for {rule.TargetKey} (>{rule.RateLimit}/s)");
                    return false;
                }
                return true;
            }
        }

        private static string FilterStack(string stack, int maxDepth)
        {
            if (string.IsNullOrEmpty(stack) || maxDepth <= 0) return "";
            var lines = stack.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var sb = new StringBuilder(128);
            int kept = 0;
            foreach (var raw in lines)
            {
                var line = raw.TrimStart();
                if (!line.StartsWith("at ", StringComparison.Ordinal)) continue;
                if (line.IndexOf("UnityEngine.", StringComparison.Ordinal) >= 0) continue;
                if (line.IndexOf("HarmonyLib.", StringComparison.Ordinal) >= 0) continue;
                if (line.IndexOf("MonoMod.", StringComparison.Ordinal) >= 0) continue;
                if (line.IndexOf("System.Environment.", StringComparison.Ordinal) >= 0) continue;
                if (line.IndexOf("WhySoLaggy.FieldProbe", StringComparison.Ordinal) >= 0) continue;
                if (line.IndexOf("WhySoLaggy.ExpressionEvaluator", StringComparison.Ordinal) >= 0) continue;
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

        private static string FormatSig(MethodBase mb)
        {
            if (mb == null) return "";
            var ps = mb.GetParameters();
            if (ps == null || ps.Length == 0) return "()";
            var sb = new StringBuilder(32);
            sb.Append('(');
            for (int i = 0; i < ps.Length; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(ps[i].ParameterType.Name);
            }
            sb.Append(')');
            return sb.ToString();
        }

        private static string Truncate(string s, int max)
        {
            if (string.IsNullOrEmpty(s) || s.Length <= max) return s;
            return s.Substring(0, max) + "...";
        }
    }
}
