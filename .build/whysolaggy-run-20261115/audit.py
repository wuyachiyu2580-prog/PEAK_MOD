# -*- coding: utf-8 -*-
from pathlib import Path
from collections import Counter,defaultdict
import json,zipfile,re,hashlib,math
out=Path(__file__).resolve().parent
p=Path('C:/Users/Administrator/AppData/Roaming/r2modmanPlus-local/PEAK/profiles/2.0.a/BepInEx')
def quant(a):
 a=sorted(x for x in a if isinstance(x,(float,int)) and math.isfinite(x))
 return dict(n=len(a),mean=sum(a)/len(a),p50=a[(len(a)-1)//2],p95=a[math.ceil(len(a)*.95)-1],p99=a[math.ceil(len(a)*.99)-1],max=a[-1]) if a else {}
files=sorted(p.glob('whysolaggy_events*.jsonl'),key=lambda x:x.stat().st_mtime)
types=Counter();states=Counter();sessions=Counter();series=defaultdict(list);ranges={};errors=Counter();alerts=Counter();captures=[];boundary=[];spikes=[];maxima=defaultdict(float);capture_rows=Counter();fps=[];net=[];mem=[];lines=0
for f in files:
 first=last=None;cnt=Counter();bad=0
 with f.open(encoding='utf-8-sig') as inp:
  for line in inp:
   try:e=json.loads(line)
   except (ValueError,UnicodeError):bad+=1;continue
   typ=e.get('type','?');ts=e.get('timestamp');first=first or ts;last=ts;cnt[typ]+=1;types[typ]+=1;sessions[e.get('sessionId','?')]+=1;lines+=1
   if e.get('captureId'):capture_rows[e['captureId']]+=1
   if typ=='FrameSample':
    state=e.get('sceneState','?');states[state]+=1
    for key in ['avgFrameMs','selfMainMs','coveredMs']:
     if isinstance(e.get(key),(float,int)):series[state+':'+key].append(e[key]);series[('capture' if e.get('captureId') else 'standby')+':'+key].append(e[key])
    for key in ['writerDropped','diskDroppedBatches','writerQueueBytes','capturePayloadBytes','methodRecordOmitted']:maxima[key]=max(maxima[key],e.get(key,0) or 0)
   elif typ=='FpsReport':fps.append(e);mem.append({k:e.get(k) for k in ['timestamp','unityAllocatedBytes','managedUsedBytes','managedCapacityBytes','gcAllocationStatus','selfMainP95Ms','selfMainP99Ms','rpcQueueDropped']})
   elif typ=='CaptureState':captures.append({k:e.get(k) for k in ['timestamp','phase','reason','captureId','roomEpoch']})
   elif typ=='SessionBoundary':boundary.append({k:e.get(k) for k in ['timestamp','roomEpoch']})
   elif typ=='ExceptionSummary':errors[(e.get('exceptionKind'),str(e.get('traceStack','')).split('\n')[0])]+=e.get('count',0)
   elif typ=='AbuseAlert':alerts[e.get('alertType','?')]+=1
   elif typ=='SpikeFrame':spikes.append({k:e.get(k) for k in ['timestamp','avgFrameMs','coveredMs','unattributedMs','sceneState','captureId']})
   elif typ=='NetworkSample' and 'ping' in e:net.append(e)
 ranges[f.name]={'first':first,'last':last,'bytes':f.stat().st_size,'counts':dict(cnt),'invalidLines':bad}
zips=[]
for path in sorted((p/'WhySoLaggy/diagnostics').glob('*.zip')):
 with zipfile.ZipFile(path) as z:
  manifest=json.loads(z.read('manifest.json'));count=Counter();phase=Counter();samples=defaultdict(list);first=last=None;partial=0;privacy=[];n=0
  with z.open('events.jsonl') as inp:
   for line in inp:
    e=json.loads(line);n+=1;count[e['type']]+=1;first=first or e.get('timestamp');last=e.get('timestamp');phase[e.get('phase','')]+=1
    if e['type'] in ['PluginTiming','PatchTiming'] and e.get('callCount',0)>0:samples[e['type']].append(e.get('sampleCount',0)/e['callCount'])
    if e.get('validity')=='Incomplete':partial+=1
    for key in ['argsSummary','snapshot','message','targetPath','specificDesc']:
     if key in e:privacy.append(key)
  zips.append({'file':path.name,'bytes':path.stat().st_size,'uncompressed':sum(x.file_size for x in z.infolist()),'badZipMember':z.testzip(),'build':manifest.get('Build'),'config':manifest.get('Config'),'summary':z.read('summary.txt').decode('utf-8'),'first':first,'last':last,'rows':n,'types':dict(count),'phase':dict(phase),'sampleCoverage':{k:quant(v) for k,v in samples.items()},'incomplete':partial,'forbiddenFields':Counter(privacy),'entries':z.namelist()})
logs={}
for path in [p/'LogOutput.log',Path('C:/Users/Administrator/AppData/LocalLow/LandCrab/PEAK/Player.log'),p/'WhySoLaggy.log']:
 s=path.read_text(encoding='utf-8',errors='replace');arr=s.splitlines();errors0=Counter()
 for i,line in enumerate(arr):
  if re.search(r'Exception:|Error\s*:|^NullReferenceException|^ArgumentException|^MissingMethodException',line):errors0[re.sub(r'0x[0-9a-f]+','0x…',line)[:220]]+=1
 logs[path.name]={'bytes':path.stat().st_size,'lines':len(arr),'errors':errors0.most_common(12),'getDisplayName':sum('GetDisplayName' in x for x in arr),'loc0':sum('LOC: 0' in x for x in arr),'whyLagErrors':[x for x in arr if '[WHY_LAG]' in x and re.search('failed|skipped|overhead|stopped|Exception|error|Shutdown incomplete',x,re.I)][:25]}
result={'files':ranges,'eventCount':lines,'types':dict(types),'sessions':dict(sessions),'states':dict(states),'frameStats':{k:quant(v) for k,v in series.items()},'maxima':dict(maxima),'captureStates':captures,'captureRows':dict(capture_rows),'boundaries':boundary,'errors':[(list(k),v) for k,v in errors.most_common(12)],'alerts':dict(alerts),'largestSpikes':sorted(spikes,key=lambda x:x.get('avgFrameMs',0),reverse=True)[:12],'spikeCount':len(spikes),'fpsReports':len(fps),'lastFpsReports':fps[-3:],'memory':{'first':mem[:3],'last':mem[-3:]},'network':{k:quant([x.get(k) for x in net]) for k in ['ping','rttVariance','incomingCommands','outgoingCommands','reliableRetransmissions','detailDroppedTotal','detailSampledOut','cpuFrameMs','gpuFrameMs']},'zipReports':zips,'logs':logs,'storage':{ext:sum(x.stat().st_size for x in p.glob('*'+ext)) for ext in ['.csv','.jsonl','.log']}}
(out/'audit.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
compact={k:v for k,v in result.items() if k not in ['files','zipReports','lastFpsReports','memory','captureRows']};compact['archives']=[{k:z[k] for k in ['file','rows','first','last','summary','sampleCoverage','forbiddenFields']} for z in zips[:1]+zips[-1:]]
print(json.dumps(compact,ensure_ascii=False,indent=2))
