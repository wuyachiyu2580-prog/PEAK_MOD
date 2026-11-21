using System; using System.Linq; namespace WhySoLaggy {internal static class CapturePayloadBudget
    {
        internal const long Limit=32L*1024*1024;
        private static long bytes;
        internal static long Bytes=>System.Threading.Interlocked.Read(ref bytes);
        internal static bool Reserve(long size)
        {long old;do{old=Bytes;if(size<0||old+size>Limit)return false;}while(System.Threading.Interlocked.CompareExchange(ref bytes,old+size,old)!=old);return true;}
        internal static void Release(long size)=>System.Threading.Interlocked.Add(ref bytes,-size);
    } internal static class EventCodec {internal static int Size(StructuredEvent e)=>256+(e.Timestamp?.Length??0)*2+(e.Fields?.Sum(x=>64+x.Key.Length*2+(x.Value is string s?s.Length*2:16))??0);}
 internal static class DiagnosticPhases {
 internal enum Stage { Pack }
 internal static readonly BoundedPhaseProfiler Profiler=new BoundedPhaseProfiler("Pack");
 internal static BoundedPhaseProfiler.Scope Measure(Stage stage)=>Profiler.Measure((int)stage);
 }}