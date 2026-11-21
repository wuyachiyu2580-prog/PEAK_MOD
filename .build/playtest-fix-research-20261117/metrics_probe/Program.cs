using System;
using System.Linq;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Text.Json;
using Probe;
class Program
{
 static object Measure(Action<string,double,bool> add)
 {
  for(int i=0;i<1000;i++)add("Runtime",.25,false);long before=GC.GetAllocatedBytesForCurrentThread();var sw=Stopwatch.StartNew();
  for(int i=0;i<1000000;i++)add("Runtime",.25,false);double ms=sw.Elapsed.TotalMilliseconds;long bytes=GC.GetAllocatedBytesForCurrentThread()-before;return new{calls=1000000,bytes,bytesPerCall=bytes/1000000d,ms};
 }
 static void Main()
 {
  _=DiagnosticContext.MainId;var before=Measure(SelfMetrics.Add);var after=Measure(CandidateSelfMetrics.Add);var reused=Measure(ReusableSelfMetrics.Add);SelfMetrics.Snapshot(true);CandidateSelfMetrics.Snapshot(true);ReusableSelfMetrics.Snapshot(true);
  Parallel.For(0,4,_=>{for(int i=0;i<25000;i++){SelfMetrics.Add("Thread",.5,false);CandidateSelfMetrics.Add("Thread",.5,false);ReusableSelfMetrics.Add("Thread",.5,false);}});
  var a=SelfMetrics.Snapshot();var b=CandidateSelfMetrics.Snapshot();var c=ReusableSelfMetrics.Snapshot();if(a.Count!=b.Count||a.Any(kv=>!b[kv.Key].Equals(kv.Value)||!c[kv.Key].Equals(kv.Value)))throw new Exception("Concurrent accumulation differs");
  if((long)b["ThreadCount"]!=100000||(double)b["ThreadTotalMs"]!=50000)throw new Exception("Total mismatch");
  CandidateSelfMetrics.Snapshot(true);if(CandidateSelfMetrics.Snapshot().Count!=0)throw new Exception("Reset failure");
  CandidateSelfMetrics.Add("MainUpdate",2);CandidateSelfMetrics.Add("Serialization",30);CandidateSelfMetrics.Add("FrameBoundary",3);CandidateSelfMetrics.Add("Hud",1);if(CandidateSelfMetrics.TakeFrame()!=6||CandidateSelfMetrics.TakeFrame()!=0)throw new Exception("Frame scope mismatch");
  ReusableSelfMetrics.Snapshot(true);if(ReusableSelfMetrics.Snapshot().Count!=0)throw new Exception("Reusable reset failure");ReusableSelfMetrics.Add("MainUpdate",2);ReusableSelfMetrics.Add("Serialization",30);ReusableSelfMetrics.Add("FrameBoundary",3);ReusableSelfMetrics.Add("Hud",1);if(ReusableSelfMetrics.TakeFrame()!=6||ReusableSelfMetrics.TakeFrame()!=0)throw new Exception("Reusable frame scope mismatch");
  Console.WriteLine(JsonSerializer.Serialize(new{runtime=Environment.Version.ToString(),baseline=before,valueCandidate=after,reusableCandidate=reused,concurrentTotalsEqual=true,resetAndFrameScopePass=true,limitation=".NET isolated measurement, not Unity Mono or in-game FPS"}));
 }
}
