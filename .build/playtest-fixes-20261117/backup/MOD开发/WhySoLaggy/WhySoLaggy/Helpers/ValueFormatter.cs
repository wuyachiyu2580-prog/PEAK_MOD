using System;
using System.Collections;
using System.Globalization;
using System.Text;

namespace WhySoLaggy
{
    /// <summary>
    /// 通用值序列化（1.0.3 FieldProbe）。
    /// - 对基础类型/字符串/枚举 → ToString；
    /// - 对 <see cref="UnityEngine.Object"/> → "{name}#{instanceID}"，并额外处理 "MissingReference" 情况；
    /// - 对集合（IEnumerable）→ "[n=&lt;count&gt; | e0, e1, e2, ...]"（最多前 3 元素）；
    /// - 对其他对象 → "TypeName@HashCode"；
    /// - 超长自动截断到 maxLen，追加 "..."。
    /// - 任何反射或 ToString 抛异常会被吃掉，输出 "err:ExName"，保障日志不中断主逻辑。
    /// </summary>
    internal static class ValueFormatter
    {
        public const int DefaultMaxLen = 128;

        public static string Format(object v, int maxLen = DefaultMaxLen)
        {
            string raw;
            try { raw = FormatCore(v); }
            catch (Exception ex) { raw = "err:" + ex.GetType().Name; }
            return Truncate(raw, maxLen);
        }

        private static string FormatCore(object v)
        {
            if (v == null) return "null";

            // Unity 空指针陷阱：UnityEngine.Object 可能 == null 但引用非 null
            if (v is UnityEngine.Object uo)
            {
                if(!DiagnosticContext.IsMainThread)return "[Unity object: main thread only]";
                if (uo == null) return "null(UE)";
                string name;
                try { name = uo.name; }
                catch { name = "<name_err>"; }
                int iid;
                try { iid = uo.GetInstanceID(); }
                catch { iid = 0; }
                return name + "#" + iid.ToString(CultureInfo.InvariantCulture);
            }

            switch (v)
            {
                case string s: return s;
                case bool b: return b ? "true" : "false";
                case char c: return c.ToString();
                case float f: return f.ToString("R", CultureInfo.InvariantCulture);
                case double d: return d.ToString("R", CultureInfo.InvariantCulture);
                case decimal m: return m.ToString(CultureInfo.InvariantCulture);
                case byte _:
                case sbyte _:
                case short _:
                case ushort _:
                case int _:
                case uint _:
                case long _:
                case ulong _:
                    return Convert.ToString(v, CultureInfo.InvariantCulture);
            }

            var type = v.GetType();
            if (type.IsEnum) return v.ToString();

            // 集合
            if (v is IEnumerable en && !(v is string))
            {
                return FormatEnumerable(en);
            }

            // 其它对象：TypeName@HashCode
            return type.Name + "@" + RuntimeHashCode(v);
        }

        [ThreadStatic] private static int depth;
        private static string FormatEnumerable(IEnumerable en)
        {
            if(depth>=3)return "[depth limit]";
            if(!(en is IList list)||!(en is Array||en.GetType()==typeof(ArrayList)||en.GetType().IsGenericType&&en.GetType().GetGenericTypeDefinition()==typeof(System.Collections.Generic.List<>)))return "["+en.GetType().Name+": enumeration omitted]";
            depth++;
            try{var sb=new StringBuilder("[");int count=list.Count;for(int i=0;i<Math.Min(count,3);i++){if(i>0)sb.Append(", ");if(ReferenceEquals(list[i],en))sb.Append("[cycle]");else sb.Append(Truncate(FormatCore(list[i]),32));}if(count>3)sb.Append(", ...");return sb.Append("] n=").Append(count).ToString();}
            finally{depth--;}
        }

        private static string RuntimeHashCode(object v)
        {
            try { return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(v).ToString(CultureInfo.InvariantCulture); }
            catch { return "?"; }
        }

        private static string Truncate(string s, int maxLen)
        {
            if (string.IsNullOrEmpty(s)) return s ?? "null";
            if (maxLen <= 0 || s.Length <= maxLen) return s;
            return s.Substring(0, maxLen) + "...";
        }
    }
}
