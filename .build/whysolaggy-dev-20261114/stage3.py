from pathlib import Path
root=Path(__file__).resolve().parents[2]/'MOD开发/WhySoLaggy/WhySoLaggy'
def edit(rel,fn):
 p=root/rel;p.write_text(fn(p.read_text(encoding='utf-8-sig')),encoding='utf-8')
def r(s,a,b):
 assert a in s,a[:100]
 return s.replace(a,b)
def rpc(s):
 if 'using System.Linq;' not in s:s='using System.Linq;\n'+s
 s=r(s,'public object[] args;    // 引用；Photon 回调完毕即同帧消费，生命周期安全','public object[] args; // Owned bounded snapshot; never the Photon payload.\n            public int epoch;')
 s=r(s,'public float enqueueRealtime;','public double enqueueRealtime;')
 s=r(s,'private static long _nextQueueSequence;','internal static double PumpBudgetMs=.5;\n        internal static int LastDepth=>_queue.Count;\n        internal static long TotalDropped;\n        internal static double LastQueueDelayMs;\n        private static long _nextQueueSequence;')
 s=r(s,'object[] args = watched ? rpcData[K_ARGS] as object[] : null;','bool retain=RpcIngress.Record(methodName,sender?.ActorNumber??-1,bytes,DiagnosticClock.Now);\n                if(!watched||!retain)return;\n                object[] args = RpcIngress.CopyArgs(rpcData[K_ARGS] as object[]);')
 s=r(s,'enqueueFrame = Time.frameCount,','enqueueFrame = DiagnosticContext.Frame,\n                    epoch = DiagnosticContext.RoomEpoch,')
 s=r(s,'enqueueRealtime = Time.realtimeSinceStartup,','enqueueRealtime = DiagnosticClock.Now,')
 # Each record is bounded under 4 KiB including cloned payload; retain original configured count as upper limit.
 s=r(s,'_queue.Capacity = QueueCapacity;','_queue.Capacity = Math.Min(QueueCapacity,1024);')
 s=r(s,'if (enqueueResult.Dropped) Interlocked.Increment(ref _metricsDropped);','if (enqueueResult.Dropped) {Interlocked.Increment(ref _metricsDropped);Interlocked.Increment(ref TotalDropped);}')
 s=r(s,'while (budget-- > 0 && _queue.TryDequeue(out var item))','while (budget-- > 0 && DiagnosticClock.Milliseconds(Stopwatch.GetTimestamp()-started)<PumpBudgetMs && _queue.TryDequeue(out var item))')
 s=r(s,'lock (_metricsLock) _metricsPumpMs += elapsedMs;','lock (_metricsLock) _metricsPumpMs += elapsedMs;\n            SelfMetrics.Add("RpcPump",elapsedMs);')
 a=s.index('            // 累加统计',s.index('private static void ProcessDequeued'));b=s.index('            if (item.watched',a)
 s=s[:a]+'''            if(item.epoch!=DiagnosticContext.RoomEpoch)return;
            LastQueueDelayMs=Math.Max(0,(DiagnosticClock.Now-item.enqueueRealtime)*1000);
'''+s[b:]
 s=r(s,'(Time.realtimeSinceStartup - item.enqueueRealtime)','(DiagnosticClock.Now - item.enqueueRealtime)')
 s=r(s,'            PumpQueue();','''            foreach(var item in RpcIngress.Drain())
            {
                _windowByMethod.TryGetValue(item.Method,out int wc);_windowByMethod[item.Method]=wc+item.Count;
                _totalByMethod.TryGetValue(item.Method,out int tc);_totalByMethod[item.Method]=tc+item.Count;
                if(!_totalByMethodActor.TryGetValue(item.Method,out var actors))_totalByMethodActor[item.Method]=actors=new Dictionary<int,int>();
                actors.TryGetValue(item.Actor,out int ac);actors[item.Actor]=ac+item.Count;
                _totalBytesByMethod.TryGetValue(item.Method,out long bytes);_totalBytesByMethod[item.Method]=bytes+item.Bytes;
                _maxBytesByMethod.TryGetValue(item.Method,out int max);_maxBytesByMethod[item.Method]=Math.Max(max,item.MaxBytes);
            }
            PumpQueue();''')
 a=s.index('        public static string GetRecentWatchedSummary(')
 s=s[:a]+'''        public static string GetRecentWatchedSummary(int maxLines)
        {return string.Join("  ",_totalByMethod.OrderByDescending(x=>x.Value).Take(maxLines).Select(x=>x.Key+"="+x.Value));}
        internal static void ResetEpoch()
        {RpcIngress.Reset();_queue.Clear();_totalByMethod.Clear();_windowByMethod.Clear();_totalByMethodActor.Clear();_totalBytesByMethod.Clear();_maxBytesByMethod.Clear();foreach(var b in _watchedByMethod.Values)b.Reset();}
    }
}
'''
 s=r(s,'            _queue.Clear();\n            _totalByMethod.Clear();','            RpcIngress.Reset();\n            _queue.Clear();\n            _totalByMethod.Clear();')
 return s
edit('Helpers/RpcMonitor.cs',rpc)
def abuse(s):
 s=r(s,'private static void CheckRates()','private static double _actualCheckSeconds=1, _previousCheck;\n        internal static double Rate(int count,double elapsed)=>count/Math.Max(.001,elapsed);\n        internal static void ResetEpoch(){ResetCounters();ResetReportStats();_lastPhotonViewCount=CountPhotonViews();_lastZombieCount=CountZombies();_previousCheck=DiagnosticClock.Now;_checkTimer=_reportTimer=0;_alertCooldown.Clear();}\n        private static void CheckRates()')
 s=r(s,'                CheckRates();','                double now=DiagnosticClock.Now;_actualCheckSeconds=_previousCheck>0?now-_previousCheck:_checkTimer;_previousCheck=now;\n                CheckRates();')
 s=r(s,'float interval = CheckIntervalSeconds;','float interval = (float)Math.Max(.001,_actualCheckSeconds);')
 s=r(s,'{ "Rate", Math.Round((double)rate, 2) },','{ "Rate", Math.Round((double)rate, 2) },\n                    { "ActualWindowSeconds", _actualCheckSeconds },\n                    { "DelayedDispatch", _actualCheckSeconds>CheckIntervalSeconds*1.5 },')
 return s
edit('Helpers/NetworkAbuseDetector.cs',abuse)
print('RPC aggregate/detail split and actual-rate intervals implemented')
