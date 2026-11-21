from collections import defaultdict
from dataclasses import dataclass
from pathlib import Path
import math,json,zipfile
OUT=Path(__file__).resolve().parent
ROOT=OUT.parents[1]

@dataclass
class Bucket:
 first:float
 last:float
 count:int=0

class LossLedger:
 # Research policy: fixed 128 one-second slots, bounded categories/reasons.
 # A partial boundary bucket returns honest lower/upper bounds, never invented precision.
 def __init__(self):self.data={};self.latest=-1;self.unknown_before=-math.inf
 def add(self,t,category='Detail',reason='Capacity'):
  sec=math.floor(t)
  if sec>self.latest:self.latest=sec
  cutoff=self.latest-127
  self.data={k:v for k,v in self.data.items() if k[0]>=cutoff}
  self.unknown_before=max(self.unknown_before,cutoff)
  if sec<cutoff:return
  k=(sec,category,reason);b=self.data.setdefault(k,Bucket(t,t));b.first=min(b.first,t);b.last=max(b.last,t);b.count+=1
 def query(self,start,end):
  low=high=0;unknown=start<self.unknown_before
  for b in self.data.values():
   if b.last<start or b.first>end:continue
   high+=b.count
   if b.first>=start and b.last<=end:low+=b.count
  return {'lower':low,'upper':None if unknown else high,'exact':not unknown and low==high}

checks=[]
def check(name,condition):
 if not condition:raise AssertionError(name)
 checks.append(name)
l=LossLedger();l.add(10);l.add(11);l.add(12);check('all inside exact',l.query(9,13)=={'lower':3,'upper':3,'exact':True})
check('expired not counted',l.query(11,20)['lower']==2)
l=LossLedger();l.add(10.1);l.add(10.9);check('partial cutoff bounded',l.query(10.5,20)=={'lower':0,'upper':2,'exact':False})
check('partial never claims zero exact',not l.query(10.5,20)['exact'])
check('single exact boundary inclusive',l.query(10.1,10.9)['lower']==2)
check('beyond latest zero',l.query(11,20)=={'lower':0,'upper':0,'exact':True})
l=LossLedger()
for i in range(10000):l.add(i,'Critical','Capacity')
check('journal remains bounded',len(l.data)==128)
check('recent requested window exact',l.query(9980,9999)['lower']==20)
check('outside journal unknown',l.query(0,9999)['upper'] is None)
l=LossLedger();l.add(99);l.add(101);check('victim time not eviction time',l.query(100,120)['lower']==1)
check('query idempotent',l.query(90,120)==l.query(90,120))
check('room boundary independent',LossLedger().query(0,20)['upper']==0)
# Each capture inherits a frozen summary once, and adds destination losses separately.
pre=l.query(90,120);saved=dict(pre);l.add(105);check('frozen inherited count immutable',saved['lower']==2)
destination=LossLedger();destination.add(106);check('inherited and new losses additive',saved['lower']+destination.query(90,120)['lower']==3)

a=json.loads((ROOT/'.build/playthrough-20261117/audit.json').read_text(encoding='utf-8'))
peak_gaps=[];peaks=[]
for z in a['archives']:
 with zipfile.ZipFile(ROOT/'.build/playthrough-20261117/evidence'/z['file']) as f:
  es=[json.loads(x) for x in f.read('events.jsonl').decode('utf-8-sig').splitlines() if x]
  fs={e['frameNumber']:e for e in es if e['type']=='FrameSample'}
  hits=sorted({fs[e['peakFrame']]['monotonicSeconds'] for e in es if e.get('name')=='WhereIsThing.WhereIsThingPlugin.Update()' and e.get('timingKind')=='window inclusive' and e.get('peakFrameMs',0)>=8 and e.get('peakFrame') in fs})
  peak_gaps += [v-u for u,v in zip(hits,hits[1:])];peaks+=hits
g=sorted(peak_gaps)
result={'policyChecks':checks,'passed':len(checks),'ledgerBound':'128 one-second slots per fixed category/reason combination','limitation':'Prototype policy only; partial cutoff returns lower/upper, no exact point count; production integration unimplemented','wit':{'peakCountAbove8ms':len(peaks),'gaps':len(g),'gapsIn4p5to5p3':sum(4.5<=x<=5.3 for x in g),'medianGap':g[len(g)//2],'minGap':min(g),'maxGap':max(g)}}
(OUT/'policy-result.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8');print(json.dumps(result,ensure_ascii=False,indent=2))
