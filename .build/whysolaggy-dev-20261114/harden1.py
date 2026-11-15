# -*- coding: utf-8 -*-
from pathlib import Path
r=Path(__file__).resolve().parents[2]/'MOD开发/WhySoLaggy/WhySoLaggy'
def edit(name,fn):
 p=r/name;s=p.read_text(encoding='utf-8');p.write_text(fn(s),encoding='utf-8')
edit('Helpers/PatchProfiler.cs',lambda s:s.replace('internal static bool HasPendingCleanup=>Session.Count!=0;','internal static bool HasPendingCleanup=>Session.Faulted && Session.Count!=0;').replace('x!=OwnHarmonyId && !(x??"").StartsWith((OwnHarmonyId??WhySoLaggyPlugin.PluginGuid)+".",StringComparison.Ordinal)','!IsOwnOwner(x)').replace('        internal static List<HookSession.Target> Targets','        internal static bool IsOwnOwner(string owner)\n        { string root=OwnHarmonyId??WhySoLaggyPlugin.PluginGuid; return owner==root || new[]{"PatchProfiler","PluginProfiler","RpcMonitor","AbuseDetection","MethodTracer","FieldProbe"}.Any(x=>owner==root+"."+x); }\n        internal static List<HookSession.Target> Targets'))
edit('Helpers/TimingLedger.cs',lambda s:s.replace('internal long Calls, Samples, Ticks, BackgroundTicks, Exceptions;','internal long Calls, Samples, Ticks, BackgroundTicks, Exceptions, Entered;\n        internal readonly object TotalsLock = new object();').replace('private static readonly List<Tuple<long, long>> Intervals = new List<Tuple<long, long>>(512);','private struct Interval { internal long Start, End; internal Interval(long start,long end){Start=start;End=end;} }\n        private static readonly List<Interval> Intervals = new List<Interval>(512);\n        private static TimingEntry[] cached = new TimingEntry[0];\n        private static readonly Comparison<Interval> ByStart=(a,b)=>a.Start.CompareTo(b.Start);').replace('Entries.Add(id, entry); return entry;','Entries.Add(id, entry); cached=Entries.Values.ToArray(); return entry;').replace('long calls = Interlocked.Increment(ref entry.Calls);','long calls = Interlocked.Increment(ref entry.Entered);').replace('            if (main) entry.FrameCalls++;','').replace('            if (exception != null) Interlocked.Increment(ref entry.Exceptions);','''            lock(entry.TotalsLock)
            {
                entry.Calls++;
                if(exception!=null)entry.Exceptions++;
                if(state.Sampled){entry.Samples++;entry.Ticks+=Math.Max(0,end-state.Start);if(!state.MainThread)entry.BackgroundTicks+=Math.Max(0,end-state.Start);}
            }
            if(state.MainThread && state.Frame==frame){entry.FrameCalls++;if(exception!=null)entry.FrameExceptions++;}''').replace('                Interlocked.Increment(ref entry.Samples); Interlocked.Add(ref entry.Ticks, elapsed);','').replace('                    if (exception != null) entry.FrameExceptions++;','').replace('Intervals.Add(Tuple.Create(state.Start, end))','Intervals.Add(new Interval(state.Start, end))').replace('                    Interlocked.Add(ref entry.BackgroundTicks, elapsed);','').replace('            foreach (var e in All(scope))','            foreach (var e in All(scope))').replace('''                long calls = reset ? Interlocked.Exchange(ref e.Calls, 0) : Interlocked.Read(ref e.Calls);
                long samples = reset ? Interlocked.Exchange(ref e.Samples, 0) : Interlocked.Read(ref e.Samples);
                long ticks = reset ? Interlocked.Exchange(ref e.Ticks, 0) : Interlocked.Read(ref e.Ticks);
                long bg = reset ? Interlocked.Exchange(ref e.BackgroundTicks, 0) : Interlocked.Read(ref e.BackgroundTicks);
                long errors = reset ? Interlocked.Exchange(ref e.Exceptions, 0) : Interlocked.Read(ref e.Exceptions);''','''                long calls,samples,ticks,bg,errors;
                lock(e.TotalsLock){calls=e.Calls;samples=e.Samples;ticks=e.Ticks;bg=e.BackgroundTicks;errors=e.Exceptions;if(reset)e.Calls=e.Samples=e.Ticks=e.BackgroundTicks=e.Exceptions=0;}''').replace('foreach (var entry in All())','foreach (var entry in cached)').replace('CoveredMs = UnionMilliseconds(Intervals, start, end)','CoveredMs = FrameUnion(start, end)').replace('foreach (var e in All()) if','foreach (var e in cached) if').replace('''        internal static void Remove(string scope)
        {
            lock (RegistryLock) foreach (var pair in Entries.Where(x => x.Value.Scope == scope).ToArray())
            { pair.Value.Active = false; Interlocked.Increment(ref pair.Value.Generation); Entries.Remove(pair.Key); }
        }''','''        private static double FrameUnion(long start,long end)
        {
            Intervals.Sort(ByStart);long total=0,left=start,right=start;
            foreach(var i in Intervals){long a=Math.Max(start,i.Start),b=Math.Min(end,i.End);if(b<=a)continue;if(a>right){total+=right-left;left=a;right=b;}else right=Math.Max(right,b);}
            return DiagnosticClock.Milliseconds(total+right-left);
        }
        internal static void Remove(string scope,MethodBase target=null)
        {
            lock(RegistryLock){foreach(var pair in Entries.Where(x=>x.Value.Scope==scope&&(target==null||x.Value.Target==target)).ToArray())
            {pair.Value.Active=false;Interlocked.Increment(ref pair.Value.Generation);Entries.Remove(pair.Key);}cached=Entries.Values.ToArray();}
        }'''))
# Queue limits count and retained payload independently; drop oldest details only.
edit('Helpers/BoundedConcurrentQueue.cs',lambda s:s.replace('        private int _capacity;','''        private readonly Queue<int> _sizes = new Queue<int>();
        private readonly Func<T,int> _size;
        private readonly long _byteCapacity;
        private long _bytes;
        public long Bytes { get { lock(_lock)return _bytes; } }
        private int _capacity;''').replace('public BoundedConcurrentQueue(int capacity)','public BoundedConcurrentQueue(int capacity, Func<T,int> size = null, long byteCapacity = long.MaxValue)').replace('            Capacity = capacity;','            _size=size; _byteCapacity=byteCapacity; Capacity = capacity;').replace('                        _items.Dequeue();','                        _items.Dequeue(); _bytes-=_sizes.Dequeue();').replace('''                _items.Enqueue(item);
                bool dropped = false;
                if (_items.Count > _capacity)''','''                int size=Math.Max(0,_size?.Invoke(item)??0);
                if(size>_byteCapacity){_droppedInWindow++;return new QueueEnqueueResult(_items.Count,true,1);}
                _items.Enqueue(item);_sizes.Enqueue(size);_bytes+=size;
                bool dropped = false;int droppedCount=0;
                while (_items.Count > _capacity || _bytes>_byteCapacity)''').replace('                    _items.Dequeue();\n','                    _items.Dequeue(); _bytes-=_sizes.Dequeue();\n').replace('                    dropped = true;','                    dropped = true; droppedCount++;').replace('return new QueueEnqueueResult(_items.Count, dropped);','return new QueueEnqueueResult(_items.Count, dropped,droppedCount);').replace('                item = _items.Dequeue();','                item = _items.Dequeue();_bytes-=_sizes.Dequeue();').replace('                _items.Clear();','                _items.Clear();_sizes.Clear();_bytes=0;').replace('public QueueEnqueueResult(int depth, bool dropped)','public QueueEnqueueResult(int depth, bool dropped,int droppedCount=0)').replace('            Dropped = dropped;\n        }\n\n        public int Depth','            Dropped = dropped; DroppedCount=droppedCount;\n        }\n\n        public int DroppedCount { get; }\n        public int Depth'))
edit('Helpers/RpcMonitor.cs',lambda s:s.replace('new BoundedConcurrentQueue<RpcQueueItem>(2048);','new BoundedConcurrentQueue<RpcQueueItem>(2048,x=>256+2*((x.methodName?.Length??0)+(x.senderName?.Length??0))+RpcIngress.SnapshotBytes(x.args),4*1024*1024);').replace('Math.Min(QueueCapacity,1024)','QueueCapacity').replace('senderName = sender?.NickName','senderName = EventCodec.Limit(sender?.NickName,128)').replace('Interlocked.Increment(ref TotalDropped);','Interlocked.Add(ref TotalDropped,enqueueResult.DroppedCount);').replace('Interlocked.Increment(ref _metricsDropped);','Interlocked.Add(ref _metricsDropped,enqueueResult.DroppedCount);'))
edit('Helpers/RpcIngress.cs',lambda s:s.replace('            lock(gate)\n            {\n                var key=Tuple.Create(method,actor);','            method=EventCodec.Limit(method,256);\n            lock(gate)\n            {\n                var key=Tuple.Create(method,actor);').replace('        internal static void Reset()', '''        internal static int SnapshotBytes(object[] args)
        {int bytes=32;if(args!=null)foreach(var x in args)bytes+=x is string s?32+s.Length*2:x is byte[] b?32+b.Length:x is float[] f?32+f.Length*4:32;return bytes;}
        internal static void Reset()'''))
edit('Helpers/HookSession.cs',lambda s:s.replace('installed.Remove(method); Lookup.TryRemove(method,out _);','installed.Remove(method); Lookup.TryRemove(method,out _);TimingLedger.Remove(scope,method);').replace('installed.Remove(target.Method); Lookup.TryRemove(target.Method,out _);','installed.Remove(target.Method); Lookup.TryRemove(target.Method,out _);TimingLedger.Remove(scope,target.Method);'))
print('Timing, owner identity and payload queue corrected')
