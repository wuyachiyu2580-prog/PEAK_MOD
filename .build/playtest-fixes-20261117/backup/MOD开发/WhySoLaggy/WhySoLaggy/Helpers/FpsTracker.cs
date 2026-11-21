using System;
using System.Collections.Generic;
namespace WhySoLaggy
{
    internal static class FpsTracker
    {
        public static int SpikeThresholdMs=50,ReportIntervalSeconds=10;
        public static bool EnableMemoryMonitor=true;
        public static bool IsSpikeFrame{get;private set;}
        public static float CurrentFrameMs{get;private set;}
        internal static FrameStatistics Statistics=new FrameStatistics(),NormalStatistics=new FrameStatistics();
        private static readonly FrameWindow all=new FrameWindow(),normal=new FrameWindow();
        private static double duration,sum,min=double.MaxValue,max,lastSnapshot,continuous,longest;
        private static int frames,spikes,runs,lastGc;
        internal static readonly FrameWindow SelfWindow=new FrameWindow();
        internal static FrameStatistics SelfStatistics=new FrameStatistics();
        internal static void Observe(FrameRecord frame)
        {
            CurrentFrameMs=(float)frame.DurationMs;IsSpikeFrame=frame.DurationMs>SpikeThresholdMs;
            all.Add(frame.Time,frame.DurationMs);SelfWindow.Add(frame.Time,Math.Max(.000001,frame.SelfMs));
            if(frame.Complete&&(frame.State=="Gameplay"||frame.State=="Airport")&&frame.Time-FrameCoordinator.StableSince>=5)normal.Add(frame.Time,frame.DurationMs);
            if(frame.Time-lastSnapshot>=.25){SelfStatistics=SelfWindow.Snapshot(frame.Time);Statistics=all.Snapshot(frame.Time);NormalStatistics=normal.Snapshot(frame.Time);lastSnapshot=frame.Time;}
            duration+=frame.DurationMs/1000;sum+=frame.DurationMs;frames++;min=Math.Min(min,frame.DurationMs);max=Math.Max(max,frame.DurationMs);
            if(IsSpikeFrame){spikes++;if(continuous==0)runs++;continuous+=frame.DurationMs;longest=Math.Max(longest,continuous);}else continuous=0;
            if(IsSpikeFrame)StructuredLogger.WriteEvent(new StructuredEvent{Timestamp=StructuredLogger.NowStamp(),FrameNumber=frame.Frame,Type=EventType.SpikeFrame,
                Fields=new Dictionary<string,object>{{"AvgFrameMs",frame.DurationMs},{"CoveredMs",frame.CoveredMs},{"UnattributedMs",Math.Max(0,frame.DurationMs-frame.CoveredMs)},{"Validity",frame.Complete?"Complete":"Incomplete"},{"SceneState",frame.State}}});
            if(duration>=ReportIntervalSeconds){Report();duration=sum=max=longest=0;min=double.MaxValue;frames=spikes=runs=0;}
        }
        public static void Tick(){MemoryMetrics.Tick(DiagnosticClock.Now);}
        public static float GetWindowAvgMs()=>(float)Statistics.Mean;
        public static float GetAllocRateKBps()=>float.NaN;
        private static void Report()
        {
            var rpc=RpcMonitor.TakeRuntimeMetrics();var writer=StructuredLogger.TakeRuntimeMetrics();
            var fields=new Dictionary<string,object>{{"AvgFps",frames/Math.Max(.001,duration)},{"MinFps",1000/Math.Max(.001,max)},{"MaxFps",1000/Math.Max(.001,min)},
                {"AvgFrameMs",sum/Math.Max(1,frames)},{"SpikeThresholdMs",SpikeThresholdMs},{"SpikeCount",spikes},{"ReportDuration",duration},
                {"SelfMainP95Ms",SelfStatistics.P95},{"SelfMainP99Ms",SelfStatistics.P99},{"GameplaySamples",NormalStatistics.Count},{"GameplayP95Ms",NormalStatistics.P95},{"GameplayP99Ms",NormalStatistics.P99},{"GameplayLow1Fps",NormalStatistics.Low1},{"P95FrameMs",Statistics.P95},{"P99FrameMs",Statistics.P99},{"Low1Fps",Statistics.Low1},{"LongestStallMs",longest},{"StallRuns",runs},
                {"UnityAllocatedBytes",MemoryMetrics.UnityBytes},{"ManagedUsedBytes",MemoryMetrics.ManagedUsedBytes},{"ManagedCapacityBytes",MemoryMetrics.ManagedCapacityBytes},
                {"MemoryNetKBps",MemoryMetrics.NetKBps},{"GcAllocatedFrameBytes",MemoryMetrics.GcAllocatedBytes},{"GcAllocationStatus",MemoryMetrics.AllocationStatus},{"GcCollections",Math.Max(0,MemoryMetrics.GcCollections-lastGc)},
                {"RpcQueueDepth",rpc.QueueDepth},{"RpcQueuePeak",rpc.QueuePeak},{"RpcQueueDropped",rpc.QueueDropped},{"RpcProcessedCount",rpc.ProcessedCount},{"RpcPumpMs",rpc.PumpMs},
                {"StructuredEventsWritten",writer.EventsWritten},{"StructuredFlushMs",writer.LastFlushMs}};
            lastGc=MemoryMetrics.GcCollections;
            foreach(var pair in SelfMetrics.Snapshot(true))fields["Self"+pair.Key]=pair.Value;
            StructuredLogger.WriteEvent(new StructuredEvent{Timestamp=StructuredLogger.NowStamp(),FrameNumber=DiagnosticContext.Frame,Type=EventType.FpsReport,Fields=fields});
            LagLogger.Write($"[WHY_LAG] FPS {frames/Math.Max(.001,duration):F1}; p95={Statistics.P95:F2}ms p99={Statistics.P99:F2}ms; spikes={spikes}; window={duration:F2}s");
        }
        public static void Reset(){SelfWindow.Clear();SelfStatistics=new FrameStatistics();all.Clear();normal.Clear();Statistics=new FrameStatistics();NormalStatistics=new FrameStatistics();frames=spikes=runs=0;duration=sum=max=continuous=longest=lastSnapshot=0;min=double.MaxValue;}
    }
}