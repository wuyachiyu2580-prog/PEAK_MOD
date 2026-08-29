using System;
using System.Reflection;
using System.Text;

namespace WhySoLaggy
{
    internal static class MethodKey
    {
        public static string Canonical(MethodBase method)
        {
            if (method == null) return "(null)";
            var sb = new StringBuilder(96);
            sb.Append(method.DeclaringType?.FullName ?? "?")
              .Append('.')
              .Append(method.Name)
              .Append('(');
            ParameterInfo[] parameters = method.GetParameters();
            for (int i = 0; i < parameters.Length; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(FormatType(parameters[i].ParameterType));
            }
            return sb.Append(')').ToString();
        }

        public static string Legacy(MethodBase method)
        {
            if (method == null) return "(null)";
            return (method.DeclaringType?.Name ?? "?") + "." + method.Name;
        }

        public static string LegacyFullType(MethodBase method)
        {
            if (method == null) return "(null)";
            return (method.DeclaringType?.FullName ?? "?") + "." + method.Name;
        }

        public static bool IsIgnored(MethodBase method, System.Collections.Generic.HashSet<string> ignoreSet)
        {
            if (method == null || ignoreSet == null || ignoreSet.Count == 0) return false;
            return ignoreSet.Contains(Canonical(method))
                || ignoreSet.Contains(Legacy(method))
                || ignoreSet.Contains(LegacyFullType(method));
        }

        private static string FormatType(Type type)
        {
            if (type == null) return "?";
            if (type.IsByRef) return FormatType(type.GetElementType()) + "&";
            if (type.IsPointer) return FormatType(type.GetElementType()) + "*";
            if (type.IsArray) return FormatType(type.GetElementType()) + "[]";
            if (!type.IsGenericType) return type.FullName ?? type.Name;

            string name = type.GetGenericTypeDefinition().FullName ?? type.Name;
            int tick = name.IndexOf('`');
            if (tick >= 0) name = name.Substring(0, tick);
            var sb = new StringBuilder(name).Append('<');
            Type[] args = type.GetGenericArguments();
            for (int i = 0; i < args.Length; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(FormatType(args[i]));
            }
            return sb.Append('>').ToString();
        }
    }
}
