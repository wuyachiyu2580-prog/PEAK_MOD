from pathlib import Path
from collections import Counter, defaultdict
from datetime import datetime
import csv, json, hashlib, re, zipfile, shutil, math

ROOT = Path(__file__).resolve().parents[2]
OUT = Path(__file__).resolve().parent
PROFILE = Path('C:/Users/Administrator/AppData/Roaming/r2modmanPlus-local/PEAK/profiles/2.0.a/BepInEx')
UNITY = Path('C:/Users/Administrator/AppData/LocalLow/LandCrab/PEAK/Player.log')
ZIPS = sorted((PROFILE/'WhySoLaggy/diagnostics').glob('*.zip'), key=lambda p:p.stat().st_mtime)
with zipfile.ZipFile(ZIPS[-1]) as z:
    SESSION = json.loads(z.read('manifest.json'))['SessionId']

def stats(v):
    a=sorted(float(x) for x in v if x is not None)
    if not a:return {'n':0}
    return {'n':len(a),'mean':sum(a)/len(a),'p50':a[int((len(a)-1)*.5)],'p95':a[int((len(a)-1)*.95)],'p99':a[int((len(a)-1)*.99)],'max':a[-1]}
def sha(p): return hashlib.sha256(p.read_bytes()).hexdigest().upper()
def num(e,k):
    try:return float(e.get(k) or 0)
    except (ValueError,TypeError):return 0
def compact(e):
    keys=['timestamp','frameNumber','monotonicSeconds','type','sceneState','scene','avgFrameMs','selfMainMs','coveredMs','unattributedMs','captureId']
    return {k:e[k] for k in keys if k in e}
def method_stats(rows):
    out={}
    for e in rows:
        k=e.get('name','');t=out.setdefault(k,{'records':0,'calls':0,'samples':0,'exceptions':0,'totalMs':0,'maxWindowMeanMs':0,'peakFrameMs':0,'owner':e.get('owner','')})
        t['records']+=1
        for target,src in [('calls','callCount'),('samples','sampleCount'),('exceptions','exceptions'),('totalMs','totalMs')]: t[target]+=num(e,src)
        t['maxWindowMeanMs']=max(t['maxWindowMeanMs'],num(e,'totalMs')/max(1,num(e,'callCount')))
        t['peakFrameMs']=max(t['peakFrameMs'],num(e,'peakFrameMs'))
    for t in out.values():
        t['observedMsPerCall']=t['totalMs']/max(1,t['calls']);t['coverage']=t['samples']/max(1,t['calls'])
    return dict(sorted(out.items(),key=lambda kv:kv[1]['totalMs'],reverse=True))

inventory=[]
for p in sorted(PROFILE.glob('whysolaggy_*'))+list(PROFILE.glob('WhySoLaggy*.log*'))+[PROFILE/'LogOutput.log',UNITY]+ZIPS:
    inventory.append({'path':str(p),'bytes':p.stat().st_size,'modified':datetime.fromtimestamp(p.stat().st_mtime).isoformat()})
evidence=OUT/'evidence';evidence.mkdir(exist_ok=True)
for p in [PROFILE/'LogOutput.log', UNITY, *ZIPS]:
    dest=evidence/p.name
    if not dest.exists():shutil.copy2(p,dest)

zip_results=[];zip_methods=[];zip_exceptions=Counter()
for p in ZIPS:
    with zipfile.ZipFile(p) as z:
        m=json.loads(z.read('manifest.json'))
        if m['SessionId']!=SESSION:continue
        rows=[json.loads(l) for l in z.read('events.jsonl').decode('utf-8-sig').splitlines() if l.strip()]
        frames=[e for e in rows if e['type']=='FrameSample'];ids=[int(e['frameNumber']) for e in frames]
        win=[e for e in rows if e.get('timingKind')=='window inclusive'];zip_methods.extend(win)
        exc=Counter()
        for e in rows:
            if e['type']=='ExceptionSummary':exc[e.get('traceStack','')]+=num(e,'count');zip_exceptions[e.get('traceStack','')]+=num(e,'count')
        tw=[e for e in rows if e['type']=='TimingWindow']
        zip_results.append({'file':p.name,'sha256':sha(p),'build':m.get('Build'),'captureId':m['CaptureId'],'requestedSeconds':m['RequestedEndSeconds']-m['RequestedStartSeconds'],'retainedSeconds':m['RetainedEndSeconds']-m['RetainedStartSeconds'],'preRollSeconds':m['RequestedStartSeconds']-m['RetainedFrameStartSeconds'],'frames':len(frames),'reportedFrames':m.get('RetainedFrameCount'),'frameMissingWithinSpan':max(ids)-min(ids)+1-len(set(ids)) if ids else None,'start':frames[0]['timestamp'] if frames else '', 'end':frames[-1]['timestamp'] if frames else '', 'drops':{k:m.get(k) for k in ['DroppedFrames','DroppedCriticalEvents','DroppedDetailEvents']},'types':dict(Counter(e['type'] for e in rows)),'exceptions':dict(exc),'methodOmissions':sum(num(e,'methodObservationsOmitted') for e in tw),'spikeDetailOmissions':sum(num(e,'spikeMethodDetailsOmitted') for e in tw),'wit':method_stats([e for e in win if 'WhereIsThing' in e.get('name','')])})

types=Counter();sessions=Counter();states=defaultdict(lambda:{'frame':[],'self':[],'covered':[]});metrics=defaultdict(list);exceptions=Counter();exception_records=[];methods=[];fps=[];periods=[];captures=[];spikes=[];frame_top=[];boundaries=[];file_ranges=[];network=Counter();network_rates=defaultdict(list);validity=Counter();disk_frames=0
last_seq=None;sequence_gaps=0;sequence_backtracks=0
for p in sorted(PROFILE.glob('whysolaggy_events*.jsonl'),key=lambda p:p.stat().st_mtime):
    first=last=None;count=0;errors=0
    with p.open(encoding='utf-8-sig') as h:
        for line in h:
            try:e=json.loads(line)
            except (ValueError,UnicodeError):errors+=1;continue
            sessions[e.get('sessionId','')]+=1
            if e.get('sessionId')!=SESSION:continue
            count+=1;first=first or e.get('timestamp');last=e.get('timestamp');t=e['type'];types[t]+=1
            seq=e.get('sequence')
            if seq is not None:
                if last_seq is not None:
                    sequence_gaps+=max(0,seq-last_seq-1);sequence_backtracks+=seq<=last_seq
                last_seq=seq
            if t=='FrameSample':
                disk_frames+=1;s=states[e.get('sceneState','unknown')]
                for k,src in [('frame','avgFrameMs'),('self','selfMainMs'),('covered','coveredMs')]:s[k].append(num(e,src))
                validity[e.get('validity','')]+=1
                for k in ['writerDropped','diskDroppedBatches','writerQueueBytes','capturePayloadBytes','methodRecordOmitted']:metrics[k].append(num(e,k))
                if num(e,'avgFrameMs')>50:spikes.append(compact(e))
                if num(e,'selfMainMs')>20:frame_top.append(compact(e))
            elif t in ('PluginTiming','PatchTiming'):methods.append(e)
            elif t=='ExceptionSummary':
                exceptions[e.get('traceStack','')]+=num(e,'count');exception_records.append(e)
            elif t=='FpsReport':fps.append(e)
            elif t=='PeriodicReport':periods.append(e)
            elif t=='CaptureState':captures.append(e)
            elif t=='SessionBoundary':boundaries.append(e)
            elif t=='NetworkSample':
                if e.get('rpcMethod'):network[e['rpcMethod']]+=num(e,'count');network_rates[e['rpcMethod']].append(num(e,'rate'))
                for k in ['rpcQueueDepth','queueDelayMs','detailDroppedTotal','detailSampledOut','ping']:
                    if k in e:metrics[k].append(num(e,k))
    file_ranges.append({'file':p.name,'rows':count,'start':first,'end':last,'parseErrors':errors})

csv_methods=[];csv_fps=[];csv_periods=[];csv_types=Counter();csv_ranges=[];csv_states=defaultdict(list);csv_sessions=Counter();csv_exception=Counter()
for p in sorted(PROFILE.glob('whysolaggy_data*.csv'),key=lambda p:p.stat().st_mtime):
    first=last=None;count=0
    with p.open(encoding='utf-8-sig',newline='') as h:
        for raw in csv.DictReader(h):
            csv_sessions[raw.get('SessionId','')]+=1
            if raw.get('SessionId')!=SESSION:continue
            e={k[:1].lower()+k[1:]:v for k,v in raw.items() if isinstance(k,str) and v!=''};t=e.get('type');count+=1;csv_types[t]+=1;first=first or e.get('timestamp');last=e.get('timestamp')
            if t in ('PluginTiming','PatchTiming'):csv_methods.append(e)
            elif t=='FpsReport':csv_fps.append(e)
            elif t=='PeriodicReport':csv_periods.append(e)
            elif t=='FrameSample':csv_states[e.get('sceneState','unknown')].append(num(e,'avgFrameMs'))
            elif t=='ExceptionSummary':csv_exception[e.get('traceStack','')]+=1
    csv_ranges.append({'file':p.name,'rows':count,'start':first,'end':last})

lines=UNITY.read_text(encoding='utf-8-sig',errors='replace').splitlines();unity_types=Counter();unity_stacks=Counter();unity_blocks=[]
for i,l in enumerate(lines):
    if re.match(r'^[A-Za-z.]+Exception:',l):
        block=[l]
        for s in lines[i+1:i+30]:
            if not s.strip():break
            block.append(s)
        joined='\n'.join(block);unity_types[l.split(':')[0]]+=1
        key='Compass' if 'Compass.CompassTapeUI' in joined else (block[1].strip() if len(block)>1 else l)
        unity_stacks[key]+=1
        if key!='Compass':unity_blocks.append({'line':i+1,'text':joined})

def unique_by_kind(es):return Counter(e.get('timingKind','') for e in es)
target_csv=[e for e in csv_methods if 'WhereIsThing' in e.get('name','') and e.get('timingKind')=='inclusive observed chain']
result={'sessionId':SESSION,'generatedAt':datetime.now().isoformat(),'inventory':inventory,'archives':zip_results,'zipWindowMethodTotals':method_stats(zip_methods),'zipExceptionCounts':dict(zip_exceptions),
    'jsonl':{'fileRanges':file_ranges,'sessions':dict(sessions),'types':dict(types),'validity':dict(validity),'sequenceGaps':sequence_gaps,'sequenceBacktracks':sequence_backtracks,'states':{k:{a:stats(b) for a,b in v.items()} for k,v in states.items()},'metrics':{k:stats(v) for k,v in metrics.items()},'exceptions':dict(exceptions),'methodKinds':dict(unique_by_kind(methods)),'periodicMethodTotals':method_stats([e for e in methods if e.get('timingKind')=='inclusive observed chain']),'fpsReports':fps,'periodicReports':periods,'captureStates':captures,'boundaries':boundaries,'networkCounts':dict(network),'networkRates':{k:stats(v) for k,v in network_rates.items()},'spikeCount':len(spikes),'worstSpikes':sorted(spikes,key=lambda x:x['avgFrameMs'],reverse=True)[:30],'selfAbove20Count':len(frame_top),'worstSelf':sorted(frame_top,key=lambda x:x['selfMainMs'],reverse=True)[:30]},
    'csv':{'fileRanges':csv_ranges,'sessions':dict(csv_sessions),'types':dict(csv_types),'states':{k:stats(v) for k,v in csv_states.items()},'methodKinds':dict(unique_by_kind(csv_methods)),'wit':method_stats(target_csv),'topPluginMethods':method_stats([e for e in csv_methods if e['type']=='PluginTiming' and e.get('timingKind')=='inclusive observed chain']),'fpsReports':csv_fps,'periodicReports':csv_periods,'exceptionRowCounts':dict(csv_exception)},
    'unity':{'exceptionTypes':dict(unity_types),'exceptionOrigins':dict(unity_stacks),'otherBlocks':unity_blocks,'targetMentions':{k:sum(k in l for l in lines) for k in ['WhereIsThing','WhySoLaggy','LOC: 0','GetDisplayName']}},
    'installed':{n:sha(PROFILE/'plugins'/n) for n in ['WhereIsThing.dll','WhySoLaggy.dll']}}
(OUT/'audit.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({'sessionId':SESSION,'archives':[{'file':x['file'],'duration':round(x['retainedSeconds'],3),'frames':x['frames'],'missing':x['frameMissingWithinSpan'],'drops':x['drops']} for x in zip_results],'jsonlTypes':dict(types),'jsonlStates':result['jsonl']['states'],'metrics':result['jsonl']['metrics'],'csvTypes':dict(csv_types),'csvStates':result['csv']['states'],'wit':result['csv']['wit'],'unity':{'types':dict(unity_types),'origins':dict(unity_stacks)},'output':str(OUT/'audit.json')},ensure_ascii=False,indent=2))
