using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Diagnostics;
using HarmonyLib;

public abstract class ProbeBase<T>
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    public virtual string GetDisplayName() { return typeof(T).Name; }
}
public class ProbeKey : ProbeBase<int> {}
public class ProbePath : ProbeBase<string> {}
public static class ProbeCallbacks
{
    public static int Hits;
    public static void DisplayPostfix(object __instance, ref string __result) { __result += ":localized"; }
    public static void TimingPrefix(MethodBase __originalMethod, out long __state) { __state = Stopwatch.GetTimestamp(); }
    public static void TimingPostfix(MethodBase __originalMethod, long __state) { Hits++; }
}
public class GenericPatchProbe
{
    public static int Main(string[] args)
    {
        try
        {
            string mode=args[0];
            Type type=args.Length>1 && args[1]=="string" ? typeof(ProbePath) : typeof(ProbeKey);
            MethodInfo inherited=type.GetMethod("GetDisplayName");
            MethodInfo declared=type.BaseType.GetMethod("GetDisplayName",BindingFlags.Public|BindingFlags.Instance|BindingFlags.DeclaredOnly);
            var external=new Harmony("probe.external");
            var localized=new Harmony("probe.localized");
            var timing=new Harmony("probe.timing");
            var postfix=new HarmonyMethod(typeof(ProbeCallbacks),"DisplayPostfix");
            MethodInfo first=mode.StartsWith("declared") ? declared : inherited;
            external.Patch(first,postfix:postfix);
            if(mode=="inherited-then-declared") localized.Patch(declared,postfix:postfix);
            if(mode=="declared-then-inherited") localized.Patch(inherited,postfix:postfix);
            MethodBase[] targets=Harmony.GetAllPatchedMethods().Where(x=>x.Name=="GetDisplayName").ToArray();
            foreach(MethodBase target in targets)
            {
                Console.WriteLine("mode="+mode+"; type="+type.Name+"; declaring="+target.DeclaringType+"; reflected="+target.ReflectedType);
                if(mode!="inherited-no-profiler")
                    timing.Patch(mode=="inherited-normalized-profiler" ? declared : target,
                    prefix:new HarmonyMethod(typeof(ProbeCallbacks),"TimingPrefix"),
                    postfix:new HarmonyMethod(typeof(ProbeCallbacks),"TimingPostfix"));
            }
            object value=inherited.Invoke(Activator.CreateInstance(type),null);
            Console.WriteLine("RESULT=PASS value="+value+" timingHits="+ProbeCallbacks.Hits);
            return 0;
        }
        catch(Exception ex)
        {
            while(ex.InnerException!=null)ex=ex.InnerException;
            Console.WriteLine("RESULT=FAIL "+ex.GetType().Name+": "+ex.Message);
            return 2;
        }
    }
}
