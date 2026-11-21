using System;
using System.Collections.Generic;
using System.Linq;

namespace WhySoLaggy
{
    internal sealed class CaptureTimingWindow
    {
        private sealed class Total
        {
            internal string Id,Name,Owner,Scope,Plugin;
            internal long Calls,Samples,Errors,PeakFrame;
            internal double Ms,Peak;
            internal int Frames;
        }
        internal const int MaxMethods=512,MaxSpikeDetails=30,MaxSpikeFramesPerWindow=3;
        private readonly Dictionary<string,Total> totals=new Dictionary<string,Total>();
        private readonly List<MethodFrame> top=new List<MethodFrame>(MaxSpikeDetails+1);
        private double start,end;
        private long first,last,omittedMethods,omittedCalls,omittedSamples,omittedSpikes;
        private double omittedMs;
        private int frames,spikeFrames;
        internal void Reset(double time){totals.Clear();top.Clear();start=end=time;first=last=omittedMethods=omittedCalls=omittedSamples=omittedSpikes=0;omittedMs=0;frames=spikeFrames=0;}
        internal void Observe(FrameRecord frame,Action<StructuredEvent> emit)
        {
            if(frames>0&&frame.Time-start>=1)Flush(emit);
            if(frames==0){start=frame.Time;first=frame.Frame;}end=frame.Time;last=frame.Frame;frames++;
            bool spike=frame.DurationMs>FpsTracker.SpikeThresholdMs;
            bool detail=spike&&spikeFrames++<MaxSpikeFramesPerWindow;top.Clear();
            foreach(var m in frame.Methods)
            {
                string key=m.Scope+"|"+m.Id+"|"+m.PluginGuid;
                if(!totals.TryGetValue(key,out var t))
                {
                    if(totals.Count>=MaxMethods){omittedMethods++;omittedCalls+=m.Calls;omittedSamples+=m.Samples;omittedMs+=m.Ms;continue;}
                    totals[key]=t=new Total{Id=EventCodec.Limit(m.Id,512),Name=EventCodec.Limit(m.Name,512),Owner=EventCodec.Limit(m.Owners,512),Scope=m.Scope,Plugin=m.PluginGuid};
                }
                t.Calls+=m.Calls;t.Samples+=m.Samples;t.Errors+=m.Exceptions;t.Ms+=m.Ms;t.Frames++;
                if(m.Ms>t.Peak){t.Peak=m.Ms;t.PeakFrame=frame.Frame;}
                if(detail)
                {
                    int index=top.FindIndex(x=>x.Ms<m.Ms);if(index<0)index=top.Count;
                    if(index<MaxSpikeDetails){top.Insert(index,m);if(top.Count>MaxSpikeDetails)top.RemoveAt(MaxSpikeDetails);}
                }
            }
            if(spike)omittedSpikes+=Math.Max(0,frame.Methods.Count-top.Count);
            foreach(var m in top)emit(new StructuredEvent{FrameNumber=frame.Frame,Type=m.Scope=="PluginProfiler"?EventType.PluginTiming:EventType.PatchTiming,
                Fields=new Dictionary<string,object>{{"MethodId",m.Id},{"Name",m.Name},{"Owner",m.Owners},{"PluginGuid",m.PluginGuid},{"TotalMs",m.Ms},{"CallCount",m.Calls},{"SampleCount",m.Samples},{"SampleCoverage",m.Calls>0?(double)m.Samples/m.Calls:0},{"Exceptions",m.Exceptions},{"TimingKind","spike frame inclusive (subset of window totals)"},{"WindowStartSeconds",frame.Time},{"WindowEndSeconds",frame.Time}}});
        }
        internal void Flush(Action<StructuredEvent> emit)
        {
            if(frames==0)return;
            foreach(var t in totals.Values)emit(new StructuredEvent{FrameNumber=last,Type=t.Scope=="PluginProfiler"?EventType.PluginTiming:EventType.PatchTiming,
                Fields=new Dictionary<string,object>{{"MethodId",t.Id},{"Name",t.Name},{"Owner",t.Owner},{"PluginGuid",t.Plugin},{"TotalMs",t.Ms},{"CallCount",t.Calls},{"SampleCount",t.Samples},{"SampleCoverage",t.Calls>0?(double)t.Samples/t.Calls:0},{"Exceptions",t.Errors},
                    {"TimingKind","window inclusive"},{"WindowStartSeconds",start},{"WindowEndSeconds",end},{"FirstFrame",first},{"LastFrame",last},{"ObservedFrames",frames},{"MethodFrames",t.Frames},{"PeakFrameMs",t.Peak},{"PeakFrame",t.PeakFrame}}});
            emit(new StructuredEvent{FrameNumber=last,Type=EventType.TimingWindow,Fields=new Dictionary<string,object>{{"WindowStartSeconds",start},{"WindowEndSeconds",end},{"FirstFrame",first},{"LastFrame",last},{"ObservedFrames",frames},{"Methods",totals.Count},{"MethodObservationsOmitted",omittedMethods},{"OmittedCallCount",omittedCalls},{"OmittedSampleCount",omittedSamples},{"OmittedInclusiveMs",omittedMs},{"SpikeMethodDetailsOmitted",omittedSpikes}}});
            Reset(end);
        }
    }
}
