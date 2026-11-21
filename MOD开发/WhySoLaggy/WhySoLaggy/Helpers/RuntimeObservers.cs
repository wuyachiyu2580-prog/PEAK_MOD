using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Photon.Pun;
using UnityEngine;

namespace WhySoLaggy
{
    internal sealed class ErrorSummary
    {internal string Key,Kind,Stack,Sample;internal int Count,WindowCount;internal double First,Last;}
    internal static class ExceptionObserver
    {
        private static readonly object gate=new object();
        private static readonly Dictionary<string,ErrorSummary> errors=new Dictionary<string,ErrorSummary>();
        private static readonly BoundedConcurrentQueue<Tuple<string,string,double>> incoming=new BoundedConcurrentQueue<Tuple<string,string,double>>(512);
        internal static ErrorSummary[] Recent=new ErrorSummary[0];
        internal static long Dropped;
        private static double last;
        internal static void Start(){Application.logMessageReceivedThreaded+=OnLog;}
        internal static void Stop(){Recent=new ErrorSummary[0];Application.logMessageReceivedThreaded-=OnLog;lock(gate)errors.Clear();incoming.Clear();}
        private static void OnLog(string message,string stack,LogType type)
        {
            if(type!=LogType.Exception&&type!=LogType.Error&&type!=LogType.Assert)return;
            if((message??"").StartsWith("[WHY_LAG]",StringComparison.Ordinal))return;
            if(incoming.Enqueue(Tuple.Create(EventCodec.Limit(message,512),EventCodec.Limit(stack,1800),DiagnosticClock.Now)).Dropped)System.Threading.Interlocked.Increment(ref Dropped);
        }
        internal static string Normalize(string stack)
        {
            string s=Regex.Replace(stack??"",@"\[0x[0-9a-fA-F]+\]| in .*?:[0-9]+", "",RegexOptions.CultureInvariant,TimeSpan.FromMilliseconds(30));
            return string.Join("\n",s.Split(new[]{'\n','\r'},StringSplitOptions.RemoveEmptyEntries).Take(6).Select(x=>x.Trim()));
        }
        internal static void Add(string message,string stack,double now)
        {
            var typeMatch=Regex.Match(message??"",@"^[A-Za-z_][A-Za-z0-9_.]*(?:Exception|Error)(?=[:\s]|$)");string kind=typeMatch.Success?typeMatch.Value:"Error";string normalized=Normalize(stack);string key=kind+"|"+normalized;
            lock(gate)
            {
                if(!errors.TryGetValue(key,out var error))
                {
                    if(errors.Count>=256){Dropped++;return;}
                    errors[key]=error=new ErrorSummary{Key=key,Kind=kind,Stack=normalized,Sample=message,First=now};
                }
                error.Count++;error.WindowCount++;error.Last=now;
            }
        }
        internal static void Tick()
        {
            long started=System.Diagnostics.Stopwatch.GetTimestamp();int n=0;
            while(n++<32&&DiagnosticClock.Milliseconds(System.Diagnostics.Stopwatch.GetTimestamp()-started)<.25&&incoming.TryDequeue(out var item))
                try{Add(item.Item1,item.Item2,item.Item3);}catch{Dropped++;}
            if(DiagnosticClock.Now-last<1)return;last=DiagnosticClock.Now;
            lock(gate)
            {
                Recent=errors.Values.OrderByDescending(x=>x.Last).Take(32).Select(x=>new ErrorSummary{Key=x.Key,Kind=x.Kind,Stack=x.Stack,Sample=x.Sample,Count=x.Count,First=x.First,Last=x.Last}).ToArray();
                foreach(var e in errors.Values.Where(x=>x.WindowCount>0))
                {
                    StructuredLogger.WriteEvent(new StructuredEvent{Timestamp=StructuredLogger.NowStamp(),FrameNumber=DiagnosticContext.Frame,Type=EventType.ExceptionSummary,
                        Fields=new Dictionary<string,object>{{"ExceptionKind",e.Kind},{"TraceStack",e.Stack},{"Message",e.Sample},{"Count",e.WindowCount},{"TotalCount",e.Count},{"FirstSeconds",e.First},{"LastSeconds",e.Last},{"Dropped",Dropped}}});e.WindowCount=0;
                }
                foreach(var key in errors.Where(x=>last-x.Value.Last>60).Select(x=>x.Key).ToArray())errors.Remove(key);
            }
            SelfMetrics.Add("Exceptions",DiagnosticClock.Milliseconds(System.Diagnostics.Stopwatch.GetTimestamp()-started));
        }
        internal static MethodBase[] Candidates()
        {
            var results=new List<MethodBase>();
            foreach(var error in Recent.Where(x=>DiagnosticClock.Now-x.Last<10))
            {
                var match=Regex.Match(error.Stack??"",@"(?:at )?([\w.]+)\s*\(");if(!match.Success)continue;
                string full=match.Groups[1].Value;int dot=full.LastIndexOf('.');if(dot<1)continue;
                string typeName=full.Substring(0,dot),method=full.Substring(dot+1);
                var types=AppDomain.CurrentDomain.GetAssemblies().Select(x=>x.GetType(typeName,false)).Where(x=>x!=null).ToArray();if(types.Length!=1)continue;
                var methods=types[0].GetMethods(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static|BindingFlags.DeclaredOnly).Where(x=>x.Name==method).ToArray();
                if(methods.Length==1&&ProfilerTargetResolver.TryResolve(methods[0],out var declared,out _))results.Add(declared);
            }
            return results.Distinct().Take(10).ToArray();
        }
    }

    internal static class NetworkObserver
    {
        private static readonly NetworkWindow window = new NetworkWindow();
        private static double last;
        private static object peer;
        private static object roomObject;
        private static bool connected;
        private static string room;
        private static int lastResent,epoch;
        internal static int? Ping,Variance,Incoming,Outgoing,Resent;
        internal static NetworkWindowSnapshot Window { get; private set; } = new NetworkWindowSnapshot();
        internal static bool CpuGpuEnabled;
        internal static double? CpuMs,GpuMs,PresentWaitMs;
        private static readonly FrameTiming[] timings=new FrameTiming[4];
        private static ulong lastTimestamp;
        internal static void Tick()
        {
            double now=DiagnosticClock.Now;
            try
            {
                object current=PhotonNetwork.NetworkingClient?.LoadBalancingPeer;
                string currentRoom=PhotonNetwork.CurrentRoom?.Name;
                if(!ReferenceEquals(peer,current)||!ReferenceEquals(roomObject,PhotonNetwork.CurrentRoom)||connected!=PhotonNetwork.IsConnected||room!=currentRoom||epoch!=DiagnosticContext.RoomEpoch)
                {
                    bool changed=peer!=null&&(room!=currentRoom||!ReferenceEquals(peer,current)||!ReferenceEquals(roomObject,PhotonNetwork.CurrentRoom)||connected!=PhotonNetwork.IsConnected);
                    peer=current;room=currentRoom;roomObject=PhotonNetwork.CurrentRoom;connected=PhotonNetwork.IsConnected;lastResent=-1;
                    RpcMonitor.ResetEpoch();NetworkAbuseDetector.ResetEpoch();
                    if(changed)DiagnosticContext.RoomEpoch++;
                    window.Clear(); Window = default;
                    epoch=DiagnosticContext.RoomEpoch;
                    StructuredLogger.WriteEvent(new StructuredEvent{Timestamp=StructuredLogger.NowStamp(),FrameNumber=DiagnosticContext.Frame,Type=EventType.SessionBoundary,Fields=new Dictionary<string,object>{{"RoomName",room??""}}});
                }
                if(now-last<1)return;last=now;
                if(PhotonNetwork.IsConnected&&current!=null)
                {
                    var p=PhotonNetwork.NetworkingClient.LoadBalancingPeer;
                    Ping=p.RoundTripTime;Variance=p.RoundTripTimeVariance;Incoming=p.QueuedIncomingCommands;Outgoing=p.QueuedOutgoingCommands;
                    int value=p.ResentReliableCommands;Resent=lastResent>=0&&value>=lastResent?(int?)(value-lastResent):null;lastResent=value;
                }
                else {Ping=Variance=Incoming=Outgoing=Resent=null;lastResent=-1;}
            }
            catch{Ping=Variance=Incoming=Outgoing=Resent=null;}
            window.Add(now, Ping, Incoming, Outgoing);
            Window = window.Snapshot(now);
            CpuMs=GpuMs=PresentWaitMs=null;
            if(CpuGpuEnabled)try
            {
                if(FrameTimingManager.IsFeatureEnabled())
                {
                    uint count=FrameTimingManager.GetLatestTimings(4,timings);
                    if(count>0&&timings[0].frameStartTimestamp>lastTimestamp)
                    {lastTimestamp=timings[0].frameStartTimestamp;CpuMs=timings[0].cpuFrameTime>0?(double?)timings[0].cpuFrameTime:null;GpuMs=timings[0].gpuFrameTime>0?(double?)timings[0].gpuFrameTime:null;PresentWaitMs=timings[0].cpuMainThreadPresentWaitTime;}
                    FrameTimingManager.CaptureFrameTimings();
                }
            }catch{}
            StructuredLogger.WriteEvent(new StructuredEvent{Timestamp=StructuredLogger.NowStamp(),FrameNumber=DiagnosticContext.Frame,Type=EventType.NetworkSample,
                Fields=new Dictionary<string,object>{{"Ping",Ping},{"RttVariance",Variance},{"IncomingCommands",Incoming},{"OutgoingCommands",Outgoing},{"ReliableRetransmissions",Resent},
                    {"RttSampleCount30s",Window.Rtt.Count},{"RttMin30s",Window.Rtt.Minimum},{"RttMedian30s",Window.Rtt.Median},{"RttP95_30s",Window.Rtt.P95},{"RttMax30s",Window.Rtt.Maximum},
                    {"IncomingCommandsSampleCount30s",Window.IncomingCommands.Count},{"IncomingCommandsMin30s",Window.IncomingCommands.Minimum},{"IncomingCommandsMedian30s",Window.IncomingCommands.Median},{"IncomingCommandsP95_30s",Window.IncomingCommands.P95},{"IncomingCommandsMax30s",Window.IncomingCommands.Maximum},
                    {"OutgoingCommandsSampleCount30s",Window.OutgoingCommands.Count},{"OutgoingCommandsMin30s",Window.OutgoingCommands.Minimum},{"OutgoingCommandsMedian30s",Window.OutgoingCommands.Median},{"OutgoingCommandsP95_30s",Window.OutgoingCommands.P95},{"OutgoingCommandsMax30s",Window.OutgoingCommands.Maximum},
                    {"RpcQueueDepth",RpcMonitor.LastDepth},{"QueueDelayMs",RpcMonitor.LastQueueDelayMs},{"DetailDroppedTotal",RpcMonitor.TotalDropped},{"DetailSampledOut",RpcIngress.SampledOut},
                    {"RpcDetailsTruncated",RpcIngress.Truncated},
                    {"CpuFrameMs",CpuMs},{"GpuFrameMs",GpuMs},{"PresentWaitMs",PresentWaitMs},{"CpuGpuStatus",CpuMs.HasValue||GpuMs.HasValue?"Available":"Unavailable"}}});
        }
    }
}
