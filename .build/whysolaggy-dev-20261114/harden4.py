# -*- coding: utf-8 -*-
from pathlib import Path
r=Path(__file__).resolve().parents[2]/'MOD开发/WhySoLaggy/WhySoLaggy'
def edit(name,fn):
 p=r/name;s=p.read_text(encoding='utf-8');p.write_text(fn(s),encoding='utf-8')
p=r/'Helpers/FieldProbe.cs';s=p.read_text(encoding='utf-8');s=s.replace('using System.IO;','using System.IO;\nusing System.Linq;')
s=s.replace('        private static bool _inited;','''        private static bool _inited;
        private static bool strict;
        private static volatile bool reloading;
        private static Harmony activeHarmony;
        internal static void Validate(string path)
        {
            if(!Enabled)return;
            if(new FileInfo(path).Length>1024*1024)throw new InvalidDataException("Rule file exceeds 1 MiB");
            if(!MiniJson.TryParse(File.ReadAllText(path),out var value,out var error)||!(value is Dictionary<string,object> root))throw new InvalidDataException(error??"Invalid rule object");
            if(!MiniJson.GetBool(root,"enabled",true))return;
            var rules=MiniJson.AsArray(root.TryGetValue("rules",out var list)?list:null);
            if(rules==null||rules.Count>128)throw new InvalidDataException("Expected at most 128 rules");
            foreach(var item in rules)
            {
                var rule=MiniJson.AsObject(item);if(rule==null)throw new InvalidDataException("Invalid rule");if(!MiniJson.GetBool(rule,"enabled",true))continue;
                string target=MiniJson.GetString(rule,"target");RuleTargets.Resolve(target);
                var fields=MiniJson.AsArray(rule.TryGetValue("fields",out var f)?f:null);
                if(fields==null||fields.Count==0||fields.Count>32)throw new InvalidDataException("Expected 1-32 field expressions");
                foreach(var field in fields)if(!(field is string text)||!string.IsNullOrEmpty(ExpressionEvaluator.Compile(text).CompileError))throw new InvalidDataException("Invalid field expression");
            }
        }
        internal static bool TryReload(Harmony initial,out string error)
        {
            error=null;try{Validate(RulesFilePath);}catch(Exception ex){error=ex.Message;return false;}
            var old=new Dictionary<MethodBase,List<Rule>>(_methodToRules);bool oldInited=_inited;int oldRules=_hookedRules,oldMethods=_hookedMethods;
            var previous=activeHarmony??initial;
            var stage=new Harmony(initial.Id+(previous.Id==initial.Id?".Staging":""));
            reloading=true;strict=true;
            try
            {
                _methodToRules.Clear();_inited=false;_hookedRules=_hookedMethods=0;
                Initialize(stage);previous.UnpatchSelf();activeHarmony=stage;return true;
            }
            catch(Exception ex)
            {
                try{stage.UnpatchSelf();}catch{} _methodToRules.Clear();foreach(var pair in old)_methodToRules[pair.Key]=pair.Value;
                _inited=oldInited;_hookedRules=oldRules;_hookedMethods=oldMethods;activeHarmony=previous;error=ex.Message;return false;
            }
            finally{strict=false;reloading=false;}
        }''')
s=s.replace('            if (_inited) return;\n            _inited = true;','            if (_inited) return;\n            if(!strict)activeHarmony=harmony;\n            _inited = true;')
s=s.replace('                int dot = target.IndexOf(\'.\');','                int dot = target.LastIndexOf(\'.\');').replace('if (typeName != rule.TargetTypeName) continue;','if (typeName != rule.TargetTypeName && t.FullName != rule.TargetTypeName) continue;')
s=s.replace('''                    WhySoLaggyPlugin.Log?.LogWarning($"[WHY_LAG] FieldProbe Patch failed''','''                    if(strict)throw;
                    WhySoLaggyPlugin.Log?.LogWarning($"[WHY_LAG] FieldProbe Patch failed''')
# All four callbacks are neutral while staging; no values or exceptions are changed.
s=s.replace('            Dispatch(__originalMethod,','            if(!reloading)Dispatch(__originalMethod,')
s=s.replace('        public static void Shutdown()\n        {','        public static void Shutdown()\n        {\n            try{activeHarmony?.UnpatchSelf();}catch{}activeHarmony=null;')
p.write_text(s,encoding='utf-8')
p=r/'Helpers/MethodTracer.cs';s=p.read_text(encoding='utf-8');s=s.replace('using System.Text;','using System.Text;\nusing System.Linq;');s=s.replace('        private static int _hooked;','''        private static int _hooked;
        private static Harmony activeHarmony;
        private static volatile bool reloading;
        internal static void Validate(string names){foreach(var name in (names??"").Split(',').Select(x=>x.Trim()).Where(x=>x.Length>0))RuleTargets.Resolve(name);}
        internal static bool TryReload(Harmony initial,string names,out string error)
        {
            error=null;MethodBase[] targets;
            try{targets=(names??"").Split(',').Select(x=>x.Trim()).Where(x=>x.Length>0).SelectMany(RuleTargets.Resolve).Distinct().ToArray();if(targets.Length>128)throw new InvalidOperationException("Maximum 128 trace methods");}catch(Exception ex){error=ex.Message;return false;}
            var previous=activeHarmony??initial;var stage=new Harmony(initial.Id+(previous.Id==initial.Id?".Staging":""));reloading=true;
            try{foreach(var method in targets)stage.Patch(method,prefix:new HarmonyMethod(typeof(MethodTracer),nameof(OnPrefix)){priority=Priority.First});previous.UnpatchSelf();activeHarmony=stage;TraceMethodNames=names;_inited=true;_hooked=targets.Length;return true;}
            catch(Exception ex){try{stage.UnpatchSelf();}catch{}error=ex.Message;return false;}
            finally{reloading=false;}
        }''')
s=s.replace('            if (_inited) return;','            if (_inited) return;\n            activeHarmony=harmony;').replace('            if (__originalMethod == null) return;','            if (reloading || __originalMethod == null) return;').replace('FrameNumber = UnityEngine.Time.frameCount,','FrameNumber = DiagnosticContext.IsMainThread?DiagnosticContext.Frame:-1,').replace('        public static void Shutdown()\n        {','        public static void Shutdown()\n        {\n            try{activeHarmony?.UnpatchSelf();}catch{}activeHarmony=null;')
p.write_text(s,encoding='utf-8')
edit('Helpers/PatchProfiler.cs',lambda s:s.replace('"MethodTracer","FieldProbe"','"MethodTracer","FieldProbe","MethodTracer.Staging","FieldProbe.Staging"'))
p=r/'WhySoLaggyPlugin.Runtime.cs';s=p.read_text(encoding='utf-8');a=s.index('                if(EnableFieldProbe.Value&&');b=s.index('\n            }catch(Exception ex){RuntimeStatus="Rule reload failed:',a)
s=s[:a]+'''                bool oldEnabled=FieldProbe.Enabled;string oldPath=FieldProbe.RulesFilePath;
                FieldProbe.Enabled=EnableFieldProbe.Value;
                try{MethodTracer.Validate(TraceMethodNames.Value);FieldProbe.Validate(path);}
                catch{FieldProbe.Enabled=oldEnabled;throw;}
                FieldProbe.RulesFilePath=path;
                if(!FieldProbe.TryReload(probeHarmony,out string probeError)){FieldProbe.Enabled=oldEnabled;FieldProbe.RulesFilePath=oldPath;RuntimeStatus="Rule reload failed: "+probeError;return;}
                if(!MethodTracer.TryReload(traceHarmony,TraceMethodNames.Value,out string traceError)){RuntimeStatus="Rule reload failed: "+traceError;return;}
                MethodTracer.TraceMaxDepth=TraceMaxDepth.Value;MethodTracer.TraceRateLimit=TraceRateLimit.Value;
                FieldProbe.DefaultRateLimit=FieldProbeDefaultRate.Value;FieldProbe.DefaultMaxValueLen=FieldProbeDefaultMaxLen.Value;FieldProbe.DefaultIncludeStack=FieldProbeDefaultStack.Value;FieldProbe.DefaultStackMaxDepth=FieldProbeDefaultStackDepth.Value;
                RuntimeStatus="Rules reloaded";FrameCoordinator.Transition=true;RefreshInventory();'''+s[b:];p.write_text(s,encoding='utf-8')
edit('Helpers/ValueFormatter.cs',lambda s:s.replace('                if (uo == null)', '                if(!DiagnosticContext.IsMainThread)return "[Unity object: main thread only]";\n                if (uo == null)').replace('if(!(en is IList list))','if(!(en is IList list)||!(en is Array||en is ArrayList||en.GetType().IsGenericType&&en.GetType().GetGenericTypeDefinition()==typeof(System.Collections.Generic.List<>)))'))
edit('Helpers/DiagnosticIO.cs',lambda s:s.replace('stopping=false;worker=new Thread','stopping=false;Error="";worker=new Thread').replace('if(stopping||size>4*1024*1024)','if(stopping||size<0||size>4*1024*1024)').replace('var done=new ManualResetEventSlim();','var done=new System.Threading.Tasks.TaskCompletionSource<bool>();').replace('done.Set();','done.TrySetResult(true);').replace('bool finished=done.Wait(timeout);if(finished)done.Dispose();return finished;','return done.Task.Wait(timeout);'))
edit('Helpers/StructuredLogger.cs',lambda s:s.replace('DiagnosticIO.Barrier(()=>{lock(_lock){FlushBuffersLocked();CloseWriters();_csvBuffer.Clear();_jsonBuffer.Clear();_inited=false;_eventsSinceMetrics=0;}TextLogSink.Close();});','DiagnosticIO.Enqueue(()=>{lock(_lock){FlushBuffersLocked();CloseWriters();_csvBuffer.Clear();_jsonBuffer.Clear();_inited=false;_eventsSinceMetrics=0;}TextLogSink.Close();},256,true);'))
print('Rule reloads preserve previous hooks on failure; shutdown and probes bounded')
