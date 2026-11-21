using System;
using System.Linq;
using System.Reflection;

namespace WhySoLaggy
{
    internal static class ProfilerTargetResolver
    {
        internal static bool TryResolve(MethodBase source, out MethodBase target, out string reason)
        {
            target = null;
            reason = null;
            try
            {
                if (source?.DeclaringType == null || source.ContainsGenericParameters || source.IsAbstract)
                { reason = "Open generic, abstract, or missing declaring type"; return false; }
                Type declaring = source.DeclaringType;
                const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic |
                    BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
                MethodBase candidate = source is ConstructorInfo
                    ? declaring.GetConstructors(flags).FirstOrDefault(m => m.Module == source.Module && m.MetadataToken == source.MetadataToken)
                    : (MethodBase)declaring.GetMethods(flags).FirstOrDefault(m => m.Module == source.Module && m.MetadataToken == source.MetadataToken);
                if (source is MethodInfo method && method.IsGenericMethod && !method.IsGenericMethodDefinition)
                    candidate = (candidate as MethodInfo)?.MakeGenericMethod(method.GetGenericArguments());
                if (candidate == null || candidate.ContainsGenericParameters || candidate.DeclaringType != declaring ||
                    candidate.ReflectedType != declaring || candidate.Module != source.Module || candidate.MetadataToken != source.MetadataToken)
                { reason = "Could not preserve the declared method identity"; return false; }
                if (candidate.GetMethodBody() == null)
                { reason = "No managed method body"; return false; }
                target = candidate;
                return true;
            }
            catch (Exception ex) { reason = ex.GetType().Name + ": " + ex.Message; return false; }
        }

        // Built once during registration; display names are not unique method identities.
        internal static string Identity(MethodBase method)
        {
            string arguments = method is MethodInfo info && info.IsGenericMethod
                ? string.Join(";", info.GetGenericArguments().Select(t => t.AssemblyQualifiedName)) : "";
            return method.Module.ModuleVersionId + ":" + method.MetadataToken + ":" +
                method.DeclaringType.AssemblyQualifiedName + ":" + arguments;
        }
    }
}
