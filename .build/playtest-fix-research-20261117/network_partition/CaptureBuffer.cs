using System;
using System.Collections.Generic;
using System.Linq;

namespace WhySoLaggy
{
    // A frozen frame has a fixed schema. Keep numbers compact while recording;
    // reconstruct dictionaries only on the export worker. Context strings are shared.
    internal sealed class CaptureFrameContext
    {
        internal string Session, Capture, Scene, State, Validity;
        internal int Epoch, References, Bytes;
        internal bool Matches(Dictionary<string, object> f) => Session == Text(f,"SessionId") && Capture == Text(f,"CaptureId") &&
            Scene == Text(f,"Scene") && State == Text(f,"SceneState") && Validity == Text(f,"Validity") && Epoch == Number(f,"RoomEpoch");
        internal bool Matches(CaptureFrameContext c) => Session==c.Session&&Capture==c.Capture&&Scene==c.Scene&&State==c.State&&Validity==c.Validity&&Epoch==c.Epoch;
        internal static string Text(Dictionary<string,object> f,string key) => f.TryGetValue(key,out var v)?Convert.ToString(v):"";
        internal static long Number(Dictionary<string,object> f,string key) => f.TryGetValue(key,out var v)?Convert.ToInt64(v):0;
    }
    internal struct CaptureFrame
    {
        internal const int PayloadBytes=192;
        internal CaptureFrameContext Context;
        internal string Timestamp;
        internal long Frame, Sequence, WriterDropped, DiskDropped, QueueBytes, CaptureBytes, Omitted;
        internal double Time, Duration, Covered, Self;
        internal static CaptureFrame Pack(StructuredEvent e,double time,CaptureFrameContext context)
        {
            var f=e.Fields;
            return new CaptureFrame{Context=context,Timestamp=e.Timestamp,Frame=e.FrameNumber,Time=time,
                Sequence=CaptureFrameContext.Number(f,"Sequence"),Duration=Convert.ToDouble(f["AvgFrameMs"]),Covered=Convert.ToDouble(f["CoveredMs"]),Self=Convert.ToDouble(f["SelfMainMs"]),
                WriterDropped=CaptureFrameContext.Number(f,"WriterDropped"),DiskDropped=CaptureFrameContext.Number(f,"DiskDroppedBatches"),QueueBytes=CaptureFrameContext.Number(f,"WriterQueueBytes"),CaptureBytes=CaptureFrameContext.Number(f,"CapturePayloadBytes"),Omitted=CaptureFrameContext.Number(f,"MethodRecordOmitted")};
        }
        internal StructuredEvent Expand() => new StructuredEvent{Timestamp=Timestamp,FrameNumber=Frame,Type=EventType.FrameSample,
            Fields=new Dictionary<string,object>{{"SchemaVersion",2},{"SessionId",Context.Session},{"CaptureId",Context.Capture},{"RoomEpoch",Context.Epoch},{"Sequence",Sequence},{"MonotonicSeconds",Time},
                {"AvgFrameMs",Duration},{"CoveredMs",Covered},{"UnattributedMs",Math.Max(0,Duration-Covered)},{"SelfMainMs",Self},{"WriterDropped",WriterDropped},{"DiskDroppedBatches",DiskDropped},
                {"WriterQueueBytes",QueueBytes},{"CapturePayloadBytes",CaptureBytes},{"MethodRecordOmitted",Omitted},{"SceneState",Context.State},{"Scene",Context.Scene},{"Validity",Context.Validity}}};
    }
    internal sealed class EventRing
    {
        private struct Item {internal double Time;internal StructuredEvent Event;internal CaptureTiming Timing;internal int Bytes;internal StructuredEvent Expand()=>Timing==null?Event:Timing.Expand();}
        private readonly Queue<Item> anchors=new Queue<Item>(),critical=new Queue<Item>(),network=new Queue<Item>(),details=new Queue<Item>();
        private readonly Queue<CaptureFrame> frames=new Queue<CaptureFrame>();
        private readonly List<CaptureFrameContext> contexts=new List<CaptureFrameContext>();
        private readonly Dictionary<string,CaptureTimingIdentity> timingIdentities=new Dictionary<string,CaptureTimingIdentity>();
        private long frameBytes,anchorBytes,criticalBytes,networkBytes,detailBytes;
        internal long Bytes=>frameBytes+anchorBytes+criticalBytes+networkBytes+detailBytes;
        internal long Evicted=>DroppedFrames+DroppedCritical+DroppedDetails;
        internal long DroppedFrames{get;private set;}
        internal long DroppedCritical{get;private set;}
        internal long DroppedDetails{get;private set;}
        internal readonly int Limit;
        internal EventRing(int bytes){Limit=bytes;}
        internal static double TimeOf(StructuredEvent e,double fallback) => e.Fields!=null&&e.Fields.TryGetValue("MonotonicSeconds",out var v)?Convert.ToDouble(v):fallback;
        private static bool Anchor(EventType type) => type==EventType.CaptureState||type==EventType.Marker||type==EventType.SessionBoundary||type==EventType.TimingWindow;
        private static bool Important(EventType type) => Anchor(type)||type==EventType.ExceptionSummary||type==EventType.SpikeFrame||type==EventType.FpsReport||type==EventType.NetworkSample||type==EventType.AbuseAlert;
        private void RemoveFrame(bool dropped)
        {
            var f=frames.Dequeue();int size=CaptureFrame.PayloadBytes;
            if(--f.Context.References==0){size+=f.Context.Bytes;contexts.Remove(f.Context);}
            frameBytes-=size;CapturePayloadBudget.Release(size);if(dropped)DroppedFrames++;
        }
        private void RemoveEvent(Queue<Item> queue,bool important,bool dropped)
        {
            var item=queue.Dequeue();int size=item.Bytes;
            if(item.Timing!=null&&--item.Timing.Identity.References==0){size+=item.Timing.Identity.Bytes;timingIdentities.Remove(item.Timing.Identity.Key);}
            CapturePayloadBudget.Release(size);
            if(important){if(queue==anchors)anchorBytes-=size;else if(queue==network)networkBytes-=size;else criticalBytes-=size;if(dropped)DroppedCritical++;}else{detailBytes-=size;if(dropped)DroppedDetails++;}
        }
        private void Expire(double cutoff)
        {
            while(frames.Count>0&&frames.Peek().Time<cutoff)RemoveFrame(false);
            while(anchors.Count>0&&anchors.Peek().Time<cutoff)RemoveEvent(anchors,true,false);
            while(critical.Count>0&&critical.Peek().Time<cutoff)RemoveEvent(critical,true,false);
            while(network.Count>0&&network.Peek().Time<cutoff)RemoveEvent(network,true,false);
            while(details.Count>0&&details.Peek().Time<cutoff)RemoveEvent(details,false,false);
        }
        internal void Add(StructuredEvent e,double time,double window)
        {
            Expire(time-window);double eventTime=TimeOf(e,time);
            if(eventTime<time-window)return;
            if(e.Type==EventType.FrameSample&&e.Fields!=null&&e.Fields.ContainsKey("SelfMainMs"))
            {
                var context=contexts.Find(x=>x.Matches(e.Fields));
                if(context==null)
                {
                    context=new CaptureFrameContext{Session=CaptureFrameContext.Text(e.Fields,"SessionId"),Capture=CaptureFrameContext.Text(e.Fields,"CaptureId"),Scene=CaptureFrameContext.Text(e.Fields,"Scene"),State=CaptureFrameContext.Text(e.Fields,"SceneState"),Validity=CaptureFrameContext.Text(e.Fields,"Validity"),Epoch=(int)CaptureFrameContext.Number(e.Fields,"RoomEpoch")};
                    context.Bytes=160+2*(context.Session.Length+context.Capture.Length+context.Scene.Length+context.State.Length+context.Validity.Length);
                }
                AddFrame(CaptureFrame.Pack(e,eventTime,context));return;
            }
            bool anchor=Anchor(e.Type),important=Important(e.Type);bool net=e.Type==EventType.NetworkSample;var queue=anchor?anchors:net?network:important?critical:details;
            CaptureTiming packed=null;
            if((e.Type==EventType.PluginTiming||e.Type==EventType.PatchTiming)&&e.Fields!=null&&e.Fields.ContainsKey("WindowStartSeconds"))
            {
                string key=CaptureTimingIdentity.KeyOf(e);if(!timingIdentities.TryGetValue(key,out var identity))identity=new CaptureTimingIdentity(e,key);
                packed=new CaptureTiming(e,identity);
            }
            int eventSize=packed==null?EventCodec.Size(e):CaptureTiming.PayloadBytes+(packed.Identity.References==0?packed.Identity.Bytes:0);long cap=anchor?Limit*3L/100:net?Limit*7L/100:important?Limit*7L/100:Limit*43L/100;
            if(eventSize>cap){if(important)DroppedCritical++;else DroppedDetails++;return;}
            while(queue.Count>0&&(anchor?anchorBytes:net?networkBytes:important?criticalBytes:detailBytes)+eventSize>cap)
            {RemoveEvent(queue,important,true);if(packed!=null)eventSize=CaptureTiming.PayloadBytes+(packed.Identity.References==0?packed.Identity.Bytes:0);}
            if(!CapturePayloadBudget.Reserve(eventSize)){if(important)DroppedCritical++;else DroppedDetails++;return;}
            if(packed!=null&&packed.Identity.References++==0)timingIdentities.Add(packed.Identity.Key,packed.Identity);
            queue.Enqueue(new Item{Time=eventTime,Event=packed==null?e:default,Timing=packed,Bytes=packed==null?eventSize:CaptureTiming.PayloadBytes});if(anchor)anchorBytes+=eventSize;else if(net)networkBytes+=eventSize;else if(important)criticalBytes+=eventSize;else detailBytes+=eventSize;
        }
        private void AddFrame(CaptureFrame frame)
        {
            var context=frame.Context;
            // Timeline, state/error summaries and verbose detail have separate quotas.
            int size=CaptureFrame.PayloadBytes+(context.References==0?context.Bytes:0);
            while(frames.Count>0&&frameBytes+size>Limit*4L/10){RemoveFrame(true);size=CaptureFrame.PayloadBytes+(context.References==0?context.Bytes:0);}
            if(frameBytes+size>Limit*4L/10||!CapturePayloadBudget.Reserve(size)){DroppedFrames++;return;}
            if(context.References++==0)contexts.Add(context);frames.Enqueue(frame);frameBytes+=size;
        }
        internal void CopyTo(EventRing destination,double now,double window)
        {
            Expire(now-window);
            foreach(var frame in frames)
            {
                var c=frame.Context;var target=destination.contexts.Find(x=>x.Matches(c));
                if(target==null)target=new CaptureFrameContext{Session=c.Session,Capture=c.Capture,Scene=c.Scene,State=c.State,Validity=c.Validity,Epoch=c.Epoch,Bytes=c.Bytes};
                var copy=frame;copy.Context=target;destination.AddFrame(copy);
            }
            foreach(var item in anchors)destination.Add(item.Expand(),now,window);
            foreach(var item in critical)destination.Add(item.Expand(),now,window);
            foreach(var item in network)destination.Add(item.Expand(),now,window);
            foreach(var item in details)destination.Add(item.Expand(),now,window);
        }
        internal IEnumerable<StructuredEvent> Enumerate() => Merge(frames,anchors.Concat(critical).Concat(network).Concat(details).Select(x=>x.Expand()));
        internal static IEnumerable<StructuredEvent> Merge(IEnumerable<CaptureFrame> packed,IEnumerable<StructuredEvent> events)
        {
            // Sort only sparse events; frame order is already monotonic.
            using(var f=packed.GetEnumerator())using(var e=events.OrderBy(x=>TimeOf(x,0)).GetEnumerator())
            {
                bool hf=f.MoveNext(),he=e.MoveNext();
                while(hf||he){if(hf&&(!he||f.Current.Time<=TimeOf(e.Current,0))){yield return f.Current.Expand();hf=f.MoveNext();}else{yield return e.Current;he=e.MoveNext();}}
            }
        }
        internal StructuredEvent[] Snapshot()=>Enumerate().ToArray();
        internal CaptureRecord Detach()
        {
            var record=new CaptureRecord{PackedFrames=frames.ToArray(),PackedTiming=details.Where(x=>x.Timing!=null).Select(x=>x.Timing).ToArray(),Events=anchors.Concat(critical).Concat(network).Concat(details).Where(x=>x.Timing==null).Select(x=>x.Event).ToArray(),Bytes=Bytes,Evicted=Evicted,DroppedFrames=DroppedFrames,DroppedCritical=DroppedCritical,DroppedDetails=DroppedDetails};
            Reset();return record;
        }
        private void Reset(){frames.Clear();anchors.Clear();critical.Clear();network.Clear();details.Clear();contexts.Clear();timingIdentities.Clear();frameBytes=anchorBytes=criticalBytes=networkBytes=detailBytes=0;DroppedFrames=DroppedCritical=DroppedDetails=0;}
        internal void Clear(){CapturePayloadBudget.Release(Bytes);Reset();}
    }
    internal sealed class CaptureRecord
    {
        internal string Id,Reason;
        internal double Started,Ended;
        internal StructuredEvent[] Events;
        internal CaptureFrame[] PackedFrames=new CaptureFrame[0];
        internal CaptureTiming[] PackedTiming=new CaptureTiming[0];
        internal long Evicted,Bytes,DroppedFrames,DroppedCritical,DroppedDetails;
        internal int Count=>Events.Length+PackedFrames.Length+PackedTiming.Length;
        internal IEnumerable<StructuredEvent> Enumerate()=>EventRing.Merge(PackedFrames,Events.Concat(PackedTiming.Select(x=>x.Expand())));
        internal double? RetainedStart=>Count==0?(double?)null:Enumerate().Min(x=>EventRing.TimeOf(x,Started));
        internal double? RetainedEnd=>Count==0?(double?)null:Enumerate().Max(x=>EventRing.TimeOf(x,Ended));
        private int references=1;
        internal void Retain()=>System.Threading.Interlocked.Increment(ref references);
        internal void Release(){if(System.Threading.Interlocked.Decrement(ref references)==0){CapturePayloadBudget.Release(Bytes);Events=new StructuredEvent[0];PackedFrames=new CaptureFrame[0];PackedTiming=new CaptureTiming[0];}}
    }
}
