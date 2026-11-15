# -*- coding: utf-8 -*-
from pathlib import Path
r=Path(__file__).resolve().parents[2]/'MOD开发/WhySoLaggy/WhySoLaggy'
def edit(name,fn):
 p=r/name;s=p.read_text(encoding='utf-8');p.write_text(fn(s),encoding='utf-8')
edit('Helpers/CaptureController.cs',lambda s:s.replace('40L*1024*1024','32L*1024*1024').replace('The remaining\n    // 24 MiB','The remaining\n    // 32 MiB').replace('},1024,true)', '},1024+patchMap.Length*2+plugins.Sum(x=>x.Length*2),true)'))
p=r/'Helpers/RpcMonitor.cs';s=p.read_text(encoding='utf-8')
s=s.replace('private static readonly HashSet<string> _extraWatchMethods', 'private static readonly HashSet<string> BuiltInWatched=new HashSet<string>(WatchedMethods,StringComparer.Ordinal);\n        private static readonly HashSet<string> _extraWatchMethods')
s=s.replace('public MethodBuffer(int cap) { Records = new WatchedRecord[cap]; }','public MethodBuffer(int cap) { Records = new WatchedRecord[Math.Max(1,Math.Min(cap,1024/Math.Max(1,WatchedMethods.Count)))]; }').replace('public void Reset() { Idx = 0; Count = 0; Total = 0; }','public void Reset() { Array.Clear(Records,0,Records.Length);Idx = 0; Count = 0; Total = 0; }')
s=s.replace('foreach (string previous in _extraWatchMethods) WatchedMethods.Remove(previous);','foreach (string previous in _extraWatchMethods) if(!BuiltInWatched.Contains(previous))WatchedMethods.Remove(previous);')
s=s.replace('if (!string.IsNullOrEmpty(name) && !AggregateOnlyMethods.Contains(name))','if (!string.IsNullOrEmpty(name) && name.Length<=256 && WatchedMethods.Count<128 && !AggregateOnlyMethods.Contains(name) && !BuiltInWatched.Contains(name))')
s=s.replace('                bool watched =','                methodName=EventCodec.Limit(methodName,256);\n                bool watched =')
s=s.replace('        public static void Tick()\n        {\n            if (!_inited) return;','''        private static readonly Dictionary<string,int> lastWindow=new Dictionary<string,int>();
        private static double windowStarted=DiagnosticClock.Now,lastWindowSeconds=1;
        internal static void Accumulate()
        {''')
s=s.replace('''                _windowByMethod.TryGetValue(item.Method''','''                if(_totalByMethod.Count>=256&&!_totalByMethod.ContainsKey(item.Method))item.Method="[overflow]";
                _windowByMethod.TryGetValue(item.Method''')
s=s.replace('                actors.TryGetValue(item.Actor,','                if(actors.Count>=64&&!actors.ContainsKey(item.Actor))item.Actor=-1;\n                actors.TryGetValue(item.Actor,')
s=s.replace('''            PumpQueue();
            _windowTimer += Time.unscaledDeltaTime;''','''        }
        public static void Tick()
        {
            if(!_inited)return;Accumulate();PumpQueue();
            _windowTimer = (float)(DiagnosticClock.Now-windowStarted);''')
s=s.replace('            _windowTimer = 0f;\n        }\n\n        private static void ProcessDequeued','            _windowTimer = 0f;windowStarted=DiagnosticClock.Now;\n        }\n\n        private static void ProcessDequeued')
s=s.replace('targetName = targetName,','targetName = EventCodec.Limit(targetName??"unavailable",128),').replace('targetPath = targetPath,','targetPath = EventCodec.Limit(targetPath,256),').replace('argsSummary = argSum,','argsSummary = EventCodec.Limit(argSum,256),').replace('specificDesc = specific,','specificDesc = EventCodec.Limit(specific,256),')
s=s.replace('''        public static void OnWindowEnd()
        {
            QueueWindowStats''','''        public static void OnWindowEnd()
        {
            lastWindowSeconds=Math.Max(.001,DiagnosticClock.Now-windowStarted);lastWindow.Clear();
            foreach(var p in _windowByMethod)lastWindow[p.Key]=p.Value;
            foreach(var p in lastWindow.OrderByDescending(x=>x.Value).Take(TopMethodCount))StructuredLogger.WriteEvent(new StructuredEvent{Type=EventType.NetworkSample,FrameNumber=DiagnosticContext.Frame,Fields=new Dictionary<string,object>{{"RpcMethod",p.Key},{"Count",p.Value},{"ActualWindowSeconds",lastWindowSeconds},{"Rate",p.Value/lastWindowSeconds}}});
            QueueWindowStats''')
s=s.replace('{return string.Join("  ",_totalByMethod.OrderByDescending(x=>x.Value).Take(maxLines).Select(x=>x.Key+"="+x.Value));}','{return UiText.Get("Last window","最近窗口")+" ("+lastWindowSeconds.ToString("F2")+"s): "+string.Join("  ",lastWindow.OrderByDescending(x=>x.Value).Take(maxLines).Select(x=>x.Key+"="+x.Value+" ("+(x.Value/lastWindowSeconds).ToString("F1")+"/s)"));}')
s=s.replace('{RpcIngress.Reset();_queue.Clear();_totalByMethod.Clear();','{Accumulate();WritePeriodicReport();RpcIngress.Reset();_queue.Clear();lastWindow.Clear();windowStarted=DiagnosticClock.Now;_totalByMethod.Clear();')
s=s.replace('FrameNumber = Time.frameCount,','FrameNumber = DiagnosticContext.Frame,')
p.write_text(s,encoding='utf-8')
edit('WhySoLaggyPlugin.cs',lambda s:s.replace('            ApplyPendingPatchProfiling();','            ApplyRuntimeHooks();'))
edit('Helpers/NetworkAbuseDetector.cs',lambda s:s.replace('internal static void ResetEpoch(){ResetCounters();','internal static void ResetEpoch(){if(_initialized){_actualCheckSeconds=Math.Max(.001,DiagnosticClock.Now-_previousCheck);CheckRates();}ResetCounters();').replace('FrameNumber = Time.frameCount,','FrameNumber = DiagnosticContext.Frame,'))
edit('Helpers/HarmonyScanner.cs',lambda s:s.replace('internal static bool IsDiagnostic(string owner)=>owner==WhySoLaggyPlugin.PluginGuid+".PatchProfiler"||owner==WhySoLaggyPlugin.PluginGuid+".PluginProfiler";','internal static bool IsDiagnostic(string owner)=>PatchProfiler.IsOwnOwner(owner);').replace('            {\n                var info=Harmony.GetPatchInfo(method);','            {\n                if(sb.Length>512*1024){failures++;break;}\n                var info=Harmony.GetPatchInfo(method);').replace('},csv.Length*2+256,true);','},csv.Length*2+added.Sum(x=>x.Length*2)+removed.Sum(x=>x.Length*2)+256,true);'))
edit('Helpers/HookSession.cs',lambda s:s.replace('        internal double LastOperationMs;','        internal double LastOperationMs;\n        internal long Revision;').replace('                operations++;','                Revision++;operations++;'))
edit('WhySoLaggyPlugin.Runtime.cs',lambda s:s.replace('private string hooksSignature="";','private string hooksSignature="";\n        private long inventoryRevision;private double nextInventory;').replace('            FrameCoordinator.Check();PluginProfiler.Pump();PatchProfiler.Pump();','''            FrameCoordinator.Check();PluginProfiler.Pump();PatchProfiler.Pump();
            long revision=PluginProfiler.Session.Revision+PatchProfiler.Session.Revision;
            if(runtimeStarted&&!FrameCoordinator.Transition&&revision!=inventoryRevision&&DiagnosticClock.Now>=nextInventory){inventoryRevision=revision;nextInventory=DiagnosticClock.Now+5;RefreshInventory();}'''))
print('RPC periods, retained payload limits and startup routing corrected')
