from pathlib import Path
from collections import Counter
import json,zipfile,csv
OUT=Path(__file__).resolve().parent
P=Path('C:/Users/Administrator/AppData/Roaming/r2modmanPlus-local/PEAK/profiles/2.0.a/BepInEx')
a=json.loads((OUT/'audit.json').read_text(encoding='utf-8')); sid=a['sessionId']
targets=[];peaks=[]
for info in a['archives']:
 with zipfile.ZipFile(OUT/'evidence'/info['file']) as z:
  m=json.loads(z.read('manifest.json'));es=[json.loads(l) for l in z.read('events.jsonl').decode('utf-8-sig').splitlines() if l]
 ids={int(e['sequence']) for e in es}; targets.append({'file':info['file'],'min':min(ids),'max':max(ids),'ids':ids,'trigger':m['RequestedStartSeconds'],'missing':Counter(),'missingBeforeTrigger':Counter(),'missingDuringRecording':Counter(),'examples':[]})
 frames={int(e['frameNumber']):e for e in es if e['type']=='FrameSample'}
 for e in es:
  if 'WhereIsThing' in e.get('name','') and e.get('timingKind')=='window inclusive':
   f=frames.get(int(e.get('peakFrame',-1)),{})
   peaks.append({'capture':info['file'],'method':e['name'],'peakFrameMs':e.get('peakFrameMs'),'frame':e.get('peakFrame'),'timestamp':f.get('timestamp'),'frameMs':f.get('avgFrameMs'),'selfMs':f.get('selfMainMs'),'sceneState':f.get('sceneState')})
for p in sorted(P.glob('whysolaggy_events*.jsonl'),key=lambda p:p.stat().st_mtime):
 with p.open(encoding='utf-8-sig') as h:
  for l in h:
   e=json.loads(l)
   if e.get('sessionId')!=sid:continue
   seq=int(e['sequence'])
   for t in targets:
    if t['min']<=seq<=t['max'] and seq not in t['ids']:
     typ=e['type'];t['missing'][typ]+=1;t['missingBeforeTrigger' if e['monotonicSeconds']<t['trigger'] else 'missingDuringRecording'][typ]+=1
     if len(t['examples'])<2:t['examples'].append({k:e.get(k) for k in ['timestamp','type','sequence','rpcMethod','exceptionKind']})
result={'captures':[{k:v for k,v in t.items() if k!='ids'} for t in targets],'witPeakFrames':sorted(peaks,key=lambda e:e['peakFrameMs'] or 0,reverse=True)[:25]}
(OUT/'capture-comparison.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(result,ensure_ascii=False,indent=2))
