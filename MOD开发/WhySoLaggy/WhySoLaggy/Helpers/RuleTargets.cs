using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
namespace WhySoLaggy
{
    internal static class RuleTargets
    {
        internal static void RemoveOwner(HarmonyLib.Harmony harmony)
        {
            if(harmony==null)return;
            // This Harmony build implements UnpatchSelf via callback identity. Two
            // generations intentionally share callbacks, so restrict removal by owner.
            foreach(var method in harmony.GetPatchedMethods().ToArray())harmony.Unpatch(method,HarmonyLib.HarmonyPatchType.All,harmony.Id);
        }
        internal static MethodBase[] Resolve(string target)
        {
            int dot=target?.LastIndexOf('.')??-1;
            if(dot<=0)throw new ArgumentException("Expected Type.Method");
            string name=target.Substring(0,dot),method=target.Substring(dot+1);
            var types=new List<Type>();
            foreach(var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {try{types.AddRange(assembly.GetTypes().Where(x=>x.FullName==name||x.Name==name));}catch(ReflectionTypeLoadException ex){types.AddRange(ex.Types.Where(x=>x!=null&&(x.FullName==name||x.Name==name)));}}
            if(types.Count!=1)throw new ArgumentException("Target type missing or ambiguous: "+name);
            var result=types[0].GetMethods(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static|BindingFlags.DeclaredOnly).Where(x=>x.Name==method&&!x.IsAbstract&&!x.ContainsGenericParameters).Cast<MethodBase>().ToArray();
            if(result.Length==0)throw new ArgumentException("Target method missing: "+target);
            return result;
        }
    }
}
