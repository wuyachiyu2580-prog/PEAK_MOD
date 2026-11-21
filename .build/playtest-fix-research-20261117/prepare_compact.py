from pathlib import Path
import shutil
OUT=Path(__file__).resolve().parent
dest=OUT/'compact_network';dest.mkdir(exist_ok=True)
for name in ['CaptureBuffer.cs','CaptureTiming.cs','StructuredEvent.cs','Support.cs','Replay.csproj']:
 shutil.copy2(OUT/'baseline'/name,dest/name)
p=dest/'CaptureTiming.cs';s=p.read_text(encoding='utf-8')
s=s.replace('private readonly long[] values=new long[Columns.Length];','private static readonly string[] NetworkColumns={"SchemaVersion","RoomEpoch","Sequence","MonotonicSeconds","Count","ActualWindowSeconds","Rate","Ping","RttVariance","IncomingCommands","OutgoingCommands","ReliableRetransmissions","RpcQueueDepth","QueueDelayMs","DetailDroppedTotal","DetailSampledOut","CpuFrameMs","GpuFrameMs","PresentWaitMs"};'+chr(10)+'        private static readonly HashSet<int> NetworkFloating=new HashSet<int>{3,5,6,13,16,17,18};'+chr(10)+'        private readonly long[] values; private readonly string[] columns; private readonly HashSet<int> floating; private readonly int nulls; internal int PayloadSize=>type==EventType.NetworkSample?400:PayloadBytes;')
s=s.replace('Identity=identity;type=e.Type;frame=e.FrameNumber;timestamp=e.Timestamp;','Identity=identity;type=e.Type;frame=e.FrameNumber;timestamp=e.Timestamp;columns=type==EventType.NetworkSample?NetworkColumns:Columns;floating=type==EventType.NetworkSample?NetworkFloating:Floating;values=new long[columns.Length];')
s=s.replace('for(int i=0;i<Columns.Length;i++)if(e.Fields.TryGetValue(Columns[i],out var v)&&v!=null)','for(int i=0;i<columns.Length;i++)if(e.Fields.TryGetValue(columns[i],out var v))')
s=s.replace('{present|=1<<i;values[i]=Floating.Contains(i)?BitConverter.DoubleToInt64Bits(Convert.ToDouble(v)):Convert.ToInt64(v);}','{present|=1<<i;if(v==null){nulls|=1<<i;continue;}values[i]=floating.Contains(i)?BitConverter.DoubleToInt64Bits(Convert.ToDouble(v)):Convert.ToInt64(v);}')
s=s.replace('for(int i=0;i<Columns.Length;i++)if((present&(1<<i))!=0)fields[Columns[i]]=Floating.Contains(i)?(object)BitConverter.Int64BitsToDouble(values[i]):values[i];','for(int i=0;i<columns.Length;i++)if((present&(1<<i))!=0)fields[columns[i]]=(nulls&(1<<i))!=0?null:floating.Contains(i)?(object)BitConverter.Int64BitsToDouble(values[i]):values[i];')
p.write_text(s,encoding='utf-8')
p=dest/'CaptureBuffer.cs';s=p.read_text(encoding='utf-8')
s=s.replace('(e.Type==EventType.PluginTiming||e.Type==EventType.PatchTiming)&&e.Fields!=null&&e.Fields.ContainsKey("WindowStartSeconds")','e.Fields!=null&&(e.Type==EventType.NetworkSample||((e.Type==EventType.PluginTiming||e.Type==EventType.PatchTiming)&&e.Fields.ContainsKey("WindowStartSeconds")))')
s=s.replace('CaptureTiming.PayloadBytes','packed.PayloadSize')
s=s.replace('PackedTiming=details.Where','PackedTiming=anchors.Concat(critical).Concat(details).Where')
p.write_text(s,encoding='utf-8')
print('Prepared compact numeric network candidate; production files unchanged')
