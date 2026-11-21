from pathlib import Path

ROOT=Path('MOD开发')
def save(p,s,raw):
    eol=chr(13)+chr(10) if bytes([13,10]) in raw else chr(10)
    bom=bytes([239,187,191]) if raw.startswith(bytes([239,187,191])) else b''
    p.write_bytes(bom+s.replace(chr(10),eol).encode('utf-8'))
def change(p,old,new):
    p=Path(p);raw=p.read_bytes();s=raw.decode('utf-8-sig').replace(chr(13)+chr(10),chr(10))
    assert old in s,(p,old[:80]);s=s.replace(old,new);save(p,s,raw)
def wrap(p,signature,scope):
    p=Path(p);raw=p.read_bytes();s=raw.decode('utf-8-sig').replace(chr(13)+chr(10),chr(10))
    a=s.index('{',s.index(signature));depth=1;b=a+1
    while depth:
        if s[b]=='{':depth+=1
        if s[b]=='}':depth-=1
        b+=1
    body=s[a+1:b-1]
    s=s[:a+1]+chr(10)+'            using('+scope+')'+chr(10)+'            {'+body+chr(10)+'            }'+chr(10)+'        '+s[b-1:]
    save(p,s,raw)
wsl=ROOT/'WhySoLaggy/WhySoLaggy'
for file,sig,stage in [
 ('WhySoLaggyPlugin.cs','void Update()','Update'),
 ('WhySoLaggyPlugin.cs','void OnGUI()','Hud'),
 ('Helpers/DiagnosticCore.cs','private static void Boundary()','FrameBoundary'),
 ('Helpers/DiagnosticCore.cs','internal FrameStatistics Snapshot(double now)','Statistics'),
 ('Helpers/TimingLedger.cs','internal static FrameRecord Seal(','Seal'),
 ('Helpers/CaptureTimingWindow.cs','internal void Flush(','TimingWindowFlush'),
 ('Helpers/CaptureBuffer.cs','internal void Add(StructuredEvent','Pack')]:
    wrap(wsl/file,sig,'DiagnosticPhases.Measure(DiagnosticPhases.Stage.'+stage+')')
change(wsl/'Helpers/StructuredLogger.cs','var frozen=EventCodec.Freeze(evt);','StructuredEvent frozen;using(DiagnosticPhases.Measure(DiagnosticPhases.Stage.Freeze))frozen=EventCodec.Freeze(evt);')
change(wsl/'Helpers/CaptureController.cs','lock(gate){rolling.Add(e,DiagnosticClock.Now,20);recording?.Add(e,DiagnosticClock.Now,90);}', '''lock(gate)
            {
                using(DiagnosticPhases.Measure(DiagnosticPhases.Stage.RollingAdd))rolling.Add(e,DiagnosticClock.Now,20);
                using(DiagnosticPhases.Measure(DiagnosticPhases.Stage.RecordingAdd))recording?.Add(e,DiagnosticClock.Now,90);
            }''')
change(wsl/'Helpers/CaptureController.cs','rolling.CopyTo(recording,started,20);','using(DiagnosticPhases.Measure(DiagnosticPhases.Stage.PreRollCopy))rolling.CopyTo(recording,started,20);')
change(wsl/'Helpers/CaptureController.cs','rolling.CopyTo(copy,DiagnosticClock.Now,20);','using(DiagnosticPhases.Measure(DiagnosticPhases.Stage.PreRollCopy))rolling.CopyTo(copy,DiagnosticClock.Now,20);')
change(wsl/'WhySoLaggyPlugin.cs','RpcMonitor.Tick();','using(DiagnosticPhases.Measure(DiagnosticPhases.Stage.RpcPump))RpcMonitor.Tick();')
change(wsl/'Helpers/CaptureController.cs','+timingText+', '+lossText+Environment.NewLine+timingText+')
wit=ROOT/'WhereIsThing/WhereIsThing'
source=(wsl/'Helpers/BoundedPhaseProfiler.cs').read_text(encoding='utf-8-sig')
(wit/'Helpers/BoundedPhaseProfiler.cs').write_text(source.replace('namespace WhySoLaggy','namespace WhereIsThing'),encoding='utf-8')
print('Instrumented WhySoLaggy stages and created matching bounded WhereIsThing collector.')
