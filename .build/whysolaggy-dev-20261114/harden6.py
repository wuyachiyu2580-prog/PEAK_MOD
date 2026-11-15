# -*- coding: utf-8 -*-
from pathlib import Path
r=Path(__file__).resolve().parents[2]/'MOD开发/WhySoLaggy/WhySoLaggy'
def edit(name,fn):
 p=r/name;s=p.read_text(encoding='utf-8');p.write_text(fn(s),encoding='utf-8')
edit('WhySoLaggyPlugin.cs',lambda s:s.replace('''        void Update()
        {
            RuntimeTick();''','''        void Update()
        {
            long start=System.Diagnostics.Stopwatch.GetTimestamp();
            try{UpdateCore();}finally{SelfMetrics.Add("MainUpdate",DiagnosticClock.Milliseconds(System.Diagnostics.Stopwatch.GetTimestamp()-start));}
        }
        private void UpdateCore()
        {
            RuntimeTick();''').replace('            InitializeRuntime();','            InitializeRuntime();\n            ModConfigLocalization.RefreshDescriptions(GetConfigEntries());') if False else s)
# Add wrapper without depending on local description helper names.
edit('WhySoLaggyPlugin.cs',lambda s:s.replace('''        void Update()
        {
            RuntimeTick();''','''        void Update()
        {
            long start=System.Diagnostics.Stopwatch.GetTimestamp();
            try{UpdateCore();}finally{SelfMetrics.Add("MainUpdate",DiagnosticClock.Milliseconds(System.Diagnostics.Stopwatch.GetTimestamp()-start));}
        }
        private void UpdateCore()
        {
            RuntimeTick();'''))
edit('Helpers/HookSession.cs',lambda s:s.replace('if(countFrame && DiagnosticContext.IsMainThread && name!="HookInstall")','if(countFrame && DiagnosticContext.IsMainThread && (name=="MainUpdate"||name=="FrameBoundary"||name=="Hud"))').replace('        private readonly Queue<MethodBase> removing','        private double nextCleanup;\n        private readonly Queue<MethodBase> removing').replace('            long begin=Stopwatch.GetTimestamp(); int operations=0;','''            if(!Busy&&Faulted&&DiagnosticClock.Now>=nextCleanup){nextCleanup=DiagnosticClock.Now+5;foreach(var p in installed)if(!p.Value.Active)removing.Enqueue(p.Key);}
            long begin=Stopwatch.GetTimestamp(); int operations=0;''').replace('                        if(entry!=null)entry.Active=false;','                        if(Automatic)Faulted=true;\n                        if(entry!=null)entry.Active=false;'))
edit('Helpers/DiagnosticCore.cs',lambda s:s.replace('finally { started = now; TimingLedger.Begin(DiagnosticContext.Frame); }','finally { started = now; TimingLedger.Begin(DiagnosticContext.Frame);SelfMetrics.Add("FrameBoundary",DiagnosticClock.Milliseconds(Stopwatch.GetTimestamp()-now)); }').replace('            if (!recorderValid) return;','            if (!recorderValid || !FpsTracker.EnableMemoryMonitor) return;'))
edit('Helpers/TimingLedger.cs',lambda s:s.replace('internal double Threshold;','internal double Threshold,NextThreadCleanup;').replace('                        foreach (var key in entry.Threads.Where','                        if(DiagnosticClock.Now>=entry.NextThreadCleanup){entry.NextThreadCleanup=DiagnosticClock.Now+5;foreach (var key in entry.Threads.Where').replace('entry.Threads.Remove(key);\n                        if','entry.Threads.Remove(key);}\n                        if'))
edit('Helpers/FieldProbe.cs',lambda s:s.replace('FrameNumber = UnityEngine.Time.frameCount,','FrameNumber = DiagnosticContext.IsMainThread?DiagnosticContext.Frame:-1,').replace('            if (_inited) return;','            if (_inited) return;\n            if(Enabled)Validate(RulesFilePath);'))
edit('Helpers/ExpressionEvaluator.cs',lambda s:s.replace('            return uo == null;', '            if(!DiagnosticContext.IsMainThread)return true;\n            return uo == null;'))
edit('Helpers/ValueFormatter.cs',lambda s:s.replace('en is Array||en is ArrayList||','en is Array||en.GetType()==typeof(ArrayList)||'))
edit('Helpers/StructuredLogger.cs',lambda s:s.replace('private static double nextFlush;','private static double nextFlush,nextDiskRetry;\n        private static int diskFailures;\n        internal static long DiskDropped;').replace('private static void FlushBuffersLocked()\n        {','''private static void FlushBuffersLocked()
        {
            if(DiagnosticClock.Now<nextDiskRetry){BoundFailedBuffer();return;}''').replace('                _jsonlWriter?.Flush();','                _jsonlWriter?.Flush();diskFailures=0;nextDiskRetry=0;').replace('                DiagnosticIO.Error = "Flush failed: " + ex.Message;','''                diskFailures++;nextDiskRetry=DiagnosticClock.Now+Math.Min(30,Math.Pow(2,Math.Min(diskFailures,5)));
                DiagnosticIO.Error = "Flush failed: " + ex.GetType().Name;BoundFailedBuffer();''').replace('        private static void AppendCsvRow(','''        private static void BoundFailedBuffer()
        {if(_csvBuffer.Length+_jsonBuffer.Length>262144){_csvBuffer.Clear();_jsonBuffer.Clear();DiskDropped++;}}
        private static void AppendCsvRow(''').replace('            DiagnosticIO.Start();\n            lock (_lock)','''            DiagnosticIO.Start();
            if(DiagnosticContext.IsMainThread&&!DiagnosticIO.IsWorker){DiagnosticIO.Enqueue(()=>Initialize(dir),256,true);return;}
            lock (_lock)'''))
edit('Helpers/DiagnosticIO.cs',lambda s:s.replace('internal Action Action;internal int Bytes;internal bool Important;','internal Action Action;internal int Bytes;internal bool Important;').replace('        private static long bytes,dropped;','        private static long bytes,dropped;\n        private static double retryAfter;').replace('                try{job.Action();}catch(Exception ex){Error=ex.GetType().Name+": "+ex.Message;}','''                try{job.Action();}catch(Exception ex){Error=ex.GetType().Name+": "+ex.Message;}
                if(stopping&&queue.Count==0)signal.Set();'''))
# Remove placeholder unused field; disk retry is sink-specific to avoid replaying partial ZIP actions.
edit('Helpers/DiagnosticIO.cs',lambda s:s.replace('        private static double retryAfter;\n',''))
edit('Helpers/NetworkAbuseDetector.cs',lambda s:s.replace('            if (!string.IsNullOrEmpty(methodName))\n            {','            if (!string.IsNullOrEmpty(methodName))\n            {\n                methodName=EventCodec.Limit(methodName,256);').replace('''                    _recentClientRpcs.AddLast''','''                    if(_rpcByActorMethodTotal.Count>=64&&!_rpcByActorMethodTotal.ContainsKey(sender))sender=-1;
                    _recentClientRpcs.AddLast''').replace('                    fastInner.TryGetValue(methodName,','                    if(fastInner.Count>=256&&!fastInner.ContainsKey(methodName))methodName="[overflow]";\n                    fastInner.TryGetValue(methodName,').replace('                    slowInner.TryGetValue(methodName,','                    if(slowInner.Count>=256&&!slowInner.ContainsKey(methodName))methodName="[overflow]";\n                    slowInner.TryGetValue(methodName,'))
print('Measured self costs, disk retry buffer and background probe safety hardened')
