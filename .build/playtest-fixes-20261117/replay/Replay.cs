using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Collections.Generic;
using System.Diagnostics;
using WhySoLaggy;

class Replay
{
 static long Seq(StructuredEvent e)=>Convert.ToInt64(e.Fields["Sequence"]);
 static double Time(StructuredEvent e)=>Convert.ToDouble(e.Fields["MonotonicSeconds"]);
 static object Val(JsonElement e)=>e.ValueKind switch {JsonValueKind.String=>e.GetString(),JsonValueKind.True=>true,JsonValueKind.False=>false,JsonValueKind.Number=>e.TryGetInt64(out long l)?(object)l:e.GetDouble(),_=>null};
 static StructuredEvent Read(string line)
 {
  using var d=JsonDocument.Parse(line);var root=d.RootElement;var fields=new Dictionary<string,object>();
  foreach(var p in root.EnumerateObject())if(p.Name!="type"&&p.Name!="timestamp"&&p.Name!="frameNumber")fields[char.ToUpperInvariant(p.Name[0])+p.Name.Substring(1)]=Val(p.Value);
  return new StructuredEvent{Type=Enum.Parse<EventType>(root.GetProperty("type").GetString()),Timestamp=root.GetProperty("timestamp").GetString(),FrameNumber=root.GetProperty("frameNumber").GetInt64(),Fields=fields};
 }
 static string Text(StructuredEvent e,string k)=>e.Fields.TryGetValue(k,out var v)?Convert.ToString(v):"";
 static bool EqualValue(object a,object b)
 {if(a==null||b==null)return a==null&&b==null;if(a is double||b is double)return Convert.ToDouble(a).Equals(Convert.ToDouble(b));return a.Equals(b);}
 static bool Same(StructuredEvent a,StructuredEvent b)=>a.Type==b.Type&&a.FrameNumber==b.FrameNumber&&a.Timestamp==b.Timestamp&&a.Fields.Count==b.Fields.Count&&a.Fields.All(kv=>b.Fields.TryGetValue(kv.Key,out var v)&&EqualValue(kv.Value,v));
 static Dictionary<string,int> Counts(IEnumerable<StructuredEvent> es)=>es.GroupBy(e=>e.Type.ToString()).ToDictionary(g=>g.Key,g=>g.Count());
 static StructuredEvent Synthetic(EventType type,long seq,double time)=>new StructuredEvent{Type=type,Timestamp="synthetic",FrameNumber=seq,Fields=new Dictionary<string,object>{{"Sequence",seq},{"MonotonicSeconds",time},{"Count",seq},{"Rate",1d},{"RpcMethod","Test"},{"SessionId","stress"}}};
 static List<string> Stress()
 {
  var tests=new List<string>();var ring=new EventRing(100000);ring.Add(Synthetic(EventType.ExceptionSummary,1,1),1,20);ring.Add(Synthetic(EventType.SpikeFrame,2,1),1,20);
  for(int i=0;i<100;i++)ring.Add(Synthetic(EventType.NetworkSample,10+i,1+i*.01),1+i*.01,20);
  var retained=ring.Snapshot();if(!retained.Any(e=>e.Type==EventType.ExceptionSummary)||!retained.Any(e=>e.Type==EventType.SpikeFrame)||ring.DroppedCritical==0)throw new Exception("Network burst protection failed");tests.Add("network burst preserves exception and spike with bounded drops");
  ring.Clear();if(CapturePayloadBudget.Bytes!=0)throw new Exception("budget leak");tests.Add("burst clear releases identities and payload");
  if(!CapturePayloadBudget.Reserve(CapturePayloadBudget.Limit))throw new Exception("reserve failed");ring.Add(Synthetic(EventType.NetworkSample,500,5),5,20);if(ring.DroppedCritical!=1)throw new Exception("global reserve failure not recorded");CapturePayloadBudget.Release(CapturePayloadBudget.Limit);ring.Clear();tests.Add("shared budget rejection counted");
  ring.Add(Synthetic(EventType.NetworkSample,501,10),10,20);ring.Add(Synthetic(EventType.NetworkSample,502,31),31,20);if(ring.Snapshot().Length!=1||ring.DroppedCritical!=0)throw new Exception("expiration miscount");tests.Add("natural expiration is not capacity loss");ring.Clear();
  ring.Add(Synthetic(EventType.NetworkSample,503,40),40,20);var record=ring.Detach();long bytes=CapturePayloadBudget.Bytes;if(bytes<=0)throw new Exception("detach not retained");record.Retain();record.Release();if(CapturePayloadBudget.Bytes!=bytes)throw new Exception("early release");record.Release();if(CapturePayloadBudget.Bytes!=0)throw new Exception("final release failed");tests.Add("detach and export reference lifetime balanced");return tests;
 }
 static void Main(string[] args)
 {
  var stress=args[1]=="compact_protected"?Stress():new List<string>();
  using var doc=JsonDocument.Parse(File.ReadAllText(args[0]));var r=doc.RootElement;string session=r.GetProperty("session").GetString();
  var expected=r.GetProperty("archives").EnumerateArray().ToDictionary(x=>x.GetProperty("id").GetString(),x=>x.Clone());
  var rolling=new EventRing(4*1024*1024);EventRing recording=null;var recent=new Queue<StructuredEvent>();var source=new List<StructuredEvent>();
  var rows=new List<object>();string active="";double started=0;int preMissing=0;long peak=0;var watch=Stopwatch.StartNew();
  if(args[1].StartsWith("compact"))
  {
   var edge=new StructuredEvent{Type=EventType.NetworkSample,Timestamp="edge",FrameNumber=9007199254740993L,Fields=new Dictionary<string,object>{{"Sequence",9007199254740993L},{"Count",long.MaxValue},{"Rate",.125d},{"CpuFrameMs",null},{"RpcMethod","Test"}}};
   var packed=new CaptureNetwork(edge,new CaptureNetworkIdentity(edge.Fields,CaptureNetworkIdentity.KeyOf(edge.Fields)));if(!Same(edge,packed.Expand()))throw new Exception("Network round-trip edge failed");
  }
  foreach(var file in r.GetProperty("files").EnumerateArray())foreach(var line in File.ReadLines(file.GetString()))
  {
   var e=Read(line);if(Text(e,"SessionId")!=session)continue;double now=Time(e);string id=Text(e,"CaptureId");
   while(recent.Count>0&&Time(recent.Peek())<now-20)recent.Dequeue();
   bool begin=e.Type==EventType.CaptureState&&Text(e,"Phase")=="Plugins"&&expected.ContainsKey(id)&&recording==null;
   if(begin)
   {
    active=id;started=expected[id].GetProperty("start").GetDouble();source.Clear();source.AddRange(recent);
    var retained=rolling.Snapshot().Select(Seq).ToHashSet();preMissing=recent.Count(x=>!retained.Contains(Seq(x)));
    recording=new EventRing(8*1024*1024);rolling.CopyTo(recording,started,20);
   }
   recent.Enqueue(e);rolling.Add(e,now,20);recording?.Add(e,now,90);if(recording!=null)source.Add(e);peak=Math.Max(peak,CapturePayloadBudget.Bytes);
   if(recording!=null&&e.Type==EventType.CaptureState&&Text(e,"Phase")=="Saving")
   {
    var record=recording.Detach();recording=null;var es=record.Enumerate().ToArray();var ids=es.Select(Seq).ToHashSet();var actual=expected[active].GetProperty("actualSequences").EnumerateArray().Select(x=>x.GetInt64()).ToHashSet();
    long min=actual.Min(),max=actual.Max();var missing=source.Where(x=>Seq(x)>=min&&Seq(x)<=max&&!ids.Contains(Seq(x))).ToArray();
    var original=source.ToDictionary(Seq);var net=es.Where(x=>x.Type==EventType.NetworkSample).ToArray();int mismatch=net.Count(x=>!original.TryGetValue(Seq(x),out var src)||!Same(src,x));if(mismatch!=0)throw new Exception("Network round-trip failed: "+mismatch);
    record.Id=active;record.Started=started;record.Ended=now;record.Reason="replay";
    rows.Add(new {file=expected[active].GetProperty("file").GetString(),frames=record.PackedFrames.Length,record.DroppedFrames,record.DroppedCritical,record.DroppedDetails,preMissing,networkFieldChecks=net.Length,networkMismatches=mismatch,actualDifferenceMissing=actual.Except(ids).Count(),actualDifferenceExtra=ids.Except(actual).Count(),missing=Counts(missing),preTriggerMissing=Counts(missing.Where(x=>Time(x)<started)),recordingMissing=Counts(missing.Where(x=>Time(x)>=started)),payload=record.Bytes,preRollLoss=new {lower=record.PreRollLoss.Total.Lower,upper=record.PreRollLoss.Total.Upper,quality=record.PreRollLoss.Total.Quality,rows=record.PreRollLoss.Rows("PreRoll")},recordingLoss=record.RecordingLoss.Rows("Recording")});
    record.Release();active="";source.Clear();
   }
  }
  rolling.Clear();recording?.Clear();var result=new{variant=args[1],stressChecks=stress,elapsedSeconds=watch.Elapsed.TotalSeconds,peakPayloadBytes=peak,remainingPayloadBytes=CapturePayloadBudget.Bytes,captures=rows};
  File.WriteAllText(args[2],JsonSerializer.Serialize(result,new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine(JsonSerializer.Serialize(result));
 }
}
