using System;
using System.Collections.Generic;
using System.Linq;

namespace WhySoLaggy
{
    // A frozen frame has a fixed schema. Keep numbers compact while recording;
    // reconstruct dictionaries only on the export worker. Context strings are shared.
    internal sealed class CaptureFrameContext
    {
        internal readonly string Session, Capture, Scene, State, Validity, HookCoverage;
        internal readonly int Epoch, Bytes;
        internal CaptureFrameContext(string session, string capture, string scene, string state, string validity, int epoch, string hooks = null)
        {
            Session = session ?? ""; Capture = capture ?? ""; Scene = scene ?? ""; State = state ?? "";
            Validity = validity; Epoch = epoch; HookCoverage = hooks;
            Bytes = 176 + 2 * (Session.Length + Capture.Length + Scene.Length + State.Length + (Validity?.Length ?? 0) + (hooks?.Length ?? 0));
        }
        internal static string Text(Dictionary<string,object> f,string key) => f.TryGetValue(key,out var v)?Convert.ToString(v):"";
        internal static long Number(Dictionary<string,object> f,string key) => f.TryGetValue(key,out var v)?Convert.ToInt64(v):0;
    }
    internal readonly struct CaptureFrame
    {
        internal const int PayloadBytes = 192;
        internal readonly CaptureFrameContext Context;
        internal readonly long Frame, Sequence, WriterDropped, DiskDropped, QueueBytes, CaptureBytes, Omitted, IntervalsOmitted;
        internal readonly double Time, Duration, Covered, Self;
        private readonly string timestamp;
        private readonly long utcTicks;

        internal CaptureFrame(FrameRecord frame, CaptureFrameContext context)
        {
            Context = context; Frame = frame.Frame; Duration = frame.DurationMs; Covered = frame.CoveredMs; Self = frame.SelfMs;
            Time = frame.Time; Sequence = EventCodec.NextSequence(); utcTicks = DateTime.UtcNow.Ticks; timestamp = null;
            WriterDropped = DiagnosticIO.Dropped; DiskDropped = System.Threading.Interlocked.Read(ref StructuredLogger.DiskDropped);
            QueueBytes = DiagnosticIO.PendingBytes; CaptureBytes = CapturePayloadBudget.Bytes; Omitted = 0;
            IntervalsOmitted = frame.IntervalsOmitted;
        }

        internal StructuredEvent Expand()
        {
            var fields = new Dictionary<string, object>(22)
            {
                {"SchemaVersion",2},{"SessionId",Context.Session},{"CaptureId",Context.Capture},{"RoomEpoch",Context.Epoch},
                {"Sequence",Sequence},{"MonotonicSeconds",Time},{"AvgFrameMs",Duration},{"CoveredMs",Covered},
                {"UnattributedMs",Math.Max(0,Duration-Covered)},{"SelfMainMs",Self},{"WriterDropped",WriterDropped},
                {"DiskDroppedBatches",DiskDropped},{"WriterQueueBytes",QueueBytes},{"CapturePayloadBytes",CaptureBytes},
                {"MethodRecordOmitted",Omitted},{"SceneState",Context.State},{"Scene",Context.Scene},{"Validity",Context.Validity},
                {"FrameIntervalsOmitted",IntervalsOmitted},{"HookCoverage",Context.HookCoverage}
            };
            return new StructuredEvent { Timestamp = timestamp ?? StructuredLogger.StampFromUtcTicks(utcTicks), FrameNumber = Frame, Type = EventType.FrameSample, Fields = fields };
        }

        internal CaptureFrame(StructuredEvent e, double time, CaptureFrameContext context) : this()
        {
            var f = e.Fields;
            Context = context; Frame = e.FrameNumber; Time = time; timestamp = e.Timestamp;
            Duration = Convert.ToDouble(f["AvgFrameMs"]); Covered = Convert.ToDouble(f["CoveredMs"]); Self = Convert.ToDouble(f["SelfMainMs"]);
            Sequence = CaptureFrameContext.Number(f,"Sequence"); WriterDropped = CaptureFrameContext.Number(f,"WriterDropped");
            DiskDropped = CaptureFrameContext.Number(f,"DiskDroppedBatches"); QueueBytes = CaptureFrameContext.Number(f,"WriterQueueBytes");
            CaptureBytes = CaptureFrameContext.Number(f,"CapturePayloadBytes"); Omitted = CaptureFrameContext.Number(f,"MethodRecordOmitted");
            IntervalsOmitted = CaptureFrameContext.Number(f,"FrameIntervalsOmitted");
        }
    }
    internal sealed class EventRing
    {
        private struct Item {internal double Time;internal StructuredEvent Event;internal CaptureTiming Timing;internal CaptureNetwork Network;internal int Bytes;internal StructuredEvent Expand()=>Timing!=null?Timing.Expand():Network!=null?Network.Expand():Event;}
        private readonly Queue<Item> anchors=new Queue<Item>(),critical=new Queue<Item>(),network=new Queue<Item>(),details=new Queue<Item>();
        private readonly Queue<CaptureFrame> frames=new Queue<CaptureFrame>();
        private readonly Dictionary<CaptureFrameContext,int> contexts = new Dictionary<CaptureFrameContext,int>();
        private CaptureFrameContext previousContext;
        private readonly Dictionary<string,CaptureTimingIdentity> timingIdentities=new Dictionary<string,CaptureTimingIdentity>();
        private readonly Dictionary<string,CaptureNetworkIdentity> networkIdentities=new Dictionary<string,CaptureNetworkIdentity>();
        private CaptureLossLedger losses;
        private CaptureLossSnapshot preRoll;
        private bool accountingInitialized;
        private const int AccountingBytes=CaptureLossLedger.PayloadBytes+4096;
        private long frameBytes,anchorBytes,criticalBytes,networkBytes,detailBytes,accountingBytes;
        internal long Bytes=>frameBytes+anchorBytes+criticalBytes+networkBytes+detailBytes+accountingBytes;
        internal long DroppedNetwork{get;private set;}
        private void EnsureAccounting()
        {
            if(accountingInitialized)return;accountingInitialized=true;
            if(Limit*4L/10>=AccountingBytes&&CapturePayloadBudget.Reserve(AccountingBytes)){accountingBytes=AccountingBytes;losses=new CaptureLossLedger();}
        }
        private void Lost(double time,CaptureCategory category,CaptureLossReason reason)
        {
            losses?.Add(time,category,reason);
            if(category==CaptureCategory.Frame)DroppedFrames++;
            else if(category==CaptureCategory.Detail)DroppedDetails++;
            else {DroppedCritical++;if(category==CaptureCategory.Network)DroppedNetwork++;}
        }
        private CaptureCategory Category(Queue<Item> queue)=>queue==anchors?CaptureCategory.Anchor:queue==critical?CaptureCategory.Protected:queue==network?CaptureCategory.Network:CaptureCategory.Detail;
        internal long Evicted=>DroppedFrames+DroppedCritical+DroppedDetails;
        internal long DroppedFrames{get;private set;}
        internal long DroppedCritical{get;private set;}
        internal long DroppedDetails{get;private set;}
        internal readonly int Limit;
        internal EventRing(int bytes){Limit=bytes;}
        internal static double TimeOf(StructuredEvent e,double fallback) => e.Fields!=null&&e.Fields.TryGetValue("MonotonicSeconds",out var v)?Convert.ToDouble(v):fallback;
        private static bool Anchor(EventType type) => type==EventType.CaptureState||type==EventType.Marker||type==EventType.SessionBoundary||type==EventType.TimingWindow;
        internal static bool Important(EventType type) => Anchor(type)||type==EventType.ExceptionSummary||type==EventType.SpikeFrame||type==EventType.FpsReport||type==EventType.NetworkSample||type==EventType.AbuseAlert;
        private void RemoveFrame(bool dropped)
        {
            var f=frames.Dequeue();int size=CaptureFrame.PayloadBytes;
            if (contexts[f.Context] == 1) { size += f.Context.Bytes; contexts.Remove(f.Context); }
            else contexts[f.Context]--;
            frameBytes-=size;CapturePayloadBudget.Release(size);if(dropped)Lost(f.Time,CaptureCategory.Frame,CaptureLossReason.Capacity);
        }
        private void RemoveEvent(Queue<Item> queue,bool important,bool dropped)
        {
            var item=queue.Dequeue();int size=item.Bytes;
            if(item.Timing!=null&&--item.Timing.Identity.References==0){size+=item.Timing.Identity.Bytes;timingIdentities.Remove(item.Timing.Identity.Key);}
            if(item.Network!=null&&--item.Network.Identity.References==0){size+=item.Network.Identity.Bytes;networkIdentities.Remove(item.Network.Identity.Key);}
            CapturePayloadBudget.Release(size);
            if(dropped)Lost(item.Time,Category(queue),CaptureLossReason.Capacity);
            if(important){if(queue==anchors)anchorBytes-=size;else if(queue==network)networkBytes-=size;else criticalBytes-=size;}else detailBytes-=size;
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
            using(DiagnosticPhases.Measure(DiagnosticPhases.Stage.Pack))
            {
                EnsureAccounting();Expire(time-window);double eventTime=TimeOf(e,time);
                if(eventTime<time-window)return;
                if(e.Type==EventType.FrameSample&&e.Fields!=null&&e.Fields.ContainsKey("SelfMainMs"))
                {
                    var f = e.Fields;
                    string scene = CaptureFrameContext.Text(f,"Scene"), state = CaptureFrameContext.Text(f,"SceneState");
                    string validity = CaptureFrameContext.Text(f,"Validity"), session = CaptureFrameContext.Text(f,"SessionId");
                    string capture = CaptureFrameContext.Text(f,"CaptureId"), hooks = CaptureFrameContext.Text(f,"HookCoverage");
                    int epoch = (int)CaptureFrameContext.Number(f,"RoomEpoch");
                    var c = previousContext;
                    if (c == null || c.Session != session || c.Capture != capture || c.Scene != scene || c.State != state ||
                        c.Validity != validity || c.Epoch != epoch || c.HookCoverage != hooks)
                        previousContext = c = new CaptureFrameContext(session, capture, scene, state, validity, epoch, hooks);
                    AddFrame(new CaptureFrame(e, eventTime, c)); return;
                }
                bool anchor=Anchor(e.Type),important=Important(e.Type);var queue=anchor?anchors:e.Type==EventType.NetworkSample?network:important?critical:details;
                var category=Category(queue);CaptureTiming packed=null;CaptureNetwork packedNetwork=null;
                if((e.Type==EventType.PluginTiming||e.Type==EventType.PatchTiming)&&e.Fields!=null&&e.Fields.ContainsKey("WindowStartSeconds"))
                {
                    string key=CaptureTimingIdentity.KeyOf(e);if(!timingIdentities.TryGetValue(key,out var identity))identity=new CaptureTimingIdentity(e,key);
                    packed=new CaptureTiming(e,identity);
                }
                if(CaptureNetwork.Supports(e))
                {
                    string key=CaptureNetworkIdentity.KeyOf(e.Fields);if(!networkIdentities.TryGetValue(key,out var identity))identity=new CaptureNetworkIdentity(e.Fields,key);
                    packedNetwork=new CaptureNetwork(e,identity);
                }
                int payload=packed!=null?CaptureTiming.PayloadBytes:packedNetwork!=null?CaptureNetwork.PayloadBytes:EventCodec.Size(e);
                int eventSize=payload+(packed!=null&&packed.Identity.References==0?packed.Identity.Bytes:packedNetwork!=null&&packedNetwork.Identity.References==0?packedNetwork.Identity.Bytes:0);
                long cap=anchor?Limit*3L/100:queue==network?Limit*5L/100:important?Limit*2L/100:Limit/2;
                if(eventSize>cap){Lost(eventTime,category,CaptureLossReason.Oversize);return;}
                while(queue.Count>0&&(anchor?anchorBytes:queue==network?networkBytes:important?criticalBytes:detailBytes)+eventSize>cap)
                {RemoveEvent(queue,important,true);eventSize=payload+(packed!=null&&packed.Identity.References==0?packed.Identity.Bytes:packedNetwork!=null&&packedNetwork.Identity.References==0?packedNetwork.Identity.Bytes:0);}
                if(eventSize>cap){Lost(eventTime,category,CaptureLossReason.Oversize);return;}
                if(!CapturePayloadBudget.Reserve(eventSize)){Lost(eventTime,category,CaptureLossReason.SharedBudget);return;}
                if(packed!=null&&packed.Identity.References++==0)timingIdentities.Add(packed.Identity.Key,packed.Identity);
                if(packedNetwork!=null&&packedNetwork.Identity.References++==0)networkIdentities.Add(packedNetwork.Identity.Key,packedNetwork.Identity);
                queue.Enqueue(new Item{Time=eventTime,Event=packed==null&&packedNetwork==null?e:default,Timing=packed,Network=packedNetwork,Bytes=payload});if(anchor)anchorBytes+=eventSize;else if(queue==network)networkBytes+=eventSize;else if(important)criticalBytes+=eventSize;else detailBytes+=eventSize;
            }
        }
        internal void Add(CaptureFrame frame, double now, double window)
        {
            Expire(now - window);
            if (frame.Time >= now - window) AddFrame(frame);
        }
        private void AddFrame(CaptureFrame frame)
        {
            EnsureAccounting();var context=frame.Context;
            // Timeline, state/error summaries and verbose detail have separate quotas.
            int size=CaptureFrame.PayloadBytes+(!contexts.ContainsKey(context)?context.Bytes:0);
            long cap=Math.Max(0,Limit*4L/10-accountingBytes);
            if(size>cap){Lost(frame.Time,CaptureCategory.Frame,CaptureLossReason.Oversize);return;}
            while(frames.Count>0&&frameBytes+size>cap){RemoveFrame(true);size=CaptureFrame.PayloadBytes+(!contexts.ContainsKey(context)?context.Bytes:0);}
            if(size>cap){Lost(frame.Time,CaptureCategory.Frame,CaptureLossReason.Oversize);return;}
            if(!CapturePayloadBudget.Reserve(size)){Lost(frame.Time,CaptureCategory.Frame,CaptureLossReason.SharedBudget);return;}
            contexts.TryGetValue(context, out int count); contexts[context] = count + 1; frames.Enqueue(frame);frameBytes+=size;
        }
        internal void CopyTo(EventRing destination,double now,double window)
        {
            if(ReferenceEquals(this,destination)||destination.preRoll!=null)throw new InvalidOperationException("Pre-roll can be inherited only once.");
            EnsureAccounting();destination.EnsureAccounting();Expire(now-window);
            destination.preRoll=losses?.Snapshot(now-window,now)??CaptureLossSnapshot.Unknown(now-window,now);
            foreach(var frame in frames)
            {
                if (frame.Time <= now) destination.AddFrame(frame);
            }
            foreach(var item in anchors.Concat(critical).Concat(network).Concat(details))
                if(item.Time>=now-window&&item.Time<=now)destination.Add(item.Expand(),now,window);
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
            var record=new CaptureRecord{PackedFrames=frames.ToArray(),PackedTiming=details.Where(x=>x.Timing!=null).Select(x=>x.Timing).ToArray(),PackedNetwork=network.Where(x=>x.Network!=null).Select(x=>x.Network).ToArray(),Events=anchors.Concat(critical).Concat(network).Concat(details).Where(x=>x.Timing==null&&x.Network==null).Select(x=>x.Event).ToArray(),Bytes=Bytes,Evicted=Evicted,DroppedFrames=DroppedFrames,DroppedCritical=DroppedCritical,DroppedDetails=DroppedDetails,DroppedNetwork=DroppedNetwork,PreRollLoss=preRoll,RecordingLoss=losses?.Lifetime(0,0)??CaptureLossSnapshot.Unknown(0,0)};
            Reset();return record;
        }
        private void Reset(){frames.Clear();anchors.Clear();critical.Clear();network.Clear();details.Clear();contexts.Clear();previousContext=null;timingIdentities.Clear();networkIdentities.Clear();losses=null;preRoll=null;accountingInitialized=false;frameBytes=anchorBytes=criticalBytes=networkBytes=detailBytes=accountingBytes=0;DroppedFrames=DroppedCritical=DroppedDetails=DroppedNetwork=0;}
        internal void Clear(){CapturePayloadBudget.Release(Bytes);Reset();}
    }
    internal sealed class CaptureRecord
    {
        internal string Id,Reason;
        internal Dictionary<string,object> StreamCounters;
        internal double Started,Ended;
        internal StructuredEvent[] Events;
        internal CaptureFrame[] PackedFrames=new CaptureFrame[0];
        internal CaptureTiming[] PackedTiming=new CaptureTiming[0];
        internal CaptureNetwork[] PackedNetwork=new CaptureNetwork[0];
        internal CaptureLossSnapshot PreRollLoss,RecordingLoss;
        internal long Evicted,Bytes,DroppedFrames,DroppedCritical,DroppedDetails,DroppedNetwork;
        internal int Count=>Events.Length+PackedFrames.Length+PackedTiming.Length+PackedNetwork.Length;
        internal IEnumerable<StructuredEvent> Enumerate()=>EventRing.Merge(PackedFrames,Events.Concat(PackedTiming.Select(x=>x.Expand())).Concat(PackedNetwork.Select(x=>x.Expand())));
        internal double? RetainedStart=>Count==0?(double?)null:Enumerate().Min(x=>EventRing.TimeOf(x,Started));
        internal double? RetainedEnd=>Count==0?(double?)null:Enumerate().Max(x=>EventRing.TimeOf(x,Ended));
        private int references=1;
        internal void Retain()=>System.Threading.Interlocked.Increment(ref references);
        internal void Release(){if(System.Threading.Interlocked.Decrement(ref references)==0){CapturePayloadBudget.Release(Bytes);Events=new StructuredEvent[0];PackedFrames=new CaptureFrame[0];PackedTiming=new CaptureTiming[0];PackedNetwork=new CaptureNetwork[0];PreRollLoss=RecordingLoss=null;}}
    }
}
