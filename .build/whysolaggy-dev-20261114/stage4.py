from pathlib import Path
root=Path(__file__).resolve().parents[2]/'MOD开发/WhySoLaggy/WhySoLaggy'
def edit(rel,fn):
 p=root/rel;p.write_text(fn(p.read_text(encoding='utf-8-sig')),encoding='utf-8')
def r(s,a,b):
 assert a in s,a[:80]
 return s.replace(a,b)
def main(s):
 s=r(s,'public class WhySoLaggyPlugin','public partial class WhySoLaggyPlugin')
 s=r(s,'            EnablePatchProfiling.SettingChanged += OnPatchProfilingChanged;','            InitializeRuntime();')
 s=r(s,'new WaitForSeconds(5f)','new WaitForSecondsRealtime(5f)')
 s=r(s,'PluginProfiler.Initialize(_harmony);','PluginProfiler.Request(true,false);')
 s=r(s,'NetworkAbuseDetector.Initialize(_harmony);','NetworkAbuseDetector.Initialize(abuseHarmony);')
 s=r(s,'RpcMonitor.Initialize(_harmony);','RpcMonitor.Initialize(rpcHarmony);')
 s=r(s,'MethodTracer.Initialize(_harmony);','MethodTracer.Initialize(traceHarmony);')
 s=r(s,'FieldProbe.Initialize(_harmony);','FieldProbe.Initialize(probeHarmony);')
 s=r(s,'            _profilingActive = true;','            runtimeStarted=true;System.Threading.Interlocked.Exchange(ref runtimeDirty,1);\n            _profilingActive = true;')
 s=r(s,'            ApplyPendingPatchProfiling();\n            StructuredLogger.Tick();','            RuntimeTick();\n            StructuredLogger.Tick();')
 a=s.index('            // 尖峰检测 → 即时日志');b=s.index('            // 定期汇总报告',a);s=s[:a]+s[b:]
 s=r(s,'            PluginProfiler.ResetFrameTimers();\n            PatchProfiler.ResetFrameTimers();','            // Completed frames are sealed by FrameCoordinator.')
 s=r(s,'        void OnDestroy()\n        {','        void OnDestroy()\n        {\n            ShutdownRuntime();')
 a=s.index('        private void ApplyPendingPatchProfiling()');b=s.index('        private void OnGameLanguageChanged()',a)
 s=s[:a]+'        private void ApplyPendingPatchProfiling(){ApplyRuntimeHooks();}\n\n'+s[b:]
 return s
edit('WhySoLaggyPlugin.cs',main)
edit('Helpers/PatchProfiler.cs',lambda s:r(r(s,'bool automatic=false)','bool automatic=false,MethodBase[] extra=null)'),'{Enabled=enabled;Session.Request(OwnHarmonyId??WhySoLaggyPlugin.PluginGuid,enabled?Targets(owners,automatic?50:int.MaxValue):new List<HookSession.Target>(),automatic);}', '''{Enabled=enabled;var targets=enabled?Targets(owners,automatic?50:int.MaxValue):new List<HookSession.Target>();
            if(enabled&&extra!=null)foreach(var method in extra)if(targets.Count<50&&!targets.Any(x=>x.Method==method))targets.Add(new HookSession.Target{Method=method,Owners="unattributed exception target"});
            Session.Request(OwnHarmonyId??WhySoLaggyPlugin.PluginGuid,targets,automatic);}'''))
edit('Helpers/PatchProfiler.cs',lambda s:s.replace('internal static bool Stop(){Enabled=false;return Session.Stop();}','internal static bool Stop(){return Session.Stop();}'))
edit('Helpers/HookSession.cs',lambda s:s.replace('Lookup.Clear();Faulted=false;return true;','Lookup.Clear();Faulted=false;harmony=null;return true;'))
edit('Helpers/ModConfigUiAdapter.cs',lambda s:s.replace('        private static MonoBehaviour _menu;','        private static MonoBehaviour _menu;\n        internal static bool IsMenuOpen=>_menu!=null&&_menu.isActiveAndEnabled;'))
edit('Helpers/StructuredLogger.cs',lambda s:s.replace('        private static readonly long FlushIntervalTicks = TimeSpan.TicksPerSecond;',''))
edit('Helpers/RpcMonitor.cs',lambda s:s.replace('        internal static void ResetEpoch()','''        internal static void ApplyCapacity(){_queue.Capacity=Math.Min(QueueCapacity,1024);}
        internal static void ReconfigureWatches(string extra,int capacity)
        {if(WatchedRecordPerMethodCapacity==capacity&&_extraWatchMethods.SetEquals((extra??"").Split(',').Select(x=>x.Trim()).Where(x=>x.Length>0)))return;
            AddExtraWatchMethods(extra);WatchedRecordPerMethodCapacity=capacity;_watchedByMethod.Clear();foreach(var name in WatchedMethods)_watchedByMethod[name]=new MethodBuffer(capacity);}
        internal static void ResetEpoch()'''))
edit('WhySoLaggy.csproj',lambda s:s.replace('  <ItemGroup>','''  <ItemGroup>
    <Reference Include="System.IO.Compression" />
    <Reference Include="System.IO.Compression.FileSystem" />
    <Reference Include="UnityEngine.InputLegacyModule"><HintPath>C:/SteamLibrary/steamapps/common/PEAK/PEAK_Data/Managed/UnityEngine.InputLegacyModule.dll</HintPath><Private>False</Private></Reference>
  </ItemGroup>
  <ItemGroup>''',1))
edit('Properties/AssemblyInfo.cs',lambda s:s+'\n[assembly: AssemblyInformationalVersion("1.0.5-dev.20261115")]\n')
print('Runtime hooks and configuration integrated')
