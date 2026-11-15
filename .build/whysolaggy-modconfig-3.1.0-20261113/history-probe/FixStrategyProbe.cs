using System;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Diagnostics;
using HarmonyLib;

public abstract class StrategyBase<T>
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    public virtual string GetDisplayName() { return typeof(T).Name; }
}
public class StrategyKey : StrategyBase<int> { }
public class StrategyPath : StrategyBase<string> { }
public static class StrategyCallbacks
{
    public static int Hits;
    public static int LastId=-1;
    public static void LocalizeA(ref string __result) { __result += ":A"; }
    public static void LocalizeB(ref string __result) { __result += ":B"; }
    public static void LateLocalize(ref string __result) { __result += ":L"; }
    public static void Prefix(MethodBase __originalMethod, out long __state) { __state=Stopwatch.GetTimestamp(); }
    public static void Postfix(MethodBase __originalMethod, long __state) { Record(7,__state); }
    public static void Record(int id,long start)
    {
        if(start<=0 || start>Stopwatch.GetTimestamp()) throw new Exception("State was not propagated");
        Hits++; LastId=id;
    }
}
public class FixStrategyProbe
{
    static MethodInfo[] EmitCallbacks()
    {
        AssemblyBuilder a=AppDomain.CurrentDomain.DefineDynamicAssembly(new AssemblyName("ProbeBoundCallbacks"),AssemblyBuilderAccess.Run);
        TypeBuilder t=a.DefineDynamicModule("main").DefineType("Timer7",TypeAttributes.Public|TypeAttributes.Abstract|TypeAttributes.Sealed);
        MethodBuilder pre=t.DefineMethod("Prefix",MethodAttributes.Public|MethodAttributes.Static,typeof(void),new[]{typeof(long).MakeByRefType()});
        pre.DefineParameter(1,ParameterAttributes.Out,"__state");
        ILGenerator il=pre.GetILGenerator();
        il.Emit(OpCodes.Ldarg_0);il.Emit(OpCodes.Call,typeof(Stopwatch).GetMethod("GetTimestamp"));il.Emit(OpCodes.Stind_I8);il.Emit(OpCodes.Ret);
        MethodBuilder post=t.DefineMethod("Postfix",MethodAttributes.Public|MethodAttributes.Static,typeof(void),new[]{typeof(long)});
        post.DefineParameter(1,ParameterAttributes.None,"__state");
        il=post.GetILGenerator();
        il.Emit(OpCodes.Ldc_I4,7);il.Emit(OpCodes.Ldarg_0);il.Emit(OpCodes.Call,typeof(StrategyCallbacks).GetMethod("Record"));il.Emit(OpCodes.Ret);
        Type done=t.CreateType();return new[]{done.GetMethod("Prefix"),done.GetMethod("Postfix")};
    }
    static string Invoke(MethodInfo method,object value)
    {
        try { return (string)method.Invoke(value,null); }
        catch(TargetInvocationException ex) { throw ex.InnerException; }
    }
    public static int Main(string[] args)
    {
        string strategy=args[0],order=args[1],kind=args[2];
        Console.WriteLine("strategy="+strategy+" order="+order+" type="+kind);
        try
        {
            Type child=kind=="int"?typeof(StrategyKey):typeof(StrategyPath);
            MethodInfo inherited=child.GetMethod("GetDisplayName");
            MethodInfo declared=child.BaseType.GetMethod("GetDisplayName",BindingFlags.Public|BindingFlags.Instance|BindingFlags.DeclaredOnly);
            object value=Activator.CreateInstance(child);
            Harmony external=new Harmony("strategy.external"),own=new Harmony("strategy.plugin"),timer=new Harmony("strategy.plugin.patchprofiler"),late=new Harmony("strategy.late");
            MethodInfo first=order=="inherited-first"?inherited:declared;
            MethodInfo second=order=="inherited-first"?declared:inherited;
            external.Patch(first,postfix:new HarmonyMethod(typeof(StrategyCallbacks),"LocalizeA"));
            own.Patch(second,postfix:new HarmonyMethod(typeof(StrategyCallbacks),"LocalizeB"));
            string baseline=Invoke(inherited,value);
            MethodInfo[] hooks=strategy=="bound"?EmitCallbacks():new[]{typeof(StrategyCallbacks).GetMethod("Prefix"),typeof(StrategyCallbacks).GetMethod("Postfix")};
            Action install=()=>{if(strategy!="skip")timer.Patch(declared,prefix:new HarmonyMethod(hooks[0]){priority=800},postfix:new HarmonyMethod(hooks[1]){priority=0});};
            install();
            int oldHits=StrategyCallbacks.Hits;
            string initial=Invoke(inherited,value);
            if(initial!=baseline || StrategyCallbacks.Hits-oldHits!=(strategy=="skip"?0:1))throw new Exception("Initial behavior/count changed");
            Console.WriteLine("initial=PASS value="+initial+" hits="+StrategyCallbacks.Hits);
            late.Patch(inherited,postfix:new HarmonyMethod(typeof(StrategyCallbacks),"LateLocalize"));
            oldHits=StrategyCallbacks.Hits;
            string afterLate=Invoke(inherited,value);
            if(StrategyCallbacks.Hits-oldHits!=(strategy=="skip"?0:1) || !afterLate.Contains(":L"))throw new Exception("Late behavior/count changed");
            Console.WriteLine("late=PASS value="+afterLate+" hits="+StrategyCallbacks.Hits);
            timer.UnpatchSelf();
            oldHits=StrategyCallbacks.Hits;
            string afterOff=Invoke(inherited,value);
            if(afterOff!=afterLate || StrategyCallbacks.Hits!=oldHits)throw new Exception("Disable damaged localization or kept timing");
            Console.WriteLine("disable=PASS value="+afterOff);
            for(int cycle=0;cycle<3;cycle++)
            {
                install();oldHits=StrategyCallbacks.Hits;
                string afterOn=Invoke(inherited,value);
                if(afterOn!=afterLate || StrategyCallbacks.Hits-oldHits!=(strategy=="skip"?0:1))throw new Exception("Repeated enable duplicated or damaged patches");
                timer.UnpatchSelf();
            }
            Console.WriteLine("RESULT=PASS callbackId="+StrategyCallbacks.LastId+" cycles=3");return 0;
        }
        catch(Exception ex){Console.WriteLine("RESULT=FAIL "+ex.GetType().Name+": "+ex.Message);return 2;}
    }
}
