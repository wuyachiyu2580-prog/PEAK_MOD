using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace WhySoLaggy.Tests
{
    [TestClass, DoNotParallelize]
    public class PatchProfilerIntegrationTests
    {
        public abstract class InputSetting<T>
        {
            [MethodImpl(MethodImplOptions.NoInlining)]
            public virtual string GetDisplayName() => typeof(T).Name;
        }
        public class KeySetting : InputSetting<int> { }
        public class PathSetting : InputSetting<string> { }
        public static void TranslateA(ref string __result) { __result += ":A"; }
        public static void TranslateB(ref string __result) { __result += ":B"; }
        public static void TranslateLate(ref string __result) { __result += ":L"; }

        [DataTestMethod]
        [DataRow(false, false)]
        [DataRow(true, false)]
        [DataRow(false, true)]
        [DataRow(true, true)]
        public void SharedGenericSettings_KeepTranslationsAndCountOnce(bool referenceType, bool inheritedFirst)
        {
            string id="wsl.test."+Guid.NewGuid().ToString("N");
            var external=new Harmony(id+".external");
            var plugin=new Harmony(id);
            var late=new Harmony(id+".late");
            Type child=referenceType?typeof(PathSetting):typeof(KeySetting);
            MethodInfo inherited=child.GetMethod("GetDisplayName");
            MethodInfo declared=child.BaseType.GetMethod("GetDisplayName",BindingFlags.Public|BindingFlags.Instance|BindingFlags.DeclaredOnly);
            object target=Activator.CreateInstance(child);
            try
            {
                PatchProfiler.Shutdown();
                PatchProfiler.Enabled=true;
                PatchProfiler.OwnHarmonyId=id;
                external.Patch(inheritedFirst?inherited:declared,postfix:new HarmonyMethod(typeof(PatchProfilerIntegrationTests),nameof(TranslateA)));
                plugin.Patch(inheritedFirst?declared:inherited,postfix:new HarmonyMethod(typeof(PatchProfilerIntegrationTests),nameof(TranslateB)));
                string before=(string)inherited.Invoke(target,null);
                PatchProfiler.Initialize(plugin);
                Assert.AreEqual(before,inherited.Invoke(target,null));
                Assert.AreEqual(1,Calls(declared));
                late.Patch(inherited,postfix:new HarmonyMethod(typeof(PatchProfilerIntegrationTests),nameof(TranslateLate)));
                string after=(string)inherited.Invoke(target,null);
                StringAssert.Contains(after,":A"); StringAssert.Contains(after,":B"); StringAssert.Contains(after,":L");
                Assert.AreEqual(2,Calls(declared));
                var sample=PatchProfiler.TakeSnapshot(false).Single(s=>s.Key==MethodKey.Canonical(declared));
                StringAssert.Contains(sample.Owners,external.Id);
                Assert.IsFalse(sample.Owners.Split(',').Select(s=>s.Trim()).Contains(plugin.Id));
                Assert.AreEqual(1,Harmony.GetPatchInfo(declared).Prefixes.Count(p=>p.owner==PatchProfiler.TimingHarmonyId));
                for(int cycle=0;cycle<3;cycle++)
                {
                    Assert.IsTrue(PatchProfiler.Stop());
                    Assert.AreEqual(after,inherited.Invoke(target,null));
                    Assert.AreEqual(0,Calls(declared));
                    Assert.IsTrue(Harmony.GetPatchInfo(inherited).Owners.Contains(external.Id) || Harmony.GetPatchInfo(declared).Owners.Contains(external.Id));
                    PatchProfiler.Initialize(plugin);
                    PatchProfiler.Initialize(plugin);
                    Assert.AreEqual(after,inherited.Invoke(target,null));
                    Assert.AreEqual(1,Calls(declared));
                }
            }
            finally
            {
                PatchProfiler.Shutdown();
                plugin.UnpatchSelf();external.UnpatchSelf();late.UnpatchSelf();
            }
        }

        public class WorkTarget
        {
            [MethodImpl(MethodImplOptions.NoInlining)] public static int Add(int n) => n+1;
            [MethodImpl(MethodImplOptions.NoInlining)] public static T Echo<T>(T value) => value;
            [MethodImpl(MethodImplOptions.NoInlining)] public static int Recurse(int n) => n==0?0:1+Recurse(n-1);
            [MethodImpl(MethodImplOptions.NoInlining)] public static void Fail(bool fail) { if(fail) throw new InvalidOperationException("original failure"); }
            [MethodImpl(MethodImplOptions.NoInlining)] public static void Wait(ManualResetEventSlim entered,ManualResetEventSlim release) { entered.Set();release.Wait(); }
        }
        public static void Observe() { }
        public static IEnumerable<CodeInstruction> RejectTiming(IEnumerable<CodeInstruction> instructions, MethodBase original)
        {
            if(Harmony.GetPatchInfo(original).Owners.Any(o=>o.EndsWith(".PatchProfiler",StringComparison.Ordinal)))
                throw new InvalidOperationException("test rejects timing installation");
            return instructions;
        }

        [TestMethod]
        public void FailedInstallation_RollsBackOnlyTimingAndCanRetry()
        {
            string id="wsl.rollback."+Guid.NewGuid().ToString("N");
            var external=new Harmony(id+".external");
            var plugin=new Harmony(id);
            MethodInfo method=typeof(WorkTarget).GetMethod(nameof(WorkTarget.Add));
            try
            {
                PatchProfiler.Shutdown();PatchProfiler.Enabled=true;PatchProfiler.OwnHarmonyId=id;
                external.Patch(method,transpiler:new HarmonyMethod(typeof(PatchProfilerIntegrationTests),nameof(RejectTiming)));
                PatchProfiler.Initialize(plugin);
                Assert.AreEqual(4,method.Invoke(null,new object[]{3}));
                Assert.AreEqual(0,Calls(method));
                Assert.IsFalse(PatchProfiler.HasPendingCleanup);
                CollectionAssert.AreEquivalent(new[]{external.Id},Harmony.GetPatchInfo(method).Owners.ToArray());
                Assert.IsTrue(PatchProfiler.Stop());
                external.UnpatchSelf();
                external.Patch(method,prefix:new HarmonyMethod(typeof(PatchProfilerIntegrationTests),nameof(Observe)));
                PatchProfiler.Initialize(plugin);
                Assert.AreEqual(4,method.Invoke(null,new object[]{3}));
                Assert.AreEqual(1,Calls(method));
            }
            finally { PatchProfiler.Shutdown();external.UnpatchSelf();plugin.UnpatchSelf(); }
        }

        [TestMethod]
        public void ConstructedGenericMethod_ActuallyRunsUnderProfiler()
        {
            var external=new Harmony("wsl.generic.external."+Guid.NewGuid().ToString("N"));
            var plugin=new Harmony("wsl.generic.plugin."+Guid.NewGuid().ToString("N"));
            MethodInfo method=typeof(WorkTarget).GetMethod("Echo").MakeGenericMethod(typeof(int));
            try
            {
                PatchProfiler.Shutdown();PatchProfiler.Enabled=true;PatchProfiler.OwnHarmonyId=plugin.Id;
                external.Patch(method,prefix:new HarmonyMethod(typeof(PatchProfilerIntegrationTests),nameof(Observe)));
                PatchProfiler.Initialize(plugin);
                Assert.AreEqual(7,method.Invoke(null,new object[]{7}));
                Assert.AreEqual(1,PatchProfiler.TakeSnapshot(false).Sum(s=>s.Calls));
            }
            finally { PatchProfiler.Shutdown();external.UnpatchSelf();plugin.UnpatchSelf(); }
        }

        [TestMethod]
        public void BackgroundCallsAndSnapshots_DoNotLoseCountsOrThrow()
        {
            RunPatched(nameof(WorkTarget.Add),(method,plugin)=>
            {
                const int count=2000;
                Task[] workers=Enumerable.Range(0,4).Select(_=>Task.Run(()=>
                {
                    for(int i=0;i<count;i++)Assert.AreEqual(i+1,method.Invoke(null,new object[]{i}));
                })).ToArray();
                int total=0;
                while(!Task.WaitAll(workers,1))
                {
                    total+=PatchProfiler.TakeSnapshot(true).Sum(s=>s.Calls);
                    PatchProfiler.ResetFrameTimers();
                }
                total+=PatchProfiler.TakeSnapshot(true).Sum(s=>s.Calls);
                Assert.AreEqual(4*count,total);
            });
        }

        [TestMethod]
        public void InFlightCallAfterDisable_IsDiscardedAcrossNewSession()
        {
            RunPatched(nameof(WorkTarget.Wait),(method,plugin)=>
            {
                using(var entered=new ManualResetEventSlim())
                using(var release=new ManualResetEventSlim())
                {
                    Task task=Task.Run(()=>method.Invoke(null,new object[]{entered,release}));
                    try
                    {
                        Assert.IsTrue(entered.Wait(5000));
                        Assert.IsTrue(PatchProfiler.Stop());
                        PatchProfiler.Initialize(plugin);
                    }
                    finally { release.Set();Assert.IsTrue(task.Wait(5000)); }
                    Assert.AreEqual(0,Calls(method));
                    method.Invoke(null,new object[]{entered,release});
                    Assert.AreEqual(1,Calls(method));
                }
            });
        }

        [TestMethod]
        public void RecursionAndOriginalExceptions_PreserveBehavior()
        {
            RunPatched(nameof(WorkTarget.Recurse),(method,plugin)=>
            {
                Assert.AreEqual(4,method.Invoke(null,new object[]{4}));
                Assert.AreEqual(5,Calls(method));
            });
            RunPatched(nameof(WorkTarget.Fail),(method,plugin)=>
            {
                var ex=Assert.ThrowsException<TargetInvocationException>(()=>method.Invoke(null,new object[]{true}));
                Assert.IsInstanceOfType(ex.InnerException,typeof(InvalidOperationException));
                Assert.AreEqual("original failure",ex.InnerException.Message);
                Assert.AreEqual(1,Calls(method));
                method.Invoke(null,new object[]{false});
                Assert.AreEqual(2,Calls(method));
                Assert.AreEqual(1L,PatchProfiler.TakeSnapshot(false).Sum(x=>x.Exceptions));
            });
        }

        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void IgnoreRules_CanonicalAndLegacySurviveEnable(bool legacy)
        {
            var external=new Harmony("wsl.ignore.external."+Guid.NewGuid().ToString("N"));
            var plugin=new Harmony("wsl.ignore.plugin."+Guid.NewGuid().ToString("N"));
            MethodInfo method=typeof(WorkTarget).GetMethod(nameof(WorkTarget.Add));
            try
            {
                PatchProfiler.Shutdown();PatchProfiler.Enabled=true;PatchProfiler.OwnHarmonyId=plugin.Id;
                external.Patch(method,prefix:new HarmonyMethod(typeof(PatchProfilerIntegrationTests),nameof(Observe)));
                PatchProfiler.IgnoreMethods.Add(legacy?MethodKey.Legacy(method):MethodKey.Canonical(method));
                PatchProfiler.Initialize(plugin);
                Assert.AreEqual(2,method.Invoke(null,new object[]{1}));
                Assert.AreEqual(0,Calls(method));
                Assert.IsFalse(Harmony.GetPatchInfo(method).Owners.Contains(PatchProfiler.TimingHarmonyId));
            }
            finally { PatchProfiler.Shutdown();external.UnpatchSelf();plugin.UnpatchSelf(); }
        }

        public class MethodBaseTarget<T>
        {
            public MethodBaseTarget() { }
            [MethodImpl(MethodImplOptions.NoInlining)] public U Convert<U>(T input,U result) => result;
            public virtual int Virtual() => 1;
            public int Overload(int input) => input;
            public int Overload(string input) => input.Length;
        }
        public class MethodChild : MethodBaseTarget<int> { public override int Virtual() => 2; }
        public abstract class AbstractTarget { public abstract void Missing(); }

        [TestMethod]
        public void Resolver_PreservesConstructedGenericOverloadsOverridesAndConstructors()
        {
            var source=typeof(MethodChild).GetMethod("Convert").MakeGenericMethod(typeof(string));
            Assert.IsTrue(ProfilerTargetResolver.TryResolve(source,out MethodBase target,out string reason),reason);
            Assert.AreEqual(typeof(MethodBaseTarget<int>),target.ReflectedType);
            CollectionAssert.AreEqual(new[]{typeof(string)},((MethodInfo)target).GetGenericArguments());
            Assert.AreEqual("ok",target.Invoke(new MethodChild(),new object[]{1,"ok"}));
            Assert.IsTrue(ProfilerTargetResolver.TryResolve(typeof(MethodChild).GetMethod("Virtual"),out target,out reason));
            Assert.AreEqual(typeof(MethodChild),target.DeclaringType);
            var a=typeof(MethodChild).GetMethod("Overload",new[]{typeof(int)});
            var b=typeof(MethodChild).GetMethod("Overload",new[]{typeof(string)});
            Assert.AreNotEqual(ProfilerTargetResolver.Identity(a),ProfilerTargetResolver.Identity(b));
            Assert.IsTrue(ProfilerTargetResolver.TryResolve(typeof(MethodBaseTarget<int>).GetConstructor(Type.EmptyTypes),out target,out reason));
            Assert.IsInstanceOfType(target,typeof(ConstructorInfo));
            Assert.IsFalse(ProfilerTargetResolver.TryResolve(typeof(MethodBaseTarget<>).GetMethod("Convert"),out target,out reason));
            Assert.IsFalse(ProfilerTargetResolver.TryResolve(typeof(AbstractTarget).GetMethod("Missing"),out target,out reason));
            Assert.IsFalse(ProfilerTargetResolver.TryResolve(null,out target,out reason));
        }

        [TestMethod]
        public void DeferredSwitch_CoalescesBackgroundChangesAndCancelsBeforeStartup()
        {
            var queue=new DeferredProfilerSwitch();
            var changes=new List<bool>();
            int main=Thread.CurrentThread.ManagedThreadId;
            Action<bool> apply=value=>{Assert.AreEqual(main,Thread.CurrentThread.ManagedThreadId);changes.Add(value);};
            Task.Run(()=>{queue.Request(true);queue.Request(false);}).Wait();
            queue.Drain(false,apply);Assert.AreEqual(0,changes.Count);
            queue.Drain(true,apply);CollectionAssert.AreEqual(new[]{false},changes);
            queue.Request(false);queue.Drain(true,apply);Assert.AreEqual(1,changes.Count);
            queue.Request(true);queue.Drain(true,apply);Assert.AreEqual(2,changes.Count);
            queue.Request(false);queue.Cancel();queue.Drain(true,apply);Assert.AreEqual(2,changes.Count);
        }

        private static void RunPatched(string name,Action<MethodInfo,Harmony> test)
        {
            var external=new Harmony("wsl.work.external."+Guid.NewGuid().ToString("N"));
            var plugin=new Harmony("wsl.work.plugin."+Guid.NewGuid().ToString("N"));
            MethodInfo method=typeof(WorkTarget).GetMethod(name);
            try
            {
                PatchProfiler.Shutdown();PatchProfiler.Enabled=true;PatchProfiler.MinReportMs=0;
                PatchProfiler.OwnHarmonyId=plugin.Id;
                external.Patch(method,prefix:new HarmonyMethod(typeof(PatchProfilerIntegrationTests),nameof(Observe)));
                PatchProfiler.Initialize(plugin);test(method,plugin);
            }
            finally { PatchProfiler.Shutdown();external.UnpatchSelf();plugin.UnpatchSelf(); }
        }

        private static int Calls(MethodBase method)
        {
            return PatchProfiler.TakeSnapshot(false).Where(x=>x.Key==MethodKey.Canonical(method)).Sum(x=>x.Calls);
        }
    }
}
