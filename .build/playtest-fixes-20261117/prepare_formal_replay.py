from pathlib import Path
import json
root=Path.cwd();out=root/'.build/playtest-fixes-20261117/replay';out.mkdir(exist_ok=True)
src=root/'MOD开发/WhySoLaggy/WhySoLaggy/Helpers'
def block(text,start):
    a=text.index(start);i=text.index('{',a)+1;depth=1
    while depth:
        if text[i]=='{':depth+=1
        if text[i]=='}':depth-=1
        i+=1
    return text[a:i]
budget=block((src/'CaptureController.cs').read_text(encoding='utf-8-sig'),'internal static class CapturePayloadBudget')
codec=(src/'DiagnosticIO.cs').read_text(encoding='utf-8-sig')
size=codec[codec.index('internal static int Size('):codec.index('internal static string Limit(')].strip()
support='using System; using System.Linq; namespace WhySoLaggy {'+budget+' internal static class EventCodec {'+size+'}'+'''
 internal static class DiagnosticPhases {
 internal enum Stage { Pack }
 internal static readonly BoundedPhaseProfiler Profiler=new BoundedPhaseProfiler("Pack");
 internal static BoundedPhaseProfiler.Scope Measure(Stage stage)=>Profiler.Measure((int)stage);
 }}'''
(out/'Support.cs').write_text(support,encoding='utf-8')
files=['CaptureBuffer.cs','CaptureTiming.cs','CaptureNetwork.cs','CaptureLossLedger.cs','StructuredEvent.cs','BoundedPhaseProfiler.cs']
links=''.join('<Compile Include="'+str(src/name).replace(chr(92),'/')+'" Link="'+name+'" />' for name in files)
(out/'Replay.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework></PropertyGroup><ItemGroup>'+links+'</ItemGroup></Project>',encoding='utf-8')
replay=(root/'.build/playtest-fix-research-20261117/Replay.cs').read_text(encoding='utf-8-sig')
replay=replay.replace('new CaptureTiming(edge,new CaptureTimingIdentity(edge,CaptureTimingIdentity.KeyOf(edge)))','new CaptureNetwork(edge,new CaptureNetworkIdentity(edge.Fields,CaptureNetworkIdentity.KeyOf(edge.Fields)))')
replay=replay.replace('rolling.CopyTo(recording,started,90)','rolling.CopyTo(recording,started,20)')
replay=replay.replace('payload=record.Bytes','payload=record.Bytes,preRollLoss=new {lower=record.PreRollLoss.Total.Lower,upper=record.PreRollLoss.Total.Upper,quality=record.PreRollLoss.Total.Quality,rows=record.PreRollLoss.Rows("PreRoll")},recordingLoss=record.RecordingLoss.Rows("Recording")')
(out/'Replay.cs').write_text(replay,encoding='utf-8')
print('Replay links six production source files; budget and size accounting extracted unchanged.')
