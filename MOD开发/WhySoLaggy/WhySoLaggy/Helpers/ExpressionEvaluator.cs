using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace WhySoLaggy
{
    /// <summary>
    /// FieldProbe 表达式求值器（1.0.3）。
    ///
    /// 支持语法：
    ///   根节点（必须）：
    ///     __instance         ← Harmony 注入 this
    ///     __arg0, __arg1...  ← 第 N 个参数（按位）
    ///     __args             ← 全部参数（输出 "[a, b, c]"）
    ///     __result           ← Postfix 返回值
    ///     __exception        ← Finalizer 捕获的异常
    ///     TypeName           ← 任意已加载类型名（静态起点）
    ///   路径运算符：
    ///     .Member            ← 字段/属性（普通）
    ///     ?.Member           ← null 传播（空即短路返回 "null"）
    ///     [N]                ← 数组 / IList 索引（支持负下标暂未开）
    ///     .Count / .Length   ← 集合/数组优先用原生 Count/Length（找不到再反射）
    ///
    /// 任一环抛异常：返回 "err:&lt;ExName&gt;"；路径断在 null 上：非 NullSafe 返回
    /// "null-deref:&lt;member&gt;"，NullSafe 返回 "null"。
    ///
    /// 反射成员按 (Type, Name) 缓存于 <see cref="_memberCache"/>；
    /// 静态根按 TypeName 缓存于 <see cref="_typeCache"/>；
    /// 整条表达式编译结果由调用方（FieldProbe）自行缓存。
    /// </summary>
    internal static class ExpressionEvaluator
    {
        // ── 类型 ──
        public enum RootKind { Instance, Arg, Args, Result, Exception, StaticType }

        public struct Step
        {
            public string Name;     // 成员名；IsIndex 时忽略
            public bool NullSafe;   // ?. 运算
            public bool IsIndex;    // true 表示 [N]
            public int Index;       // IsIndex 时用
        }

        public sealed class CompiledExpr
        {
            public string Source;
            public RootKind Root;
            public int ArgIndex;         // Root=Arg 时用
            public Type StaticRootType;  // Root=StaticType 时用
            public List<Step> Steps;     // 路径步骤
            public string CompileError;  // 非 null 表示编译期失败
        }

        // ── 缓存 ──
        private static readonly Dictionary<string, Type> _typeCache = new Dictionary<string, Type>(StringComparer.Ordinal);
        private static readonly Dictionary<string, MemberReader> _memberCache = new Dictionary<string, MemberReader>(StringComparer.Ordinal);
        private static readonly object _cacheLock = new object();

        private sealed class MemberReader
        {
            public FieldInfo Field;
            public PropertyInfo Property;
            public bool IsNone;  // 找不到
            public object Read(object instance)
            {
                if (Field != null) return Field.GetValue(instance);
                if (Property != null) return Property.GetValue(instance, null);
                return null;
            }
        }

        // ── 编译 ──
        public static CompiledExpr Compile(string expr)
        {
            var result = new CompiledExpr { Source = expr, Steps = new List<Step>() };
            if (string.IsNullOrEmpty(expr)) { result.CompileError = "empty"; return result; }

            int pos = 0;
            string head = ReadIdent(expr, ref pos);
            if (string.IsNullOrEmpty(head)) { result.CompileError = "no root token"; return result; }

            switch (head)
            {
                case "__instance": result.Root = RootKind.Instance; break;
                case "__args":     result.Root = RootKind.Args; break;
                case "__result":   result.Root = RootKind.Result; break;
                case "__exception": result.Root = RootKind.Exception; break;
                default:
                    if (head.StartsWith("__arg", StringComparison.Ordinal) &&
                        int.TryParse(head.Substring(5), out int idx) && idx >= 0)
                    {
                        result.Root = RootKind.Arg;
                        result.ArgIndex = idx;
                    }
                    else
                    {
                        var t = ResolveType(head);
                        if (t == null) { result.CompileError = "unknown root/type: " + head; return result; }
                        result.Root = RootKind.StaticType;
                        result.StaticRootType = t;
                    }
                    break;
            }

            // 读后续 steps
            while (pos < expr.Length)
            {
                char c = expr[pos];
                if (c == '.')
                {
                    pos++;
                    string name = ReadIdent(expr, ref pos);
                    if (string.IsNullOrEmpty(name)) { result.CompileError = "expected member after '.'"; return result; }
                    result.Steps.Add(new Step { Name = name, NullSafe = false });
                }
                else if (c == '?')
                {
                    if (pos + 1 >= expr.Length || expr[pos + 1] != '.')
                    { result.CompileError = "expected '?.' at " + pos; return result; }
                    pos += 2;
                    string name = ReadIdent(expr, ref pos);
                    if (string.IsNullOrEmpty(name)) { result.CompileError = "expected member after '?.'"; return result; }
                    result.Steps.Add(new Step { Name = name, NullSafe = true });
                }
                else if (c == '[')
                {
                    pos++;
                    int s = pos;
                    while (pos < expr.Length && expr[pos] != ']') pos++;
                    if (pos >= expr.Length) { result.CompileError = "unterminated [index]"; return result; }
                    string num = expr.Substring(s, pos - s).Trim();
                    pos++; // skip ]
                    if (!int.TryParse(num, out int iv)) { result.CompileError = "bad index '" + num + "'"; return result; }
                    result.Steps.Add(new Step { IsIndex = true, Index = iv });
                }
                else
                {
                    result.CompileError = "unexpected '" + c + "' at " + pos;
                    return result;
                }
            }
            return result;
        }

        // ── 执行 ──
        public static string Evaluate(CompiledExpr ce, object instance, object[] args, object result, Exception ex, int maxLen)
        {
            if (ce == null) return "err:null_expr";
            if (!string.IsNullOrEmpty(ce.CompileError)) return "err:compile_" + ce.CompileError;

            object cur;
            try { cur = GetRoot(ce, instance, args, result, ex); }
            catch (Exception e) { return "err:root_" + e.GetType().Name; }

            int stepCount = ce.Steps?.Count ?? 0;
            for (int i = 0; i < stepCount; i++)
            {
                var step = ce.Steps[i];
                if (cur == null || IsUnityNull(cur))
                {
                    return step.NullSafe ? "null" : ("null-deref:" + (step.IsIndex ? "[" + step.Index + "]" : step.Name));
                }

                if (step.IsIndex)
                {
                    var list = cur as IList;
                    if (list == null) { return "err:not_ilist"; }
                    if (step.Index < 0 || step.Index >= list.Count) return "err:idx_oor";
                    try { cur = list[step.Index]; }
                    catch (Exception e) { return "err:idx_" + e.GetType().Name; }
                }
                else
                {
                    // 快捷：Count / Length 对集合和数组先走原生
                    if ((step.Name == "Count" || step.Name == "Length") && TryGetCollectionSize(cur, out int sz))
                    {
                        cur = sz;
                        continue;
                    }

                    // 静态或实例反射成员
                    Type targetType;
                    bool isStaticStep = (i == 0 && ce.Root == RootKind.StaticType);
                    object readFrom;
                    if (isStaticStep)
                    {
                        targetType = ce.StaticRootType;
                        readFrom = null;  // 静态
                    }
                    else
                    {
                        targetType = cur.GetType();
                        readFrom = cur;
                    }

                    var reader = GetMember(targetType, step.Name, isStaticStep);
                    if (reader.IsNone) return "err:no_member_" + step.Name;
                    try { cur = reader.Read(readFrom); }
                    catch (Exception e) { return "err:read_" + e.GetType().Name; }
                }
            }

            // args 根未走 steps 时直接展开
            if (stepCount == 0 && ce.Root == RootKind.Args)
            {
                return FormatArgs(args, maxLen);
            }

            return ValueFormatter.Format(cur, maxLen);
        }

        private static string FormatArgs(object[] args, int maxLen)
        {
            if (args == null) return "null";
            if (args.Length == 0) return "[]";
            var sb = new System.Text.StringBuilder(64);
            sb.Append('[');
            for (int i = 0; i < args.Length; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(ValueFormatter.Format(args[i], 32));
            }
            sb.Append(']');
            string s = sb.ToString();
            return s.Length > maxLen ? s.Substring(0, maxLen) + "..." : s;
        }

        private static object GetRoot(CompiledExpr ce, object instance, object[] args, object result, Exception ex)
        {
            switch (ce.Root)
            {
                case RootKind.Instance: return instance;
                case RootKind.Args: return args;
                case RootKind.Result: return result;
                case RootKind.Exception: return ex;
                case RootKind.Arg:
                    if (args == null || ce.ArgIndex >= args.Length) return null;
                    return args[ce.ArgIndex];
                case RootKind.StaticType:
                    // 静态成员通过第一步读取，这里先返回占位（Step 循环里会识别 i==0 && StaticType）
                    return null;
            }
            return null;
        }

        private static Type ResolveType(string name)
        {
            lock (_cacheLock)
            {
                if (_typeCache.TryGetValue(name, out var cached)) return cached;
                Type found = null;
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    Type[] types;
                    try { types = asm.GetTypes(); } catch { continue; }
                    foreach (var t in types)
                    {
                        if (t.Name == name) { found = t; break; }
                    }
                    if (found != null) break;
                }
                _typeCache[name] = found;
                return found;
            }
        }

        private static MemberReader GetMember(Type type, string name, bool isStatic)
        {
            string key = (isStatic ? "S|" : "I|") + type.FullName + "|" + name;
            lock (_cacheLock)
            {
                if (_memberCache.TryGetValue(key, out var cached)) return cached;

                var flags = BindingFlags.Public | BindingFlags.NonPublic |
                            (isStatic ? BindingFlags.Static : BindingFlags.Instance);
                var reader = new MemberReader();
                FieldInfo fi = null;
                PropertyInfo pi = null;
                // 向上查找继承链
                Type t = type;
                while (t != null && fi == null && pi == null)
                {
                    try { fi = t.GetField(name, flags | BindingFlags.DeclaredOnly); } catch { }
                    if (fi == null)
                        try { pi = t.GetProperty(name, flags | BindingFlags.DeclaredOnly); } catch { }
                    t = t.BaseType;
                }
                reader.Field = fi;
                reader.Property = pi;
                reader.IsNone = (fi == null && pi == null);
                _memberCache[key] = reader;
                return reader;
            }
        }

        private static bool TryGetCollectionSize(object v, out int size)
        {
            size = 0;
            if (v is Array arr) { size = arr.Length; return true; }
            if (v is ICollection c) { size = c.Count; return true; }
            // 走 .Count 属性回退：显式读一次反射
            var type = v.GetType();
            var prop = type.GetProperty("Count");
            if (prop != null && prop.PropertyType == typeof(int))
            {
                try { size = (int)prop.GetValue(v, null); return true; } catch { }
            }
            return false;
        }

        private static bool IsUnityNull(object v)
        {
            // 1.0.3 修正：原逻辑永远返回 false，FieldProbe 的 ?.Member null-safe 羊路本质失效。
            // 本意：当装载的 UnityEngine.Object 已被 Destroy（uo==null 返回 true，但 CLR 引用还活）时，视为 Unity null。
            //   • v 为 null：返回 true（防御，外层通常已检测）
            //   • v 不是 UnityEngine.Object 子类：返回 false（这些类型由外层 cur==null 处理）
            //   • v 是未销毁的 Unity.Object：Unity 重载 == null 为 false，返回 false
            //   • v 是已销毁的 Unity.Object：Unity 重载 == null 为 true，返回 true
            if (v == null) return true;
            var uo = v as UnityEngine.Object;
            if (ReferenceEquals(uo, null)) return false; // 非 Unity.Object 类型，不用 Unity 重载判断
            return uo == null;                           // Unity 重载：已 Destroy 时为 true
        }

        private static string ReadIdent(string s, ref int pos)
        {
            int start = pos;
            while (pos < s.Length)
            {
                char c = s[pos];
                if (c == '_' || (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9')) pos++;
                else break;
            }
            return pos > start ? s.Substring(start, pos - start) : "";
        }

        public static void ClearCaches()
        {
            lock (_cacheLock)
            {
                _typeCache.Clear();
                _memberCache.Clear();
            }
        }
    }
}
