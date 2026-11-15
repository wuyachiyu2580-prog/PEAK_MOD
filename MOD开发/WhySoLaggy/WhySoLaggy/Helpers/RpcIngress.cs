using System;
using System.Collections.Generic;
using System.Linq;

namespace WhySoLaggy
{
    internal sealed class RpcAggregate
    { internal string Method; internal int Actor,Count,MaxBytes;internal long Bytes; }
    internal static class RpcIngress
    {
        private static readonly object gate=new object();
        private static readonly Dictionary<Tuple<string,int>,RpcAggregate> counts=new Dictionary<Tuple<string,int>,RpcAggregate>();
        private static readonly Dictionary<Tuple<string,int>,Tuple<double,int>> limits=new Dictionary<Tuple<string,int>,Tuple<double,int>>();
        internal static long SampledOut,Truncated;
        internal static bool Record(string method,int actor,int bytes,double now)
        {
            method=EventCodec.Limit(method,256);
            lock(gate)
            {
                var key=Tuple.Create(method,actor);
                if(!counts.ContainsKey(key)&&counts.Count>=4096)key=Tuple.Create("[overflow]",-1);
                if(!counts.TryGetValue(key,out var aggregate))counts[key]=aggregate=new RpcAggregate{Method=key.Item1,Actor=key.Item2};
                aggregate.Count++;aggregate.Bytes+=bytes;aggregate.MaxBytes=Math.Max(aggregate.MaxBytes,bytes);
                if(limits.Count>=4096&&!limits.ContainsKey(key)){SampledOut++;return false;}
                limits.TryGetValue(key,out var limit);
                if(limit==null||now-limit.Item1>=1)limit=Tuple.Create(now,0);
                limits[key]=Tuple.Create(limit.Item1,limit.Item2+1);
                if(limit.Item2>=8){SampledOut++;return false;}return true;
            }
        }
        internal static RpcAggregate[] Drain()
        {lock(gate){var result=counts.Values.ToArray();counts.Clear();foreach(var key in limits.Where(x=>DiagnosticClock.Now-x.Value.Item1>2).Select(x=>x.Key).ToArray())limits.Remove(key);return result;}}
        internal static object[] CopyArgs(object[] source)
        {
            if(source==null)return null;int n=Math.Min(16,source.Length);var result=new object[n];
            if(source.Length>n)System.Threading.Interlocked.Increment(ref Truncated);
            for(int i=0;i<n;i++)
            {
                object v=source[i];
                if(v==null||v is bool||v is byte||v is short||v is int||v is long||v is float||v is double||v is UnityEngine.Vector2||v is UnityEngine.Vector3||v is UnityEngine.Quaternion)result[i]=v;
                else if(v is string s){result[i]=EventCodec.Limit(s,128);if(s.Length>128)System.Threading.Interlocked.Increment(ref Truncated);}
                else if(v is byte[] b){result[i]=b.Take(64).ToArray();if(b.Length>64)System.Threading.Interlocked.Increment(ref Truncated);}
                else if(v is float[] f){result[i]=f.Take(16).ToArray();if(f.Length>16)System.Threading.Interlocked.Increment(ref Truncated);}
                else result[i]="["+v.GetType().Name+"]";
            }
            return result;
        }
        internal static int SnapshotBytes(object[] args)
        {int bytes=32;if(args!=null)foreach(var x in args)bytes+=x is string s?32+s.Length*2:x is byte[] b?32+b.Length:x is float[] f?32+f.Length*4:32;return bytes;}
        internal static void Reset(){lock(gate){counts.Clear();limits.Clear();SampledOut=Truncated=0;}}
    }
}
