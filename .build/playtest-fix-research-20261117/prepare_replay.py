from pathlib import Path
import json
ROOT=Path(__file__).resolve().parents[2]
OUT=Path(__file__).resolve().parent
SRC=ROOT/'MOD开发/WhySoLaggy/WhySoLaggy/Helpers'
def block(text,start):
 p=text.index(start);a=text.index('{',p);d=1;i=a+1
 while d:
  if text[i]=='{':d+=1
  elif text[i]=='}':d-=1
  i+=1
 return text[p:i]
budget=block((SRC/'CaptureController.cs').read_text(encoding='utf-8-sig'),'internal static class CapturePayloadBudget')
support='using System; using System.Linq; namespace WhySoLaggy {'+budget+' internal static class EventCodec {internal static int Size(StructuredEvent e)=>256+(e.Timestamp?.Length??0)*2+(e.Fields?.Sum(x=>64+x.Key.Length*2+(x.Value is string s?s.Length*2:16))??0);}}'
for mode in ['baseline','network_partition']:
 p=OUT/mode;p.mkdir(exist_ok=True)
 for name in ['CaptureBuffer.cs','CaptureTiming.cs','StructuredEvent.cs']:
  text=(SRC/name).read_text(encoding='utf-8-sig')
  if name=='CaptureBuffer.cs' and mode=='network_partition':
   text=text.replace('critical=new Queue<Item>(),details=new Queue<Item>()','critical=new Queue<Item>(),network=new Queue<Item>(),details=new Queue<Item>()')
   text=text.replace('criticalBytes,detailBytes','criticalBytes,networkBytes,detailBytes').replace('criticalBytes+detailBytes','criticalBytes+networkBytes+detailBytes')
   text=text.replace('else criticalBytes-=size;','else if(queue==network)networkBytes-=size;else criticalBytes-=size;')
   text=text.replace('while(details.Count>0&&details.Peek().Time<cutoff)', 'while(network.Count>0&&network.Peek().Time<cutoff)RemoveEvent(network,true,false);'+chr(10)+'            while(details.Count>0&&details.Peek().Time<cutoff)')
   text=text.replace('var queue=anchor?anchors:important?critical:details;','bool net=e.Type==EventType.NetworkSample;var queue=anchor?anchors:net?network:important?critical:details;')
   text=text.replace('anchor?Limit*3L/100:important?Limit*7L/100:Limit/2','anchor?Limit*3L/100:net?Limit*7L/100:important?Limit*7L/100:Limit*43L/100')
   text=text.replace('anchor?anchorBytes:important?criticalBytes:detailBytes','anchor?anchorBytes:net?networkBytes:important?criticalBytes:detailBytes')
   text=text.replace('else if(important)criticalBytes+=eventSize;','else if(net)networkBytes+=eventSize;else if(important)criticalBytes+=eventSize;')
   text=text.replace('foreach(var item in details)destination.Add', 'foreach(var item in network)destination.Add(item.Expand(),now,window);'+chr(10)+'            foreach(var item in details)destination.Add')
   text=text.replace('anchors.Concat(critical).Concat(details)','anchors.Concat(critical).Concat(network).Concat(details)')
   text=text.replace('critical.Clear();details.Clear();','critical.Clear();network.Clear();details.Clear();').replace('criticalBytes=detailBytes=0','criticalBytes=networkBytes=detailBytes=0')
  (p/name).write_text(text,encoding='utf-8')
 (p/'Support.cs').write_text(support,encoding='utf-8')
 (p/'Replay.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><EnableDefaultCompileItems>true</EnableDefaultCompileItems></PropertyGroup><ItemGroup><Compile Include="../Replay.cs" Link="Replay.cs" /></ItemGroup></Project>',encoding='utf-8')
a=json.loads((ROOT/'.build/playthrough-20261117/audit.json').read_text(encoding='utf-8'))
profile=Path('C:/Users/Administrator/AppData/Roaming/r2modmanPlus-local/PEAK/profiles/2.0.a/BepInEx')
meta={'session':a['sessionId'],'files':[str(profile/x['file']) for x in a['jsonl']['fileRanges'] if x['rows']],'archives':[]}
import zipfile
for x in a['archives']:
 with zipfile.ZipFile(ROOT/'.build/playthrough-20261117/evidence'/x['file']) as z:
  m=json.loads(z.read('manifest.json'));es=[json.loads(l) for l in z.read('events.jsonl').decode('utf-8-sig').splitlines() if l]
 meta['archives'].append({'id':m['CaptureId'],'start':m['RequestedStartSeconds'],'file':x['file'],'actualSequences':[e['sequence'] for e in es]})
(OUT/'replay-input.json').write_text(json.dumps(meta,ensure_ascii=False),encoding='utf-8')
print('Prepared isolated baseline and candidate with production buffer sources; input archives',len(meta['archives']))
