from pathlib import Path
# Load only the editing helpers, never rerun the first instrumentation batch.
exec(Path('.build/playtest-fixes-20261117/instrument.py').read_text(encoding='utf-8').split("wsl=ROOT/")[0])
wit=ROOT/'WhereIsThing/WhereIsThing'
wrap(wit/'WhereIsThingPlugin.cs','private void Update()','_phaseProfiler.Measure(0)')
change(wit/'WhereIsThingPlugin.cs', '''        private void Update()
        {
            using''','''        private void Update()
        {
            TickPhaseTiming();
            using''')
change(wit/'WhereIsThingPlugin.cs','            ProcessPhotonDiscovery();','            UpdatePhaseContext();'+chr(10)+'            ProcessPhotonDiscovery();')
for file,sig,phase in [('WhereIsThingPlugin.cs','private void RefreshLabels()',4),
 ('WhereIsThingPlugin.Discovery.cs','private void ProcessPhotonDiscovery()',1),
 ('WhereIsThingPlugin.Discovery.cs','private void ProcessClassificationRetries()',2)]:
    wrap(wit/file,sig,'_phaseProfiler.Measure('+str(phase)+')')
# Wrap real cleanup only, excluding its cheap not-due returns.
change(wit/'WhereIsThingPlugin.Discovery.cs','            _nextNetworkCleanup = Time.unscaledTime + FullValidationInterval;', '''            using(_phaseProfiler.Measure(3))
            {
            _nextNetworkCleanup = Time.unscaledTime + FullValidationInterval;''')
change(wit/'WhereIsThingPlugin.Discovery.cs','''                _networkObjects.Remove(viewId);
            }
        }''','''                _networkObjects.Remove(viewId);
            }
            }
        }''')
change(wit/'WhereIsThingPlugin.SceneDiscovery.cs','            switch (_sceneDiscoveryStep++)','            using(_phaseProfiler.Measure(6+_sceneDiscoveryStep))'+chr(10)+'            switch (_sceneDiscoveryStep++)')
change(wit/'WhereIsThingPlugin.cs','            Camera camera = GetMainCamera();','            using(_phaseProfiler.Measure(5))'+chr(10)+'            {'+chr(10)+'            Camera camera = GetMainCamera();')
change(wit/'WhereIsThingPlugin.cs','''        private void LateUpdate()''','''        private void LateUpdate()''')
# Projection is the final block of Update, immediately inside its outer timing scope.
p=wit/'WhereIsThingPlugin.cs';raw=p.read_bytes();s=raw.decode('utf-8-sig').replace(chr(13)+chr(10),chr(10))
end=s.index('        private void LateUpdate()');s=s[:end].rstrip()[:-1]+'}'+chr(10)+'        }'+chr(10)+chr(10)+s[end:];save(p,s,raw)
change(wit/'Helpers/ModConfigLocalization.cs','''                case "Enabled": return chinese ? "启用或关闭 WhereIsThing。" : "Enable or disable WhereIsThing.";''','''                case "PhaseTiming": return chinese ? "采集一次限时阶段耗时，结束后自动关闭。嵌套阶段不可重复相加。" : "Run one bounded diagnostic window, then automatically switch off. Nested phases must not be summed.";
                case "PhaseTimingSeconds": return chinese ? "诊断窗口持续 30 至 60 秒。P95 为直方图上界；GC 增量是次数，不是暂停耗时。" : "Diagnostic window duration, 30 to 60 seconds. P95 is a histogram upper bound; GC deltas are counts, not pause durations.";
                case "Enabled": return chinese ? "启用或关闭 WhereIsThing。" : "Enable or disable WhereIsThing.";''')
test=ROOT/'WhereIsThing/Tests/StateFlow'
change(test/'WhereIsThing.StateFlow.csproj','    <Compile Include="GameRuntimeStubs.cs" />','''    <Compile Include="GameRuntimeStubs.cs" />
    <Compile Include="../../WhereIsThing/Helpers/BoundedPhaseProfiler.cs" />''')
change(test/'GameRuntimeStubs.cs','    public sealed partial class WhereIsThingPlugin'+chr(10)+'    {','''    public sealed partial class WhereIsThingPlugin
    {
        private readonly BoundedPhaseProfiler _phaseProfiler=new BoundedPhaseProfiler("Update","Network","Retry","Cleanup","Refresh","Projection");
        private void TickPhaseTiming() { }
        private void UpdatePhaseContext() { }''')
for mod,version,previous in [('WhySoLaggy','1.0.5','1.0.5-dev.20261116.retention'),('WhereIsThing','0.1.3','0.1.3-dev.20261116.fixes')]:
    change(ROOT/mod/mod/'Properties/AssemblyInfo.cs',previous,version+'-dev.20261117.playtest')
print('WhereIsThing stages instrumented; scan choices unchanged; test extraction wired.')
