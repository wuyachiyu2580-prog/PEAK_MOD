using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace WhySoLaggy.Tests
{
    [TestClass,DoNotParallelize]
    public class PlaytestFixTests
    {
        private static StructuredEvent Event(EventType type,double time,string text=null)=>new StructuredEvent
        {Type=type,Timestamp="test",FrameNumber=42,Fields=new Dictionary<string,object>{{"MonotonicSeconds",time},{"Sequence",42L},{"RpcMethod",text??"Rpc"},{"Count",1L}}};
        private static CaptureLossLedger Ledger(params double[] times)
        {var l=new CaptureLossLedger();foreach(var t in times)l.Add(t,CaptureCategory.Detail,CaptureLossReason.Capacity);return l;}

        [TestMethod]
        public void LossLedger_UsesOriginalEventTimeAndInclusiveRequestedWindow()
        {
            var ledger=Ledger(1,19.9,20,20.2,40,40.1);
            var range=ledger.Snapshot(20,40).Total;
            Assert.AreEqual(3L,range.Lower);Assert.AreEqual(3L,range.Upper);Assert.AreEqual("Exact",range.Quality);
            Assert.AreEqual(0L,ledger.Snapshot(50,70).Total.Upper);
        }
        [TestMethod]
        public void LossLedger_PartialBucketReportsBoundsWithoutInventingExactCount()
        {
            var ledger=Ledger(10.1,10.3,10.5,10.9);
            var range=ledger.Snapshot(10.4,20).Total;
            Assert.AreEqual(1L,range.Lower);Assert.AreEqual(3L,range.Upper);Assert.AreEqual("Bounded",range.Quality);
            var middle=ledger.Snapshot(10.4,10.6).Total;Assert.AreEqual(0L,middle.Lower);Assert.AreEqual(2L,middle.Upper);
        }
        [TestMethod]
        public void LossLedger_SnapshotsAreIndependentAndOldCoverageBecomesUnknown()
        {
            var ledger=Ledger(0,1,2);var frozen=ledger.Snapshot(0,2);
            for(int i=128;i<20000;i++)ledger.Add(i,CaptureCategory.Detail,CaptureLossReason.Capacity);
            Assert.AreEqual(3L,frozen.Total.Upper);Assert.IsTrue(ledger.Snapshot(0,20).Total.Unknown);
            var recent=ledger.Snapshot(19980,19999).Total;Assert.IsFalse(recent.Unknown);Assert.AreEqual(20L,recent.Lower);
            Assert.AreEqual(19875L,ledger.Lifetime(0,20000).Total.Lower);
        }
        [TestMethod]
        public void LossLedger_OutOfOrderOldEventNeverOverwritesNewEvidence()
        {
            var ledger=Ledger(130,2);Assert.IsTrue(ledger.Snapshot(0,3).Total.Unknown);
            var recent=ledger.Snapshot(129,131).Total;Assert.IsFalse(recent.Unknown);Assert.AreEqual(1L,recent.Lower);
        }
        [TestMethod]
        public void PreRollCopy_InheritsOnlyRequestedLossAndSeparatesDestinationDrops()
        {
            long before=CapturePayloadBudget.Bytes;var source=new EventRing(200000);var target=new EventRing(1000000);CaptureRecord record=null;
            try
            {
                source.Add(Event(EventType.RpcCall,1,new string('x',60000)),1,20);
                source.Add(Event(EventType.RpcCall,25,new string('x',60000)),25,20);
                source.Add(Event(EventType.RpcCall,30),30,20);source.CopyTo(target,40,20);
                Assert.ThrowsException<InvalidOperationException>(()=>source.CopyTo(target,40,20));
                target.Add(Event(EventType.NetworkSample,41,new string('x',30000)),41,90);
                record=target.Detach();source.Clear();
                Assert.AreEqual(1L,record.PreRollLoss.Total.Lower);Assert.AreEqual(1L,record.RecordingLoss.Total.Lower);
                Assert.AreEqual(20d,record.PreRollLoss.Start);Assert.AreEqual(40d,record.PreRollLoss.End);
                Assert.AreEqual(1L,record.DroppedNetwork);Assert.AreEqual(0L,record.DroppedDetails);
                StringAssert.Contains(EventCodec.Json(CaptureController.LossManifest(record)),"PreRoll");
            }
            finally{source.Clear();target.Clear();record?.Release();}Assert.AreEqual(before,CapturePayloadBudget.Bytes);
        }
        [TestMethod]
        public void NaturalExpiry_DoesNotCreateLossEvidence()
        {
            long before=CapturePayloadBudget.Bytes;var a=new EventRing(1000000);var b=new EventRing(1000000);CaptureRecord r=null;
            try{a.Add(Event(EventType.NetworkSample,1),1,20);a.Add(Event(EventType.NetworkSample,40),40,20);a.CopyTo(b,40,20);r=b.Detach();Assert.AreEqual(0L,r.PreRollLoss.Total.Upper);Assert.AreEqual(0L,r.RecordingLoss.Total.Upper);Assert.AreEqual(1,r.Count);}
            finally{a.Clear();b.Clear();r?.Release();}Assert.AreEqual(before,CapturePayloadBudget.Bytes);
        }
        [TestMethod]
        public void NetworkPacked_PreservesRuntimeTypesNullAbsenceAndLargeIntegers()
        {
            var e=Event(EventType.NetworkSample,1);e.Fields["Sequence"]=9007199254740993L;e.Fields["Count"]=long.MaxValue;
            e.Fields["Rate"]=.125d;e.Fields["CpuFrameMs"]=null;e.Fields["IncomingCommands"]=ulong.MaxValue;
            e.Fields["Ping"]=(short)17;e.Fields["QueueDelayMs"]=.25f;
            Assert.IsTrue(CaptureNetwork.Supports(e));
            var packed=new CaptureNetwork(e,new CaptureNetworkIdentity(e.Fields,CaptureNetworkIdentity.KeyOf(e.Fields)));
            var output=packed.Expand();Assert.AreEqual(e.Fields.Count,output.Fields.Count);Assert.AreEqual(e.FrameNumber,output.FrameNumber);
            foreach(var field in e.Fields){Assert.AreEqual(field.Value,output.Fields[field.Key],field.Key);Assert.AreEqual(field.Value?.GetType(),output.Fields[field.Key]?.GetType(),field.Key);}
            Assert.IsFalse(output.Fields.ContainsKey("GpuFrameMs"));
        }
        [DataTestMethod]
        [DataRow("NewField",1)]
        [DataRow("Rate",true)]
        [DataRow("RpcMethod",null)]
        public void NetworkPacked_UnknownSchemaFallsBackWithoutFieldLoss(string key,object value)
        {
            var e=Event(EventType.NetworkSample,1);e.Fields[key]=value;Assert.IsFalse(CaptureNetwork.Supports(e));
            long before=CapturePayloadBudget.Bytes;var ring=new EventRing(1000000);CaptureRecord record=null;
            try{ring.Add(e,1,20);record=ring.Detach();Assert.AreEqual(0,record.PackedNetwork.Length);Assert.AreEqual(value,record.Enumerate().Single().Fields[key]);}
            finally{record?.Release();ring.Clear();}Assert.AreEqual(before,CapturePayloadBudget.Bytes);
        }
        [TestMethod]
        public void NetworkIdentity_LengthPrefixPreventsSeparatorCollisions()
        {
            var a=Event(EventType.NetworkSample,1);a.Fields["SessionId"]="a1:b";a.Fields["CaptureId"]="c";
            var b=Event(EventType.NetworkSample,1);b.Fields["SessionId"]="a";b.Fields["CaptureId"]="b1:c";
            Assert.AreNotEqual(CaptureNetworkIdentity.KeyOf(a.Fields),CaptureNetworkIdentity.KeyOf(b.Fields));
        }
        [TestMethod]
        public void NetworkFlood_PreservesProtectedEventsAndReleasesFrozenIdentities()
        {
            long before=CapturePayloadBudget.Bytes;var ring=new EventRing(1000000);var copy=new EventRing(1000000);CaptureRecord record=null;
            try
            {
                ring.Add(Event(EventType.ExceptionSummary,1),1,90);ring.Add(Event(EventType.SpikeFrame,1),1,90);
                for(int i=0;i<1000;i++)ring.Add(Event(EventType.NetworkSample,2+i*.001),3,90);
                ring.CopyTo(copy,3,20);record=copy.Detach();ring.Clear();
                Assert.AreEqual(2,record.Events.Length);Assert.IsTrue(record.PreRollLoss.Total.Lower>0);Assert.AreEqual(0L,record.DroppedCritical);
                Assert.AreSame(record.PackedNetwork[0].Identity,record.PackedNetwork[1].Identity);
                record.Retain();long charged=CapturePayloadBudget.Bytes;record.Release();Assert.AreEqual(charged,CapturePayloadBudget.Bytes);
            }
            finally{ring.Clear();copy.Clear();record?.Release();}Assert.AreEqual(before,CapturePayloadBudget.Bytes);
        }
        [TestMethod]
        public void SharedBudgetLoss_IsClassifiedAndUnavailableLedgerIsUnknown()
        {
            long before=CapturePayloadBudget.Bytes;var ring=new EventRing(1000000);CaptureRecord record=null;long reserved=0;
            try
            {
                ring.Add(Event(EventType.NetworkSample,1),1,20);
                reserved=CapturePayloadBudget.Limit-CapturePayloadBudget.Bytes;Assert.IsTrue(CapturePayloadBudget.Reserve(reserved));
                ring.Add(Event(EventType.NetworkSample,2),2,20);record=ring.Detach();
                Assert.AreEqual(1L,record.RecordingLoss.Ranges[(int)CaptureCategory.Network*3+(int)CaptureLossReason.SharedBudget].Lower);
                var unavailable=new EventRing(1000000);
                try{unavailable.Add(Event(EventType.RpcCall,3),3,20);var r=unavailable.Detach();try{Assert.IsTrue(r.RecordingLoss.Total.Unknown);}finally{r.Release();}}
                finally{unavailable.Clear();}
            }
            finally{CapturePayloadBudget.Release(reserved);record?.Release();ring.Clear();}Assert.AreEqual(before,CapturePayloadBudget.Bytes);
        }
        [TestMethod]
        public void PhaseProfiler_OffByDefaultAutoStopsAndRequiresRearm()
        {
            var p=new BoundedPhaseProfiler("stage");int reports=0;
            Assert.IsFalse(p.Active);p.Observe(0,1);Assert.IsFalse(p.Tick(false,45,0,_=>reports++));
            p.Tick(true,45,0,_=>reports++);p.Observe(0,2);Assert.IsFalse(p.Tick(true,45,44,_=>reports++));
            Assert.IsTrue(p.Tick(true,45,45,_=>reports++));Assert.IsFalse(p.Active);Assert.AreEqual(1,reports);
            p.Tick(true,45,100,_=>reports++);Assert.IsFalse(p.Active);Assert.AreEqual(1,reports);
            p.Tick(false,45,100,null);p.Tick(true,45,100,null);Assert.IsTrue(p.Active);
            p.Tick(false,45,101,_=>reports++);Assert.AreEqual(2,reports);StringAssert.Contains(p.LastReport,"reason=disabled");
        }
        [TestMethod]
        public void PhaseProfiler_PercentileBoundsPeakContextAndNestedCounts()
        {
            var p=new BoundedPhaseProfiler("outer","inner");p.Tick(true,30,0,null);
            for(int i=1;i<=100;i++){p.Current=new BoundedPhaseProfiler.Context{Objects=i,Selection=7};p.Observe(0,i*.001);p.Observe(1,.001);}
            Assert.IsTrue(p.P95Upper(0)>=.095&&p.P95Upper(0)<=.1);
            p.Stop(30,null,"test");StringAssert.Contains(p.LastReport,"outer Count=100");StringAssert.Contains(p.LastReport,"inner Count=100");
            StringAssert.Contains(p.LastReport,"objects=100");StringAssert.Contains(p.LastReport,"selection=7");StringAssert.Contains(p.LastReport,"nested=true");
        }
        [TestMethod]
        public void PhaseProfiler_RejectsWorkerSamplesAndStaleScopes()
        {
            var p=new BoundedPhaseProfiler("stage");p.Tick(true,30,0,null);var stale=p.Measure(0);
            Task.Run(()=>p.Observe(0,100)).Wait();Assert.AreEqual(0d,p.P95Upper(0));
            p.Tick(false,30,1,null);p.Tick(true,30,2,null);stale.Dispose();Assert.AreEqual(0d,p.P95Upper(0));
            using(p.Measure(0)){}p.Stop(3,null,"test");StringAssert.Contains(p.LastReport,"Count=1");
        }
        [TestMethod]
        public void SelfMetrics_ConcurrentUpdatesPreserveTotalsAndReset()
        {
            SelfMetrics.Snapshot(true);Parallel.For(0,10000,_=>SelfMetrics.Add("testMetric",.25,false));
            var snapshot=SelfMetrics.Snapshot(true);Assert.AreEqual(10000L,snapshot["testMetricCount"]);Assert.AreEqual(2500d,snapshot["testMetricTotalMs"]);Assert.AreEqual(.25d,snapshot["testMetricMaxMs"]);
            Assert.IsFalse(SelfMetrics.Snapshot().ContainsKey("testMetricCount"));
        }
        [DataTestMethod]
        [DataRow(-10,30)]
        [DataRow(999,60)]
        public void PhaseProfiler_ClampsDurationToBoundedWindow(int requested,int expected)
        {
            var p=new BoundedPhaseProfiler("stage");p.Tick(true,requested,0,null);
            Assert.IsFalse(p.Tick(true,requested,expected-1,null));Assert.IsTrue(p.Tick(true,requested,expected,null));Assert.IsFalse(p.Active);
        }
        [TestMethod]
        public void LossSummary_LocalizesAllThreeLanguagesAndUnknownUpperIsNull()
        {
            var field=typeof(UiText).GetField("current",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);
            var next=typeof(UiText).GetField("next",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);
            object old=field.GetValue(null),oldNext=next.GetValue(null);
            try
            {
                var record=new CaptureRecord{Started=20,Ended=50,PreRollLoss=CaptureLossSnapshot.Unknown(0,20),RecordingLoss=Ledger().Lifetime(20,50)};
                next.SetValue(null,double.MaxValue);
                field.SetValue(null,"English");StringAssert.Contains(CaptureController.LossSummary(record),"unknown is not zero");
                field.SetValue(null,"SimplifiedChinese");StringAssert.Contains(CaptureController.LossSummary(record),"继承的预录省略");
                field.SetValue(null,"TraditionalChinese");StringAssert.Contains(CaptureController.LossSummary(record),"繼承的預錄省略");
                StringAssert.Contains(EventCodec.Json(CaptureController.LossManifest(record)),"null");
            }
            finally{field.SetValue(null,old);next.SetValue(null,oldNext);}
        }
        [TestMethod]
        public void DiskFormats_SwitchInQueueOrderKeepMemoryEventsAndReinitialize()
        {
            int main=DiagnosticContext.MainThreadId;DiagnosticContext.MainThreadId=-1;
            string dir=Path.Combine(Path.GetTempPath(),"WhySoLaggy.Tests",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);
            int seen=0;Action<StructuredEvent> listener=e=>seen++;StructuredLogger.EventWritten+=listener;
            try
            {
                StructuredLogger.ConfigureDiskFormat(StructuredDiskFormat.Jsonl);StructuredLogger.Initialize(dir);
                StructuredLogger.WriteEvent(Event(EventType.Marker,1));StructuredLogger.Flush();
                Assert.IsFalse(File.Exists(Path.Combine(dir,"whysolaggy_data.csv")));
                StructuredLogger.ConfigureDiskFormat(StructuredDiskFormat.Csv);StructuredLogger.WriteEvent(Event(EventType.Marker,2));StructuredLogger.Flush();
                StructuredLogger.ConfigureDiskFormat(StructuredDiskFormat.Both);StructuredLogger.WriteEvent(Event(EventType.Marker,3));StructuredLogger.Flush();
                StructuredLogger.Shutdown();Assert.AreEqual(3,seen);
                Assert.AreEqual(2,File.ReadAllLines(Path.Combine(dir,"whysolaggy_events.jsonl")).Length);
                Assert.AreEqual(3,File.ReadAllLines(Path.Combine(dir,"whysolaggy_data.csv")).Length);
                StructuredLogger.Initialize(dir);StructuredLogger.Shutdown();
            }
            finally{StructuredLogger.EventWritten-=listener;StructuredLogger.Shutdown();StructuredLogger.ConfigureDiskFormat(StructuredDiskFormat.Both);DiagnosticContext.MainThreadId=main;Directory.Delete(dir,true);}
        }
        [TestMethod]
        public void DiskFormats_MainThreadStartupAndQueuedSwitchUseRequestedFormat()
        {
            int main=DiagnosticContext.MainThreadId;DiagnosticContext.MainThreadId=Thread.CurrentThread.ManagedThreadId;
            string dir=Path.Combine(Path.GetTempPath(),"WhySoLaggy.Tests",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);
            try
            {
                StructuredLogger.ConfigureDiskFormat(StructuredDiskFormat.Jsonl);StructuredLogger.Initialize(dir);
                Assert.IsTrue(DiagnosticIO.Barrier(()=>{}));Assert.IsFalse(File.Exists(Path.Combine(dir,"whysolaggy_data.csv")));
                StructuredLogger.WriteEvent(Event(EventType.Marker,1));StructuredLogger.ConfigureDiskFormat(StructuredDiskFormat.Csv);
                StructuredLogger.WriteEvent(Event(EventType.Marker,2));StructuredLogger.Flush();Assert.IsTrue(DiagnosticIO.Barrier(()=>{}));StructuredLogger.Shutdown();
                Assert.AreEqual(1,File.ReadAllLines(Path.Combine(dir,"whysolaggy_events.jsonl")).Length);
                Assert.AreEqual(2,File.ReadAllLines(Path.Combine(dir,"whysolaggy_data.csv")).Length);
            }
            finally{StructuredLogger.Shutdown();DiagnosticContext.MainThreadId=-1;StructuredLogger.ConfigureDiskFormat(StructuredDiskFormat.Both);DiagnosticContext.MainThreadId=main;Directory.Delete(dir,true);}
        }
        [TestMethod]
        public void CopyAdmissionLoss_IsAdditionalToInheritedLossAndBudgetRemainsBounded()
        {
            long before=CapturePayloadBudget.Bytes;var source=new EventRing(1000000);var target=new EventRing(200000);CaptureRecord record=null;
            try
            {
                for(int i=0;i<300;i++)source.Add(Event(EventType.RpcCall,1+i*.01,new string('x',500)),4,20);
                source.CopyTo(target,4,20);record=target.Detach();
                Assert.AreEqual(300L-record.Count,record.PreRollLoss.Total.Lower+record.RecordingLoss.Total.Lower);
                Assert.IsTrue(record.Bytes<=target.Limit);Assert.IsTrue(record.RecordingLoss.Total.Lower>0);
                var manifest=CaptureController.LossManifest(record);StringAssert.Contains(EventCodec.Json(manifest),"copy admission");
            }
            finally{source.Clear();target.Clear();record?.Release();}Assert.AreEqual(before,CapturePayloadBudget.Bytes);
        }
        [TestMethod]
        public void PhaseReport_CreatesDirectoryBeforeFirstCapture()
        {
            string dir=Path.Combine(Path.GetTempPath(),"WhySoLaggy.Tests",Guid.NewGuid().ToString("N"));
            string previous=CaptureController.DirectoryPath;
            try
            {
                CaptureController.DirectoryPath=Path.Combine(dir,"WhySoLaggy","diagnostics");
                DiagnosticIO.Start();DiagnosticPhases.Report("first timing report");
                Assert.IsTrue(DiagnosticIO.Barrier(TextLogSink.Flush));
                using(var stream=new FileStream(Path.Combine(dir,"WhySoLaggy","phase-timing.log"),FileMode.Open,FileAccess.Read,FileShare.ReadWrite))
                using(var reader=new StreamReader(stream))StringAssert.Contains(reader.ReadToEnd(),"first timing report");
            }
            finally{DiagnosticIO.Barrier(TextLogSink.Close);DiagnosticIO.Stop();CaptureController.DirectoryPath=previous;if(Directory.Exists(dir))Directory.Delete(dir,true);}
        }
        [TestMethod]
        public void SmallRing_DeclaresUnknownAccountingWithoutExceedingCapacity()
        {
            long before=CapturePayloadBudget.Bytes;var ring=new EventRing(100000);CaptureRecord record=null;
            try
            {
                for(int i=0;i<300;i++)ring.Add(Event(EventType.RpcCall,1,new string('x',1000)),1,20);
                record=ring.Detach();Assert.IsTrue(record.Bytes<=ring.Limit);Assert.IsTrue(record.DroppedDetails>0);
                Assert.IsTrue(record.RecordingLoss.Total.Unknown);Assert.AreEqual("Unknown",record.RecordingLoss.Total.Quality);
            }
            finally{ring.Clear();record?.Release();}Assert.AreEqual(before,CapturePayloadBudget.Bytes);
        }
    }
}
