# -*- coding: utf-8 -*-
from pathlib import Path
p=Path('MOD开发/WhySoLaggy/WhySoLaggy/Helpers/CaptureController.cs')
s=p.read_text(encoding='utf-8-sig')
a=s.index('    internal sealed class EventRing')
b=s.index('    internal sealed class TriggerPolicy',a)
s=s[:a]+s[b:]
s=s.replace('private static readonly EventRing rolling=new EventRing(16*1024*1024);','''// Rolling 4 MiB, active 8 MiB, two frozen records and one export stay below 32 MiB.
        private static readonly EventRing rolling=new EventRing(4*1024*1024);
        private static readonly CaptureTimingWindow timing=new CaptureTimingWindow();''')
s=s.replace('new EventRing(16*1024*1024)','new EventRing(8*1024*1024)')
a=s.index('                if(frame.Methods.Count>0)foreach')
b=s.index('                budgetSum+=frame.SelfMs',a)
s=s[:a]+'''                timing.Observe(frame,StructuredLogger.WriteEvent);
'''+s[b:]
s=s.replace('{"MethodRecordOmitted",Math.Max(0,frame.Methods.Count-30)}','{"MethodRecordOmitted",0}')
s=s.replace('DiagnosticContext.CaptureId=Guid.NewGuid().ToString("N");','DiagnosticContext.CaptureId=Guid.NewGuid().ToString("N");timing.Reset(started);')
s=s.replace('foreach(var e in rolling.Snapshot())recording.Add(e,started,90);','rolling.CopyTo(recording,started,90);')
s=s.replace('if(!Busy)return;Phase=CapturePhase.Saving;','if(!Busy)return;timing.Flush(StructuredLogger.WriteEvent);Phase=CapturePhase.Saving;')
s=s.replace('foreach(var e in rolling.Snapshot())copy.Add(e,DiagnosticClock.Now,20);','rolling.CopyTo(copy,DiagnosticClock.Now,20);')
s=s.replace('record.Events.Length','record.Count').replace('foreach(var e in record.Events)','foreach(var e in record.Enumerate())').replace('record.Events.Where','record.Enumerate().Where')
s=s.replace('{"Phase",Phase.ToString()},{"Reason",Detail}','{"Phase",Phase.ToString()},{"Reason",Detail},{"PluginTimingMode","Exact"},{"PatchTimingMode",PatchProfiler.Adaptive?"Adaptive":"Exact"}')
s=s.replace('TargetOwners=null;ExceptionTargets=new MethodBase[0];ApplyHooks?.Invoke(false,null,ExceptionTargets);','TargetOwners=null;ExceptionTargets=new MethodBase[0];ApplyHooks?.Invoke(false,null,ExceptionTargets);')
s=s.replace('recording=null;while(saved.Count>0)saved.Dequeue().Release();','recording=null;while(saved.Count>0)saved.Dequeue().Release();timing.Reset(0);')
p.write_text(s,encoding='utf-8')
