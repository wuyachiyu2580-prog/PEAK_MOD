using System;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Threading;

namespace WhySoLaggy
{
    // Main-thread-only diagnostic window. No per-sample objects, sorting or logging.
    // P95 is an upper bound from fixed logarithmic buckets; stages may be nested.
    internal sealed class BoundedPhaseProfiler
    {
        internal struct Context
        {
            internal int Objects, Labels, Selection, Scene, SelectedItems, LocationScopes, MapScope;
            public override string ToString()=>"objects="+Objects+" labels="+Labels+" selection="+Selection+" scene="+Scene+
                " selectedItems="+SelectedItems+" locationScopes="+LocationScopes+" mapScope="+MapScope;
        }
        internal struct Scope : IDisposable
        {
            private readonly BoundedPhaseProfiler owner;
            private readonly long started;
            private readonly int stage, generation;
            internal Scope(BoundedPhaseProfiler owner,int stage)
            {this.owner=owner;this.stage=stage;generation=owner.generation;started=Stopwatch.GetTimestamp();}
            public void Dispose()
            {
                if(owner!=null&&owner.Active&&owner.generation==generation)
                    owner.Observe(stage,(Stopwatch.GetTimestamp()-started)*1000d/Stopwatch.Frequency);
            }
        }
        private const int Buckets=64;
        private readonly string[] names;
        private readonly long[] counts,histogram;
        private readonly double[] total,maximum;
        private readonly Context[] peak;
        private readonly int[] gc;
        private int thread,generation;
        private double started,deadline;
        private bool armed;
        internal bool Active {get;private set;}
        internal Context Current;
        internal string LastReport {get;private set;}="";
        internal BoundedPhaseProfiler(params string[] names)
        {
            this.names=names;counts=new long[names.Length];histogram=new long[names.Length*Buckets];
            total=new double[names.Length];maximum=new double[names.Length];peak=new Context[names.Length];gc=new int[GC.MaxGeneration+1];
        }
        internal Scope Measure(int stage)=>Active&&Thread.CurrentThread.ManagedThreadId==thread?new Scope(this,stage):default;
        internal bool Tick(bool requested,double seconds,double now,Action<string> output)
        {
            if(!requested){armed=false;if(Active)Stop(now,output,"disabled");return false;}
            if(!armed)
            {
                armed=true;Active=true;thread=Thread.CurrentThread.ManagedThreadId;generation++;
                started=now;deadline=now+Math.Max(30,Math.Min(60,double.IsNaN(seconds)?45:seconds));
                Array.Clear(counts,0,counts.Length);Array.Clear(histogram,0,histogram.Length);
                Array.Clear(total,0,total.Length);Array.Clear(maximum,0,maximum.Length);Array.Clear(peak,0,peak.Length);
                for(int i=0;i<gc.Length;i++)gc[i]=GC.CollectionCount(i);
            }
            if(now<deadline)return false;
            Stop(now,output,"timeout");return true;
        }
        internal void Observe(int stage,double milliseconds)
        {
            if(!Active||Thread.CurrentThread.ManagedThreadId!=thread||stage<0||stage>=names.Length||double.IsNaN(milliseconds)||double.IsInfinity(milliseconds)||milliseconds<0)return;
            counts[stage]++;total[stage]+=milliseconds;
            if(milliseconds>=maximum[stage]){maximum[stage]=milliseconds;peak[stage]=Current;}
            int bucket=milliseconds<=.001?0:Math.Min(Buckets-1,(int)Math.Ceiling(Math.Log(milliseconds/.001,2)*3));
            histogram[stage*Buckets+bucket]++;
        }
        internal double P95Upper(int stage)
        {
            long threshold=(long)Math.Ceiling(counts[stage]*.95),sum=0;
            if(threshold==0)return 0;
            for(int i=0;i<Buckets;i++)
            {sum+=histogram[stage*Buckets+i];if(sum>=threshold)return i==Buckets-1?maximum[stage]:Math.Min(maximum[stage],.001*Math.Pow(2,i/3d));}
            return maximum[stage];
        }
        internal void Stop(double now,Action<string> output,string reason)
        {
            if(!Active)return;Active=false;
            var report=new StringBuilder("PhaseTiming schema=1 reason=").Append(reason).Append(" durationSeconds=").Append((now-started).ToString("F3",CultureInfo.InvariantCulture));
            report.Append(" nested=true; do not sum stages; P95UpperMs is a histogram bound; GC deltas are counts, not pause durations.");
            for(int i=0;i<gc.Length;i++)report.Append(" gc").Append(i).Append('=').Append(GC.CollectionCount(i)-gc[i]);
            for(int i=0;i<names.Length;i++)
            {
                if(counts[i]==0)continue;
                report.AppendLine().Append(names[i]).Append(" Count=").Append(counts[i]).Append(" TotalMs=").Append(total[i].ToString("F6",CultureInfo.InvariantCulture))
                    .Append(" MaxMs=").Append(maximum[i].ToString("F6",CultureInfo.InvariantCulture)).Append(" P95UpperMs=").Append(P95Upper(i).ToString("F6",CultureInfo.InvariantCulture))
                    .Append(" peakContext{").Append(peak[i]).Append('}');
            }
            LastReport=report.ToString();output?.Invoke(LastReport);
        }
    }
}
