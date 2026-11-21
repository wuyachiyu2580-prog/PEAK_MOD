using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace WhySoLaggy.Tests
{
    [TestClass,DoNotParallelize]
    public class CaptureRetentionTests
    {
        public TestContext TestContext{get;set;}
        private static StructuredEvent Freeze(EventType kind,double time,long frame,Dictionary<string,object> fields)
        {var e=EventCodec.Freeze(new StructuredEvent{Timestamp="2026-11-15 15:00:00.000",FrameNumber=frame,Type=kind,Fields=fields});e.Fields["MonotonicSeconds"]=time;return e;}
        private static StructuredEvent Frame(double time,long frame)=>Freeze(EventType.FrameSample,time,frame,new Dictionary<string,object>{{"AvgFrameMs",5d},{"CoveredMs",2d},{"SelfMainMs",.2d},{"SceneState","Gameplay"},{"Scene","SampleScene"},{"Validity","Complete"}});
        private static List<MethodFrame> Methods(int count)=>Enumerable.Range(0,count).Select(i=>new MethodFrame{Id="module:123:Method"+i,Name="Example.Plugin.Component"+i+".Update()",Scope=i%2==0?"PluginProfiler":"PatchProfiler",Owners="sample.plugin."+i,PluginGuid="sample.plugin."+i,Ms=.05,Calls=10,Samples=1}).ToList();

        [DataTestMethod]
        [DataRow(30)]
        [DataRow(60)]
        public void HighRateCapture_PreservesPretriggerAndEntireTimelineAcrossRepeatedExports(int duration)
        {
            long before=CapturePayloadBudget.Bytes;var rolling=new EventRing(4*1024*1024);var frozen=new Queue<CaptureRecord>();CaptureRecord export=null;var methods=Methods(30);
            var stopwatch=Stopwatch.StartNew();long emitted=0;double now=0;long frame=0;
            try
            {
                for(int run=0;run<4;run++)
                {
                    for(int i=0;i<4000;i++){now+=.005;rolling.Add(Frame(now,++frame),now,20);}
                    while(frozen.Count>1)frozen.Dequeue().Release();
                    var active=new EventRing(8*1024*1024);rolling.CopyTo(active,now,90);double start=now;
                    var marker=Freeze(EventType.CaptureState,now,frame,new Dictionary<string,object>{{"Phase","Manual"}});active.Add(marker,now,90);
                    var window=new CaptureTimingWindow();window.Reset(now);
                    Action<StructuredEvent> emit=e=>{e=Freeze(e.Type,now,e.FrameNumber,e.Fields);rolling.Add(e,now,20);active.Add(e,now,90);emitted++;};
                    for(int i=0;i<duration*200;i++)
                    {
                        now+=.005;var e=Frame(now,++frame);rolling.Add(e,now,20);active.Add(e,now,90);
                        window.Observe(new FrameRecord{Frame=frame,Time=now,DurationMs=i%1000==0?100:5,Methods=methods},emit);
                    }
                    window.Flush(emit);var record=active.Detach();frozen.Enqueue(record);
                    Assert.AreEqual(0L,record.DroppedFrames,"Frame timeline must survive detail pressure");
                    Assert.AreEqual(0L,record.DroppedCritical,"Trigger and status must survive");
                    Assert.AreEqual(0L,record.DroppedDetails,"Thirty methods must fit for the full sixty seconds");
                    Assert.IsTrue(record.PackedFrames.Length>=(duration+20)*200-2);
                    Assert.IsTrue(record.PackedFrames[0].Time<=start-19.99);
                    Assert.AreEqual(now,record.PackedFrames.Last().Time,1e-8);
                    Assert.IsTrue(record.Events.Any(e=>e.Type==EventType.CaptureState&&EventRing.TimeOf(e,0)==start));
                    var calls=record.PackedTiming.Select(x=>x.Expand()).Where(e=>(string)e.Fields["TimingKind"]=="window inclusive"&&Convert.ToDouble(e.Fields["WindowStartSeconds"])>start);
                    Assert.AreEqual((long)duration*200*30*10,calls.Sum(e=>Convert.ToInt64(e.Fields["CallCount"])));
                    Assert.IsTrue(CapturePayloadBudget.Bytes<=CapturePayloadBudget.Limit);
                    TestContext.WriteLine("duration={0}; run={1}; bytes={2}; frames={3}; details omitted={4}; shared={5}",duration,run,record.Bytes,record.PackedFrames.Length,record.DroppedDetails,CapturePayloadBudget.Bytes);
                    if(export!=null)export.Release();export=record;export.Retain();
                }
                Assert.IsTrue(emitted<4*duration*150,"Method event volume must be bounded independently of FPS");
            }
            finally{export?.Release();foreach(var r in frozen)r.Release();rolling.Clear();}
            Assert.AreEqual(before,CapturePayloadBudget.Bytes);TestContext.WriteLine("Elapsed {0:F1}ms; timing events {1}",stopwatch.Elapsed.TotalMilliseconds,emitted);
        }
        [TestMethod]
        public void TimingWindows_PreserveExactCallsSamplesPeaksAndFinalPartialWindow()
        {
            var emitted=new List<StructuredEvent>();var window=new CaptureTimingWindow();window.Reset(0);var methods=Methods(30);
            for(int i=0;i<650;i++)window.Observe(new FrameRecord{Frame=i,Time=i*.005,DurationMs=i==12?100:5,Methods=methods},emitted.Add);
            window.Flush(emitted.Add);
            var totals=emitted.Where(e=>e.Fields.TryGetValue("TimingKind",out var kind)&&(string)kind=="window inclusive").ToArray();
            foreach(var group in totals.GroupBy(x=>(string)x.Fields["MethodId"]))
            {Assert.AreEqual(6500L,group.Sum(x=>(long)x.Fields["CallCount"]));Assert.AreEqual(650L,group.Sum(x=>(long)x.Fields["SampleCount"]));Assert.AreEqual(32.5,group.Sum(x=>(double)x.Fields["TotalMs"]),1e-8);Assert.IsTrue(group.All(x=>(double)x.Fields["SampleCoverage"]==.1));}
            Assert.AreEqual(30,totals.Select(x=>x.Fields["MethodId"]).Distinct().Count());
            Assert.AreEqual(30,emitted.Count(x=>x.Fields.TryGetValue("TimingKind",out var kind)&&((string)kind).StartsWith("spike")));
            Assert.AreEqual(650,emitted.Where(x=>x.Type==EventType.TimingWindow).Sum(x=>(int)x.Fields["ObservedFrames"]));
        }
        [TestMethod]
        public void ContinuousSpikesAndUnknownMethods_ExposeEveryOmissionAndStayBounded()
        {
            var output=new List<StructuredEvent>();var window=new CaptureTimingWindow();window.Reset(0);var methods=Methods(600);
            for(int i=0;i<200;i++)window.Observe(new FrameRecord{Frame=i,Time=i*.004,DurationMs=100,Methods=methods},output.Add);
            window.Flush(output.Add);var summary=output.Single(x=>x.Type==EventType.TimingWindow);
            Assert.AreEqual(512,(int)summary.Fields["Methods"]);Assert.AreEqual(88L*200,(long)summary.Fields["MethodObservationsOmitted"]);
            Assert.AreEqual(600L*200-90,(long)summary.Fields["SpikeMethodDetailsOmitted"]);
            Assert.AreEqual(512+90+1,output.Count);
        }
        [TestMethod]
        public void DetailAndExceptionFlood_CannotEvictTriggerOrFrameTimeline()
        {
            long before=CapturePayloadBudget.Bytes;var ring=new EventRing(1024*1024);
            try
            {
                ring.Add(Freeze(EventType.CaptureState,0,0,new Dictionary<string,object>{{"Phase","Manual"}}),0,90);
                for(int i=0;i<12000;i++)
                {
                    double t=i/200d;if(i%10==0)ring.Add(Frame(t,i),t,90);
                    ring.Add(Freeze(i%2==0?EventType.RpcCall:EventType.ExceptionSummary,t,i,new Dictionary<string,object>{{"Message",new string('x',2048)}}),t,90);
                }
                Assert.IsTrue(ring.DroppedDetails>0);Assert.IsTrue(ring.DroppedCritical>0);Assert.AreEqual(0L,ring.DroppedFrames);
                var events=ring.Snapshot();Assert.IsTrue(events.Any(x=>x.Type==EventType.CaptureState));Assert.AreEqual(1200,events.Count(x=>x.Type==EventType.FrameSample));
            }
            finally{ring.Clear();}Assert.AreEqual(before,CapturePayloadBudget.Bytes);
        }
        [TestMethod]
        public void RollingExpiry_IsNotCountedAsCapacityLossAndCopiesOriginalTimes()
        {
            var a=new EventRing(1024*1024);var b=new EventRing(1024*1024);
            try{for(int i=0;i<100;i++)a.Add(Frame(i,i),i,20);a.CopyTo(b,99,90);Assert.AreEqual(0L,a.Evicted);var frames=b.Snapshot();Assert.AreEqual(21,frames.Length);Assert.AreEqual(79d,EventRing.TimeOf(frames[0],0));Assert.AreEqual(99d,EventRing.TimeOf(frames.Last(),0));}
            finally{a.Clear();b.Clear();}
        }
        [TestMethod]
        public void PackedTiming_RoundTripsExactIntegersMissingFieldsAndSharedIdentityLifetime()
        {
            long before=CapturePayloadBudget.Bytes;var ring=new EventRing(1024*1024);var copy=new EventRing(1024*1024);CaptureRecord record=null;
            var original=Freeze(EventType.PatchTiming,12.5,42,new Dictionary<string,object>{{"MethodId","module:Method"},{"Name","Example.Update()"},{"Owner","example.plugin"},{"PluginGuid","example.plugin"},{"TimingKind","window inclusive"},{"WindowStartSeconds",11.5},{"WindowEndSeconds",12.5},{"TotalMs",123.25},{"CallCount",9007199254740993L},{"SampleCount",17L},{"SampleCoverage",.125},{"Exceptions",2L},{"FirstFrame",40L},{"LastFrame",42L},{"ObservedFrames",3},{"MethodFrames",2},{"PeakFrameMs",65.75},{"PeakFrame",42L}});
            try
            {
                ring.Add(original,12.5,20);ring.Add(original,12.5,20);ring.CopyTo(copy,12.5,20);record=copy.Detach();ring.Clear();
                Assert.AreEqual(2,record.PackedTiming.Length);Assert.AreSame(record.PackedTiming[0].Identity,record.PackedTiming[1].Identity);
                foreach(var restored in record.Enumerate())
                {
                    Assert.AreEqual(original.Type,restored.Type);Assert.AreEqual(original.FrameNumber,restored.FrameNumber);Assert.AreEqual(original.Timestamp,restored.Timestamp);Assert.AreEqual(original.Fields.Count,restored.Fields.Count);
                    foreach(var field in original.Fields)Assert.AreEqual(EventCodec.Json(field.Value),EventCodec.Json(restored.Fields[field.Key]),field.Key);
                }
                var sparse=Freeze(EventType.PluginTiming,13,43,new Dictionary<string,object>{{"MethodId","module:Other"},{"TimingKind","spike frame inclusive (subset of window totals)"},{"WindowStartSeconds",13d},{"WindowEndSeconds",13d}});
                copy.Add(sparse,13,20);var expanded=copy.Snapshot().Single();Assert.IsFalse(expanded.Fields.ContainsKey("SampleCount"));Assert.IsFalse(expanded.Fields.ContainsKey("PeakFrame"));
            }
            finally{record?.Release();ring.Clear();copy.Clear();}
            Assert.AreEqual(before,CapturePayloadBudget.Bytes);
        }
        [TestMethod]
        public void ReusedFrameStatistics_MatchReferenceAcrossExpiryAndBufferGrowth()
        {
            var window=new FrameWindow();var samples=new List<Tuple<double,double>>();var random=new Random(7);
            for(int i=0;i<20000;i++)
            {
                double time=i/200d,ms=1+random.NextDouble()*99;window.Add(time,ms);samples.Add(Tuple.Create(time,ms));
                if(i%250!=0)continue;
                var expected=FrameWindow.Calculate(samples.Where(x=>x.Item1>=time-30).Select(x=>x.Item2));var actual=window.Snapshot(time);
                Assert.AreEqual(expected.Count,actual.Count);Assert.AreEqual(expected.Mean,actual.Mean,1e-10);Assert.AreEqual(expected.P95,actual.P95);Assert.AreEqual(expected.P99,actual.P99);Assert.AreEqual(expected.Low1,actual.Low1);
            }
            window.Clear();Assert.AreEqual(0,window.Snapshot(1000).Count);
        }
    }
}
