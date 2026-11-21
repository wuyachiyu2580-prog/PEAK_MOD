using System;
using System.Collections.Generic;
using System.Linq;

namespace WhySoLaggy
{
    internal sealed class CaptureTimingIdentity
    {
        internal readonly string Key;
        internal readonly Dictionary<string,object> Strings;
        internal int References;
        internal readonly int Bytes;
        internal static string KeyOf(StructuredEvent e)=>string.Join("\u001f",e.Fields.Where(x=>x.Value is string).OrderBy(x=>x.Key,StringComparer.Ordinal).Select(x=>x.Key+"="+x.Value));
        internal CaptureTimingIdentity(StructuredEvent e,string key)
        {
            Key=key;Strings=e.Fields.Where(x=>x.Value is string).ToDictionary(x=>x.Key,x=>x.Value);
            Bytes=EventCodec.Size(new StructuredEvent{Fields=Strings})+key.Length*2;
        }
    }
    internal sealed class CaptureTiming
    {
        // Numeric columns are fixed, with a presence mask to preserve absent fields.
        private static readonly string[] Columns={"SchemaVersion","RoomEpoch","Sequence","MonotonicSeconds","TotalMs","CallCount","SampleCount","SampleCoverage","Exceptions","WindowStartSeconds","WindowEndSeconds","FirstFrame","LastFrame","ObservedFrames","MethodFrames","PeakFrameMs","PeakFrame"};
        private static readonly HashSet<int> Floating=new HashSet<int>{3,4,7,9,10,15};
        internal const int PayloadBytes=320;
        internal readonly CaptureTimingIdentity Identity;
        private readonly long[] values=new long[Columns.Length];
        private readonly int present;
        private readonly EventType type;
        private readonly long frame;
        private readonly string timestamp;
        internal CaptureTiming(StructuredEvent e,CaptureTimingIdentity identity)
        {
            Identity=identity;type=e.Type;frame=e.FrameNumber;timestamp=e.Timestamp;
            for(int i=0;i<Columns.Length;i++)if(e.Fields.TryGetValue(Columns[i],out var v)&&v!=null)
            {present|=1<<i;values[i]=Floating.Contains(i)?BitConverter.DoubleToInt64Bits(Convert.ToDouble(v)):Convert.ToInt64(v);}
        }
        internal StructuredEvent Expand()
        {
            var fields=new Dictionary<string,object>(Identity.Strings);
            for(int i=0;i<Columns.Length;i++)if((present&(1<<i))!=0)fields[Columns[i]]=Floating.Contains(i)?(object)BitConverter.Int64BitsToDouble(values[i]):values[i];
            return new StructuredEvent{Type=type,FrameNumber=frame,Timestamp=timestamp,Fields=fields};
        }
    }
}
