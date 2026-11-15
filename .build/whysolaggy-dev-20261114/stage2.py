from pathlib import Path
import re
root=Path(__file__).resolve().parents[2]/'MOD开发/WhySoLaggy/WhySoLaggy'
def edit(rel,fn):
 p=root/rel;s=p.read_text(encoding='utf-8-sig');p.write_text(fn(s),encoding='utf-8')
def replace(s,a,b):
 assert a in s,a[:120]
 return s.replace(a,b)
def logger(s):
 s=replace(s,'"SuppressedCount", "PeakRate",','"SuppressedCount", "PeakRate", "SchemaVersion", "SessionId", "RoomEpoch", "Sequence", "MonotonicSeconds", "CaptureId", "Validity",\n            "UnityAllocatedBytes", "ManagedUsedBytes", "ManagedCapacityBytes", "MemoryNetKBps", "GcAllocatedFrameBytes", "GcAllocationStatus", "GcCollections",\n            "P95FrameMs", "P99FrameMs", "Low1Fps", "SampleCount", "SampleCoverage", "BackgroundMs", "Exceptions", "MethodId", "TimingKind", "CoveredMs", "UnattributedMs", "SceneState",')
 s=replace(s,'public static void Initialize(string dir)\n        {','public static void Initialize(string dir)\n        {\n            DiagnosticIO.Start();')
 start=s.index('        public static void WriteEvent(');end=s.index('        private static void FlushBuffersLocked()',start)
 s=s[:start]+'''        internal static event Action<StructuredEvent> EventWritten;
        private static double nextFlush;
        public static void WriteEvent(StructuredEvent evt)
        {
            if (!_inited) return;
            var frozen=EventCodec.Freeze(evt);
            try {EventWritten?.Invoke(frozen);} catch { }
            bool important=evt.Type==EventType.AbuseAlert||evt.Type==EventType.ExceptionSummary||evt.Type==EventType.CaptureState;
            DiagnosticIO.Enqueue(()=>{long start=System.Diagnostics.Stopwatch.GetTimestamp();lock(_lock){AppendCsvRow(frozen);AppendJsonlRow(frozen);_eventsSinceMetrics++;if(_csvBuffer.Length+_jsonBuffer.Length>262144)FlushBuffersLocked();}SelfMetrics.Add("Serialization",DiagnosticClock.Milliseconds(System.Diagnostics.Stopwatch.GetTimestamp()-start));},EventCodec.Size(frozen),important);
        }
        public static void Tick()
        {if(!_inited||DiagnosticClock.Now<nextFlush)return;nextFlush=DiagnosticClock.Now+1;DiagnosticIO.Enqueue(()=>{lock(_lock)FlushBuffersLocked();TextLogSink.Flush();},128,true);}
        public static void Flush()
        {if(!_inited)return;Action action=()=>{lock(_lock)FlushBuffersLocked();TextLogSink.Flush();};if(DiagnosticContext.IsMainThread)DiagnosticIO.Enqueue(action,128,true);else DiagnosticIO.Barrier(action);}
        public static void Shutdown()
        {
            if(!_inited)return;
            DiagnosticIO.Barrier(()=>{lock(_lock){FlushBuffersLocked();CloseWriters();_csvBuffer.Clear();_jsonBuffer.Clear();_inited=false;_eventsSinceMetrics=0;}TextLogSink.Close();});
            DiagnosticIO.Stop();
        }

'''+s[end:]
 s=replace(s,'long started = DateTime.UtcNow.Ticks;','long started = System.Diagnostics.Stopwatch.GetTimestamp();')
 s=replace(s,'_lastFlushMs = Math.Max(0d, (_lastFlushTicks - started) * 1000d / TimeSpan.TicksPerSecond);','_lastFlushMs = DiagnosticClock.Milliseconds(System.Diagnostics.Stopwatch.GetTimestamp()-started);\n                SelfMetrics.Add("Flush",_lastFlushMs);')
 s=replace(s,'WhySoLaggyPlugin.Log?.LogWarning($"[WHY_LAG] StructuredLogger flush failed: {ex.Message}");','DiagnosticIO.Error = "Flush failed: " + ex.Message;')
 return s
edit('Helpers/StructuredLogger.cs',logger)
edit('Helpers/StructuredEvent.cs',lambda s:s.replace('        FpsReport,','        FrameSample, ExceptionSummary, NetworkSample, CaptureState, SessionBoundary, Marker,\n        FpsReport,'))
for name,kind in [('LagLogger','Performance'),('AbuseLogger','Abuse')]:
 source='''using System;
using System.IO;
namespace WhySoLaggy
{
    internal static class NAME
    {
        private static string path;
        public static void Initialize(string dir){path=Path.Combine(dir,"FILENAME");DiagnosticIO.Start();Info("[WHY_LAG] KIND monitor session "+DiagnosticContext.SessionId);}
        public static void Info(string message){if(path!=null)TextLogSink.Write(path,"["+StructuredLogger.NowStamp()+"] "+message,true);}
        public static void Write(string message){if(LogFilter.ALLOW())Raw(message,false);}
        public static void WriteRaw(string message){if(LogFilter.ALLOW())Raw(message,false);}
        private static void Raw(string message,bool important){if(path!=null)TextLogSink.Write(path,"["+StructuredLogger.NowStamp()+"] "+message,important);}
        public static void Flush()=>StructuredLogger.Flush();
        public static void Shutdown(){if(path!=null)Info("Session ended");path=null;}
        EXTRA
    }
}'''.replace('NAME',name).replace('FILENAME','WhySoLaggy.log' if name=='LagLogger' else 'WhySoLaggy_Abuse.log').replace('KIND',kind).replace('ALLOW','AllowLag' if name=='LagLogger' else 'AllowAbuse').replace('EXTRA','' if name=='LagLogger' else '''public static void Alert(string message){Raw("ABUSE ALERT: "+message,true);WhySoLaggyPlugin.Log?.LogWarning("[WHY_LAG] "+message);if(DiagnosticContext.IsMainThread)PerformanceDashboard.ReportAlert(message);}
        public static void AlertDetail(string message)=>Raw(message,true);
        public static void AlertDetailRaw(string message)=>Raw(message,true);''')
 (root/'Helpers'/f'{name}.cs').write_text(source,encoding='utf-8')
def formatter(s):
 a=s.index('        private static string FormatEnumerable(');b=s.index('        private static string RuntimeHashCode',a)
 s=s[:a]+'''        [ThreadStatic] private static int depth;
        private static string FormatEnumerable(IEnumerable en)
        {
            if(depth>=3)return "[depth limit]";
            if(!(en is IList list))return "["+en.GetType().Name+": enumeration omitted]";
            depth++;
            try{var sb=new StringBuilder("[");int count=list.Count;for(int i=0;i<Math.Min(count,3);i++){if(i>0)sb.Append(", ");if(ReferenceEquals(list[i],en))sb.Append("[cycle]");else sb.Append(Truncate(FormatCore(list[i]),32));}if(count>3)sb.Append(", ...");return sb.Append("] n=").Append(count).ToString();}
            finally{depth--;}
        }

'''+s[b:]
 return s
edit('Helpers/ValueFormatter.cs',formatter)
print('Updated background IO, event schema, text logs and bounded formatting')
