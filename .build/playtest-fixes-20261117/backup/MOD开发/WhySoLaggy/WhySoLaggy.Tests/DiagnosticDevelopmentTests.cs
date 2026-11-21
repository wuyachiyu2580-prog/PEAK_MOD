using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace WhySoLaggy.Tests
{
    [TestClass,DoNotParallelize]
    public class DiagnosticDevelopmentTests
    {
        public TestContext TestContext { get; set; }
        [TestMethod]
        public void SyntheticTimingAndRpcBenchmark_RecordsThreeIndependentRounds()
        {
            var entry=Entry();int oldMain=DiagnosticContext.MainThreadId;DiagnosticContext.MainThreadId=Thread.CurrentThread.ManagedThreadId;
            try
            {
                for(int warm=0;warm<5000;warm++){var call=TimingLedger.Enter(entry);TimingLedger.Exit(ref call,null);}TimingLedger.Snapshot("unit",true);
                foreach(bool adaptive in new[]{false,true})for(int round=1;round<=3;round++)
                {
                    entry.Adaptive=adaptive;entry.Threshold=double.MaxValue;int gc=GC.CollectionCount(0);long before=GC.GetTotalMemory(false);var watch=Stopwatch.StartNew();
                    for(int i=0;i<100000;i++){if(i%100==0)TimingLedger.Begin(i/100);var call=TimingLedger.Enter(entry);TimingLedger.Exit(ref call,null);}
                    watch.Stop();var row=TimingLedger.Snapshot("unit",true).Single();Assert.AreEqual(100000L,row.Calls);
                    TestContext.WriteLine("Synthetic mode={0} round={1} calls={2} samples={3} elapsedMs={4:F3} nsPerCall={5:F1} gen0Delta={6} managedNetBytes={7}; not in-game A/B",adaptive?"adaptive":"exact",round,row.Calls,row.Samples,watch.Elapsed.TotalMilliseconds,watch.Elapsed.TotalMilliseconds*1000000/row.Calls,GC.CollectionCount(0)-gc,GC.GetTotalMemory(false)-before);
                }
                for(int round=1;round<=3;round++){RpcIngress.Reset();var watch=Stopwatch.StartNew();for(int i=0;i<100000;i++)RpcIngress.Record("RPC",i%4,64,1);watch.Stop();Assert.AreEqual(100000,RpcIngress.Drain().Sum(x=>x.Count));TestContext.WriteLine("Synthetic RPC round={0} count=100000 elapsedMs={1:F3}; not network throughput",round,watch.Elapsed.TotalMilliseconds);}
            }
            finally{RpcIngress.Reset();DiagnosticContext.MainThreadId=oldMain;}
        }
        [TestMethod]
        public void ExceptionAggregation_NormalizesAddressesAndRetainsOriginalTimestamps()
        {
            string a=ExceptionObserver.Normalize("Game.Foo.Update () [0x00aa] in C:\\Users\\Alice\\Foo.cs:12");
            string b=ExceptionObserver.Normalize("Game.Foo.Update () [0x00ff] in C:\\Users\\Bob\\Foo.cs:99");Assert.AreEqual(a,b);
            ExceptionObserver.Add("InvalidOperationException: private","Game.UniqueCheck.Update () [0x01]",10);
            ExceptionObserver.Add("InvalidOperationException: changed","Game.UniqueCheck.Update () [0x03]",12);
            var dictionary=(IDictionary)typeof(ExceptionObserver).GetField("errors",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);
            var match=dictionary.Values.Cast<ErrorSummary>().Single(x=>x.Stack.Contains("UniqueCheck"));Assert.AreEqual(2,match.Count);Assert.AreEqual(10d,match.First);Assert.AreEqual(12d,match.Last);
        }
        [TestCleanup]
        public void Cleanup(){TimingLedger.Remove("unit");DiagnosticContext.MainThreadId=0;}

        [TestMethod]
        public void PercentilesAndLowOne_HaveDefinedKnownResults()
        {
            var value=FrameWindow.Calculate(Enumerable.Range(1,200).Select(x=>(double)x));
            Assert.AreEqual(100.5,value.Mean);Assert.AreEqual(190d,value.P95);Assert.AreEqual(198d,value.P99);
            Assert.AreEqual(1000/199.5,value.Low1.Value,1e-9);
            Assert.IsNull(FrameWindow.Calculate(new[]{10d,20d,30d}).Low1);
            Assert.AreEqual(0,FrameWindow.Calculate(new[]{double.NaN,-1,double.PositiveInfinity}).Count);
        }
        [TestMethod]
        public void FrameWindow_ExpiresOldSamples()
        {var w=new FrameWindow();w.Add(0,200);w.Add(30,10);Assert.AreEqual(1,w.Snapshot(31).Count);Assert.AreEqual(10d,w.Snapshot(31).Mean);}

        [TestMethod]
        public void MemoryNetRate_PreservesNegativeChangesAndMissingBaseline()
        {Assert.IsNull(MemoryMetrics.NetRate(null,100,2));Assert.AreEqual(-.5,MemoryMetrics.NetRate(2048,1024,2).Value,1e-9);}

        [TestMethod]
        public void IntervalUnion_ClipsAndDeduplicatesNestedScopes()
        {
            long unit=Stopwatch.Frequency;
            var spans=new[]{Tuple.Create(unit,4*unit),Tuple.Create(2*unit,3*unit),Tuple.Create(5*unit,7*unit),Tuple.Create(-unit,2*unit)};
            Assert.AreEqual(5000,TimingLedger.UnionMilliseconds(spans,0,6*unit),1e-6);
        }

        private static TimingEntry Entry()=>TimingLedger.Register("unit",typeof(DiagnosticDevelopmentTests).GetMethod(nameof(Target)),"test");
        [MethodImpl(MethodImplOptions.NoInlining)]public static int Target(int input)=>input+1;

        [TestMethod]
        public void TimingExit_RecordsExceptionOnceAndIgnoresOldGeneration()
        {
            var entry=Entry();var call=TimingLedger.Enter(entry);TimingLedger.Exit(ref call,new InvalidOperationException());TimingLedger.Exit(ref call,null);
            var value=TimingLedger.Snapshot("unit",true).Single();Assert.AreEqual(1L,value.Calls);Assert.AreEqual(1L,value.Samples);Assert.AreEqual(1L,value.Exceptions);
            call=TimingLedger.Enter(entry);TimingLedger.Remove("unit");Entry();TimingLedger.Exit(ref call,null);Assert.AreEqual(0,TimingLedger.Snapshot("unit",true).Count);
        }

        [TestMethod]
        public void ConcurrentSnapshots_CountCallsAndSamplesTogether()
        {
            var entry=Entry();const int perThread=4000;long calls=0,samples=0;
            var tasks=Enumerable.Range(0,4).Select(_=>Task.Run(()=>{for(int i=0;i<perThread;i++){var call=TimingLedger.Enter(entry);TimingLedger.Exit(ref call,null);}})).ToArray();
            while(!Task.WaitAll(tasks,1)){foreach(var row in TimingLedger.Snapshot("unit",true)){Assert.AreEqual(row.Calls,row.Samples);calls+=row.Calls;samples+=row.Samples;}}
            foreach(var row in TimingLedger.Snapshot("unit",true)){calls+=row.Calls;samples+=row.Samples;}
            Assert.AreEqual(4L*perThread,calls);Assert.AreEqual(calls,samples);
        }

        [TestMethod]
        public void BackgroundWait_DoesNotBecomeMainThreadCoverage()
        {
            DiagnosticContext.MainThreadId=Thread.CurrentThread.ManagedThreadId;TimingLedger.Begin(1);var entry=Entry();long start=Stopwatch.GetTimestamp();
            Task.Run(()=>{var state=TimingLedger.Enter(entry);Thread.Sleep(25);TimingLedger.Exit(ref state,null);}).Wait();
            var frame=TimingLedger.Seal(1,start,Stopwatch.GetTimestamp());Assert.AreEqual(0d,frame.CoveredMs);Assert.AreEqual(0,frame.Methods.Count);
            Assert.IsTrue(TimingLedger.Snapshot("unit",true).Single().BackgroundMs>=10);
        }

        [TestMethod]
        public void Queue_EnforcesBytesWithoutLoweringConfiguredCapacity()
        {
            var q=new BoundedConcurrentQueue<string>(2048,x=>x.Length,10);q.Enqueue("aaaa");q.Enqueue("bbbb");var r=q.Enqueue("ccccccc");
            Assert.AreEqual(2,r.DroppedCount);Assert.AreEqual(7L,q.Bytes);Assert.AreEqual(2048,q.Capacity);Assert.IsTrue(q.TryDequeue(out var value));Assert.AreEqual("ccccccc",value);Assert.AreEqual(0L,q.Bytes);
            Assert.IsTrue(q.Enqueue(new string('x',11)).Dropped);Assert.AreEqual(0,q.Count);
        }
        [TestMethod]
        public void QueueCapacityShrink_UpdatesRetainedBytes()
        {var q=new BoundedConcurrentQueue<string>(3,x=>x.Length,100);q.Enqueue("a");q.Enqueue("bb");q.Enqueue("ccc");q.Capacity=1;Assert.AreEqual(3L,q.Bytes);Assert.AreEqual(2L,q.TakeWindowStats().Dropped);q.Clear();Assert.AreEqual(0L,q.Bytes);}

        [TestMethod]
        public void RpcIngress_BurstTotalsSurviveDetailSampling()
        {
            RpcIngress.Reset();int retained=0;for(int i=0;i<10000;i++)if(RpcIngress.Record("RPC",7,32,1))retained++;
            var count=RpcIngress.Drain().Single();Assert.AreEqual(10000,count.Count);Assert.AreEqual(320000L,count.Bytes);Assert.AreEqual(8,retained);Assert.AreEqual(9992L,RpcIngress.SampledOut);RpcIngress.Reset();
        }
        [TestMethod]
        public void RpcIngress_KeyOverflowRetainsEveryCount()
        {RpcIngress.Reset();for(int i=0;i<12000;i++)RpcIngress.Record("Method"+i,i,1,1);var rows=RpcIngress.Drain();Assert.IsTrue(rows.Length<=4097);Assert.AreEqual(12000,rows.Sum(x=>x.Count));RpcIngress.Reset();}
        [TestMethod]
        public void RpcSnapshot_DetachesMutableBuffersAndBoundsText()
        {
            byte[] bytes={1,2,3};float[] floats={1,2};object[] input={bytes,floats,new string('x',500),new object()};var copy=RpcIngress.CopyArgs(input);
            bytes[0]=99;floats[0]=99;Assert.AreEqual((byte)1,((byte[])copy[0])[0]);Assert.AreEqual(1f,((float[])copy[1])[0]);Assert.IsTrue(((string)copy[2]).Length<=128);Assert.IsInstanceOfType(copy[3],typeof(string));
        }

        [TestMethod]
        public void Trigger_RespectsBaselineEligibilityCooldownAndHourLimit()
        {
            var p=new TriggerPolicy();Assert.IsFalse(p.Observe(0,200,10,50,false));Assert.IsTrue(p.Observe(1,200,10,50,true));
            p.Started(1);p.Ended(2);Assert.IsFalse(p.Observe(121,200,10,50,true));Assert.IsTrue(p.Observe(122,200,10,50,true));
            for(int i=0;i<9;i++)p.Started(123+i);Assert.IsFalse(p.Observe(200,200,10,50,true));Assert.IsTrue(p.Observe(3800,200,10,50,true));
        }
        [TestMethod]
        public void Trigger_RequiresThreeRecentRelativeSpikes()
        {var p=new TriggerPolicy();Assert.IsFalse(p.Observe(0,60,15,50,true));Assert.IsFalse(p.Observe(.5,60,15,50,true));Assert.IsTrue(p.Observe(1,60,15,50,true));Assert.IsFalse(p.Observe(4,60,15,50,true));}

        [TestMethod]
        public void AnonymousExport_AliasesActorAcrossNameChangesButSeparatesEpoch()
        {
            var a=new DiagnosticAnonymizer();Func<int,int,string,StructuredEvent> row=(epoch,actor,name)=>new StructuredEvent{Type=EventType.RpcCall,Fields=new Dictionary<string,object>{{"RoomEpoch",epoch},{"SenderActor",actor},{"SenderName",name},{"RoomName","PRIVATE ROOM"},{"ArgsSummary","SECRET"},{"TargetPath",@"C:\Users\Secret"},{"Name","SECRET"}}};
            var first=a.Sanitize(row(1,1,"Alice"));var renamed=a.Sanitize(row(1,1,"Bob"));var second=a.Sanitize(row(1,2,"Alice"));var rejoined=a.Sanitize(row(2,1,"Alice"));
            Assert.AreEqual(first.Fields["SenderName"],renamed.Fields["SenderName"]);Assert.AreNotEqual(first.Fields["SenderName"],second.Fields["SenderName"]);Assert.AreNotEqual(first.Fields["SenderName"],rejoined.Fields["SenderName"]);
            string json=EventCodec.EventJson(first);foreach(var secret in new[]{"Alice","PRIVATE ROOM","SECRET","Secret"})Assert.IsFalse(json.Contains(secret));
        }
        [TestMethod]
        public void AnonymousStack_RetainsMethodButStripsArgumentsAndPrivatePaths()
        {string safe=DiagnosticAnonymizer.SafeStack("at Game.Item.Update(string secret) in C:\\Users\\Alice\\game.cs:12\nGame.Other.Tick () [0x001]");StringAssert.Contains(safe,"Game.Item.Update()");Assert.IsFalse(safe.Contains("Alice"));Assert.IsFalse(safe.Contains("secret"));}

        [TestMethod]
        public void FrozenEvents_DetachCallerDictionary()
        {var fields=new Dictionary<string,object>{{"Name","before"}};var frozen=EventCodec.Freeze(new StructuredEvent{Fields=fields});fields["Name"]="after";Assert.AreEqual("before",frozen.Fields["Name"]);Assert.AreEqual(2,frozen.Fields["SchemaVersion"]);}

        [TestMethod]
        public void CapturePayload_SharedBudgetAndExportReferencesReleaseExactlyOnce()
        {
            long before=CapturePayloadBudget.Bytes;var rings=Enumerable.Range(0,4).Select(_=>new EventRing(16*1024*1024)).ToArray();
            var e=new StructuredEvent{Fields=new Dictionary<string,object>{{"Message",new string('x',20000)}}};
            foreach(var ring in rings)for(int i=0;i<1000;i++)ring.Add(e,0,20);
            Assert.IsTrue(CapturePayloadBudget.Bytes<=CapturePayloadBudget.Limit);Assert.IsTrue(rings.Sum(x=>x.Evicted)>0);
            var record=rings[0].Detach();record.Retain();record.Release();Assert.IsTrue(record.Events.Length>0);record.Release();foreach(var ring in rings)ring.Clear();Assert.AreEqual(before,CapturePayloadBudget.Bytes);
        }

        private sealed class LazySequence:IEnumerable
        {public IEnumerator GetEnumerator(){throw new InvalidOperationException("must never enumerate");}}
        private sealed class HostileList:ArrayList
        {public override object this[int index]{get=>throw new InvalidOperationException();set=>throw new InvalidOperationException();}}
        [TestMethod]
        public void Formatter_BoundsCyclesAndNeverRunsLazyEnumerators()
        {StringAssert.Contains(ValueFormatter.Format(new LazySequence()),"omitted");var list=new ArrayList();list.Add(list);StringAssert.Contains(ValueFormatter.Format(list),"cycle");Assert.IsTrue(ValueFormatter.Format(Enumerable.Range(0,1000000).ToArray()).Length<160);}

        [TestMethod]
        public void PatchCycleDetection_DistinguishesSharedOwnersFromCycles()
        {var graph=new Dictionary<string,HashSet<string>>{{"A",new HashSet<string>{"B"}},{"B",new HashSet<string>()}};Assert.IsFalse(HarmonyScanner.HasCycle(graph));graph["B"].Add("A");Assert.IsTrue(HarmonyScanner.HasCycle(graph));}

        [TestMethod]
        public void Writer_RejectsOverBudgetAndRecoversAfterWorkerFailure()
        {
            DiagnosticIO.Start();Assert.IsFalse(DiagnosticIO.Enqueue(()=>{},4*1024*1024+1));
            DiagnosticIO.Enqueue(()=>{throw new IOException("simulated disk failure");},32,true);int result=0;Assert.IsTrue(DiagnosticIO.Barrier(()=>result=1));Assert.AreEqual(1,result);StringAssert.Contains(DiagnosticIO.Error,"simulated disk failure");Assert.IsTrue(DiagnosticIO.Stop());
        }
        [TestMethod]
        public void DiagnosticZip_ContainsStructuredFilesAndNoPrivatePayload()
        {
            string dir=Path.Combine(Path.GetTempPath(),"WhySoLaggy.Tests",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);
            try
            {
                StructuredLogger.Initialize(dir);CaptureController.Initialize(dir,new Dictionary<string,object>(),new string[0]);
                StructuredLogger.WriteEvent(new StructuredEvent{Type=EventType.RpcCall,Fields=new Dictionary<string,object>{{"SenderActor",42},{"SenderName","PrivateNickname"},{"ArgsSummary","PrivateParameter"},{"TargetPath",@"C:\Users\PrivateUser"}}});
                StructuredLogger.WriteEvent(new StructuredEvent{Type=EventType.FrameSample,FrameNumber=42,Fields=new Dictionary<string,object>{{"AvgFrameMs",5d},{"CoveredMs",2d},{"SelfMainMs",.2d},{"SceneState","Gameplay"},{"Scene","PrivateSceneName"},{"Validity","Complete"}}});
                StructuredLogger.WriteEvent(new StructuredEvent{Type=EventType.PluginTiming,FrameNumber=42,Fields=new Dictionary<string,object>{{"MethodId","module:Update"},{"Name","Example.Update()"},{"PluginGuid","example.plugin"},{"TimingKind","window inclusive"},{"WindowStartSeconds",0d},{"WindowEndSeconds",1d},{"CallCount",100L},{"SampleCount",10L},{"SampleCoverage",.1},{"TotalMs",12.5}}});
                CaptureController.ExportLatest(false,false);Assert.IsTrue(DiagnosticIO.Barrier(()=>{},10000));
                string path=Directory.GetFiles(Path.Combine(dir,"WhySoLaggy","diagnostics"),"*.zip").Single();
                using(var zip=ZipFile.OpenRead(path))
                {foreach(var name in new[]{"summary.txt","manifest.json","events.jsonl","frames.csv","calls.csv","network.csv","exceptions.csv","harmony_patches.csv"})Assert.IsNotNull(zip.GetEntry(name));
                    foreach(var entry in zip.Entries)using(var reader=new StreamReader(entry.Open()))
                    {
                        string content=reader.ReadToEnd();foreach(var secret in new[]{"PrivateNickname","PrivateParameter","PrivateUser","PrivateSceneName"})Assert.IsFalse(content.Contains(secret),entry.FullName);
                        if(entry.FullName=="manifest.json"){StringAssert.Contains(content,"\"RetainedFrameCount\":1");StringAssert.Contains(content,"\"DroppedFrames\":0");StringAssert.Contains(content,"RetainedFrameStartSeconds");StringAssert.Contains(content,"RetainedFrameEndSeconds");}
                        if(entry.FullName=="summary.txt"){StringAssert.Contains(content,"Retained frames: 1");StringAssert.Contains(content,"Dropped frames / critical events / details: 0 / 0 / 0");StringAssert.Contains(content,"spike details are subsets");}
                        if(entry.FullName=="frames.csv"){Assert.AreEqual(2,content.Split(new[]{'\n'},StringSplitOptions.RemoveEmptyEntries).Length);StringAssert.Contains(content,"FrameSample");}
                        if(entry.FullName=="calls.csv"){Assert.AreEqual(2,content.Split(new[]{'\n'},StringSplitOptions.RemoveEmptyEntries).Length);StringAssert.Contains(content,"window inclusive");StringAssert.Contains(content,"12.5");}
                    }}
            }
            finally{CaptureController.Shutdown();StructuredLogger.Shutdown();Directory.Delete(dir,true);}
        }

        public static void ThrowExternal(){throw new ApplicationException("external original object");}
        [TestMethod]
        public void CaptureStop_ReleasesAutomaticHooksAndPreservesNewManualIntent()
        {
            string dir=Path.Combine(Path.GetTempPath(),"wsl-state-"+Guid.NewGuid());Directory.CreateDirectory(dir);bool manualIntent=false,applied=false;
            try
            {
                StructuredLogger.Initialize(dir);CaptureController.Initialize(dir,new Dictionary<string,object>(),new string[0]);
                CaptureController.ApplyHooks=(capture,owners,errors)=>applied=manualIntent||CaptureController.WantsPlugin;
                CaptureController.Begin(false,"test");Assert.IsTrue(applied);Assert.IsTrue(CaptureController.Busy);
                manualIntent=true;CaptureController.Finish("User stopped");Assert.IsTrue(applied);Assert.IsFalse(CaptureController.Busy);Assert.AreEqual(CapturePhase.Cooldown,CaptureController.Phase);
                Assert.IsTrue(DiagnosticIO.Barrier(()=>{},10000));
            }
            finally{CaptureController.ApplyHooks=null;CaptureController.Shutdown();StructuredLogger.Shutdown();Directory.Delete(dir,true);}
        }
        [TestMethod]
        public void WriterPressure_PreservesImportantJobsAndBoundsQueueBytes()
        {
            DiagnosticIO.Start();using(var entered=new ManualResetEventSlim())using(var release=new ManualResetEventSlim())
            {
                int important=0;DiagnosticIO.Enqueue(()=>{entered.Set();release.Wait(10000);},1,true);Assert.IsTrue(entered.Wait(3000));
                try{for(int i=0;i<6000;i++)DiagnosticIO.Enqueue(()=>{},1024);Assert.IsTrue(DiagnosticIO.PendingBytes<=4*1024*1024);Assert.IsTrue(DiagnosticIO.Enqueue(()=>important++,128,true));}
                finally{release.Set();}
                Assert.IsTrue(DiagnosticIO.Barrier(()=>{},10000));Assert.AreEqual(1,important);Assert.IsTrue(DiagnosticIO.Stop());
            }
        }
        [TestMethod]
        public void RepeatedProbeReload_InstallsOneCallbackAndLeavesExternalOwner()
        {
            string path=Path.Combine(Path.GetTempPath(),"wsl-reload-"+Guid.NewGuid()+".json");var h=new Harmony("unit.probe.repeat."+Guid.NewGuid());
            var external=new Harmony("unit.probe.other."+Guid.NewGuid());var method=typeof(DiagnosticDevelopmentTests).GetMethod(nameof(Target));
            try
            {
                FieldProbe.Shutdown();FieldProbe.Enabled=true;FieldProbe.RulesFilePath=path;File.WriteAllText(path,"{\"rules\":[{\"target\":\"DiagnosticDevelopmentTests.Target\",\"fields\":[\"__arg0\"]}]}");
                external.Patch(method,prefix:new HarmonyMethod(typeof(PatchProfilerIntegrationTests),nameof(PatchProfilerIntegrationTests.Observe)));FieldProbe.Initialize(h);
                for(int i=0;i<4;i++){Assert.IsTrue(FieldProbe.TryReload(h,out string error),error);var info=Harmony.GetPatchInfo(method);Assert.AreEqual(1,info.Prefixes.Count(x=>x.PatchMethod.DeclaringType==typeof(FieldProbe)));Assert.IsTrue(info.Owners.Contains(external.Id));Assert.AreEqual(2,method.Invoke(null,new object[]{1}));}
            }
            finally{FieldProbe.Shutdown();FieldProbe.Enabled=false;external.UnpatchSelf();File.Delete(path);}
        }
        [TestMethod]
        public void TraditionalChinese_TranslatesNewConfigurationAndHudText()
        {
            Assert.AreEqual("自動卡頓錄製",UiText.Traditional("自动卡顿录制"));
            Assert.AreEqual("預設關閉，記憶體與網絡監控",UiText.Traditional("预设关闭，内存与网络监控"));
            Assert.AreEqual("保留幀時間範圍（單調秒）",UiText.Traditional("保留帧时间范围（单调秒）"));
            foreach(var entry in ModConfigLocalization.Entries){Assert.IsFalse(string.IsNullOrWhiteSpace(UiText.Traditional(entry.ChineseName)));Assert.IsFalse(string.IsNullOrWhiteSpace(UiText.Traditional(entry.ChineseDescription)));}
        }
        private struct ForeignNode { }
        [TestMethod]
        public void PlayerLoopInsertion_PreservesForeignNodesAndRemainsIdempotent()
        {
            var loop=new UnityEngine.LowLevel.PlayerLoopSystem{type=typeof(object),subSystemList=new[]{new UnityEngine.LowLevel.PlayerLoopSystem{type=typeof(UnityEngine.PlayerLoop.Initialization),subSystemList=new[]{new UnityEngine.LowLevel.PlayerLoopSystem{type=typeof(ForeignNode)}}}}};
            Assert.IsTrue(FrameCoordinator.Insert(ref loop));Assert.IsTrue(FrameCoordinator.Insert(ref loop));
            var children=loop.subSystemList[0].subSystemList;Assert.AreEqual(2,children.Length);Assert.AreEqual(typeof(ForeignNode),children[1].type);Assert.IsNotNull(children[0].updateDelegate);
        }
        [TestMethod]
        public void Rate_UsesElapsedTimeAfterLongStall()
        {Assert.AreEqual(25d,NetworkAbuseDetector.Rate(100,4));Assert.AreEqual(100d,NetworkAbuseDetector.Rate(100,1));}
        [TestMethod]
        public void ProbeReload_InvalidExpressionPreservesPreviouslyInstalledHooks()
        {
            string path=Path.Combine(Path.GetTempPath(),"wsl-probe-"+Guid.NewGuid()+".json");var h=new Harmony("unit.probe."+Guid.NewGuid());
            try
            {
                FieldProbe.Shutdown();FieldProbe.Enabled=true;FieldProbe.RulesFilePath=path;
                File.WriteAllText(path,"{\"rules\":[{\"target\":\"DiagnosticDevelopmentTests.Target\",\"fields\":[\"__arg0\"]}]}");
                FieldProbe.Initialize(h);var method=typeof(DiagnosticDevelopmentTests).GetMethod(nameof(Target));Assert.IsTrue(Harmony.GetPatchInfo(method).Owners.Contains(h.Id));
                File.WriteAllText(path,"{\"rules\":[{\"target\":\"MissingType.Target\",\"fields\":[\"__arg0\"]}]}");
                Assert.IsFalse(FieldProbe.TryReload(h,out var error));Assert.IsFalse(string.IsNullOrEmpty(error));Assert.IsTrue(Harmony.GetPatchInfo(method).Owners.Contains(h.Id));
                Assert.AreEqual(3,method.Invoke(null,new object[]{2}));
            }
            finally{FieldProbe.Shutdown();FieldProbe.Enabled=false;h.UnpatchSelf();File.Delete(path);}
        }
        [TestMethod]
        public void TracerReload_InstallationFailurePreservesPreviouslyInstalledHooks()
        {
            var h=new Harmony("unit.trace."+Guid.NewGuid());var external=new Harmony("unit.reject."+Guid.NewGuid());var method=typeof(DiagnosticDevelopmentTests).GetMethod(nameof(Target));
            try
            {
                MethodTracer.Shutdown();MethodTracer.TraceMethodNames="DiagnosticDevelopmentTests.Target";MethodTracer.Initialize(h);
                Assert.IsTrue(Harmony.GetPatchInfo(method)?.Owners.Contains(h.Id)==true,"Initial trace was not installed");
                external.Patch(method,transpiler:new HarmonyMethod(typeof(DiagnosticDevelopmentTests),nameof(RejectStaging)));
                Assert.IsFalse(MethodTracer.TryReload(h,"DiagnosticDevelopmentTests.Target",out var error));Assert.IsFalse(string.IsNullOrEmpty(error));
                Assert.IsTrue(Harmony.GetPatchInfo(method).Owners.Contains(h.Id),"Original owner removed: "+string.Join(",",Harmony.GetPatchInfo(method).Owners)+"; "+error);Assert.IsFalse(Harmony.GetPatchInfo(method).Owners.Any(x=>x.EndsWith(".Staging")),"Staging owner survived");Assert.AreEqual(3,method.Invoke(null,new object[]{2}));
            }
            finally{external.UnpatchSelf();MethodTracer.Shutdown();}
        }
        public static IEnumerable<CodeInstruction> RejectStaging(IEnumerable<CodeInstruction> instructions,MethodBase original)
        {if(Harmony.GetPatchInfo(original).Owners.Any(x=>x.EndsWith(".Staging")))throw new InvalidOperationException("Reject staged hook");return instructions;}
        [TestMethod]
        public void ExternalPrefixException_PreservesExceptionAndCountsOnce()
        {
            var external=new Harmony("unit.external."+Guid.NewGuid());var profiler=new Harmony("unit.profiler."+Guid.NewGuid());var method=typeof(DiagnosticDevelopmentTests).GetMethod(nameof(Target));
            try{PatchProfiler.Shutdown();PatchProfiler.Enabled=true;PatchProfiler.OwnHarmonyId=profiler.Id;external.Patch(method,prefix:new HarmonyMethod(typeof(DiagnosticDevelopmentTests),nameof(ThrowExternal)));PatchProfiler.Initialize(profiler);
                var ex=Assert.ThrowsException<TargetInvocationException>(()=>method.Invoke(null,new object[]{1}));Assert.IsInstanceOfType(ex.InnerException,typeof(ApplicationException));Assert.AreEqual("external original object",ex.InnerException.Message);var row=PatchProfiler.TakeSnapshot(false).Single();Assert.AreEqual(1,row.Calls);Assert.AreEqual(1L,row.Exceptions);}
            finally{PatchProfiler.Shutdown();external.UnpatchSelf();}
        }
    }
}
