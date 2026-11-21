using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace WhySoLaggy
{
    internal static class DiagnosticIO
    {
        private sealed class Job {internal Action Action;internal int Bytes;internal bool Important;}
        private static readonly object gate=new object();
        private static readonly LinkedList<Job> queue=new LinkedList<Job>();
        private static readonly AutoResetEvent signal=new AutoResetEvent(false);
        private static Thread worker;
        private static bool stopping;
        private static long bytes,dropped;
        internal static string Error="";
        internal static long Dropped=>Interlocked.Read(ref dropped);
        internal static long PendingBytes {get{lock(gate)return bytes;}}
        internal static bool IsWorker=>Thread.CurrentThread==worker;
        internal static void Start()
        {
            lock(gate){if(worker!=null&&worker.IsAlive)return;stopping=false;Error="";worker=new Thread(Run){IsBackground=true,Name="WhySoLaggy IO"};worker.Start();}
        }
        internal static bool Enqueue(Action action,int size=256,bool important=false)
        {
            lock(gate)
            {
                if(stopping||size<0||size>4*1024*1024){Interlocked.Increment(ref dropped);return false;}
                while(queue.Count>=4096||bytes+size>4*1024*1024)
                {
                    var victim=queue.First;while(victim!=null&&victim.Value.Important)victim=victim.Next;
                    if(victim==null||!important){Interlocked.Increment(ref dropped);return false;}
                    bytes-=victim.Value.Bytes;queue.Remove(victim);Interlocked.Increment(ref dropped);
                }
                queue.AddLast(new Job{Action=action,Bytes=size,Important=important});bytes+=size;
            }
            signal.Set();return true;
        }
        internal static bool Barrier(Action action,int timeout=2000)
        {
            if(IsWorker){action();return true;}
            var done=new System.Threading.Tasks.TaskCompletionSource<bool>();
            if(!Enqueue(()=>{try{action();}finally{done.TrySetResult(true);}},256,true))return false;
            return done.Task.Wait(timeout);
        }
        private static void Run()
        {
            while(true)
            {
                Job job=null;lock(gate){if(queue.Count>0){job=queue.First.Value;queue.RemoveFirst();bytes-=job.Bytes;}else if(stopping)break;}
                if(job==null){signal.WaitOne(100);continue;}
                try{job.Action();}catch(Exception ex){Error=ex.GetType().Name+": "+ex.Message;}
            }
        }
        internal static bool Stop(int timeout=2000)
        {
            lock(gate)stopping=true;signal.Set();
            bool done=worker==null||worker.Join(timeout);if(!done)Error="Shutdown incomplete; background writes pending";return done;
        }
    }

    internal static class EventCodec
    {
        private static long sequence;
        internal static StructuredEvent Freeze(StructuredEvent input)
        {
            var fields=new Dictionary<string,object>(StringComparer.Ordinal);
            int remaining=12000;
            if(input.Fields!=null)foreach(var pair in input.Fields.Take(100))
            {
                if(remaining<=0)break;string key=Limit(pair.Key,80);object value=pair.Value;
                if(value is string s){value=Limit(s,Math.Min(2048,remaining));remaining-=((string)value).Length;}
                else if(value!=null&&!value.GetType().IsPrimitive&&!(value is decimal))value="[unsupported value]";
                fields[key]=value;
            }
            fields["SchemaVersion"]=2;fields["SessionId"]=DiagnosticContext.SessionId;fields["RoomEpoch"]=DiagnosticContext.RoomEpoch;
            fields["Sequence"]=Interlocked.Increment(ref sequence);fields["MonotonicSeconds"]=DiagnosticClock.Now;fields["CaptureId"]=DiagnosticContext.CaptureId;
            if(!fields.ContainsKey("Validity"))fields["Validity"]=DiagnosticContext.Valid?"Complete":"Incomplete";
            return new StructuredEvent{Timestamp=input.Timestamp??StructuredLogger.NowStamp(),FrameNumber=input.FrameNumber,Type=input.Type,Fields=fields};
        }
        internal static int Size(StructuredEvent e)=>256+(e.Timestamp?.Length??0)*2+(e.Fields?.Sum(x=>64+x.Key.Length*2+(x.Value is string s?s.Length*2:16))??0);
        internal static string Limit(string value,int length)=>string.IsNullOrEmpty(value)?value??"":value.Length<=length?value:value.Substring(0,Math.Max(0,length-3))+"...";
        internal static string Json(object value)
        {
            if(value==null)return "null";
            if(value is string s)return "\""+Escape(s)+"\"";
            if(value is bool b)return b?"true":"false";
            if(value is double d&&(double.IsNaN(d)||double.IsInfinity(d)))return "null";
            if(value is float f&&(float.IsNaN(f)||float.IsInfinity(f)))return "null";
            if(value is IDictionary<string,object> map)return "{"+string.Join(",",map.Select(x=>Json(x.Key)+":"+Json(x.Value)))+"}";
            if(value is System.Collections.IEnumerable list){var parts=new List<string>();foreach(var x in list)parts.Add(Json(x));return "["+string.Join(",",parts)+"]";}
            return Convert.ToString(value,CultureInfo.InvariantCulture);
        }
        private static string Escape(string s)
        {
            var b=new StringBuilder();foreach(char c in s){switch(c){case '"':b.Append("\\\"");break;case '\\':b.Append("\\\\");break;case '\n':b.Append("\\n");break;case '\r':b.Append("\\r");break;case '\t':b.Append("\\t");break;default:if(c<32)b.Append("\\u"+((int)c).ToString("x4"));else b.Append(c);break;}}return b.ToString();
        }
        internal static string EventJson(StructuredEvent e)
        {var data=new Dictionary<string,object>{{"timestamp",e.Timestamp},{"frameNumber",e.FrameNumber},{"type",e.Type.ToString()}};if(e.Fields!=null)foreach(var p in e.Fields)data[char.ToLowerInvariant(p.Key[0])+p.Key.Substring(1)]=p.Value;return Json(data);}
    }

    internal static class TextLogSink
    {
        private static readonly Dictionary<string,StreamWriter> writers=new Dictionary<string,StreamWriter>();
        private static readonly Dictionary<string,double> retries=new Dictionary<string,double>();
        internal static void Write(string path,string text,bool important)
        {
            text=EventCodec.Limit(text,16000);string frozen=text;
            DiagnosticIO.Enqueue(()=>
            {
                if(retries.TryGetValue(path,out double next)&&DiagnosticClock.Now<next)return;
                try{
                if(!writers.TryGetValue(path,out var writer)){writer=new StreamWriter(path,true,new UTF8Encoding(false));writers[path]=writer;}
                if(writer.BaseStream.Length>Math.Max(1,StructuredLogger.MaxLogFileSizeMB)*1024L*1024L)
                {writer.Dispose();File.Move(path,path+".wsl-"+DateTime.UtcNow.Ticks);writer=new StreamWriter(path,false,new UTF8Encoding(false));writers[path]=writer;
                    var files=new DirectoryInfo(Path.GetDirectoryName(path)).GetFiles(Path.GetFileName(path)+".wsl-*").OrderByDescending(x=>x.LastWriteTimeUtc).ToArray();
                    long total=0;for(int i=0;i<files.Length;i++){total+=files[i].Length;if(i>=StructuredLogger.MaxRotatedFiles||total>StructuredLogger.MaxRotatedStorageMB*1024L*1024L)files[i].Delete();}}
                writer.WriteLine(frozen);retries.Remove(path);
                }catch{retries[path]=DiagnosticClock.Now+5;throw;}
            },text.Length*2+128,important);
        }
        internal static void Flush(){foreach(var writer in writers.Values)writer.Flush();}
        internal static void Close(){foreach(var writer in writers.Values)try{writer.Dispose();}catch{}writers.Clear();}
    }
}
