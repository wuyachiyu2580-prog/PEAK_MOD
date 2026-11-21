from pathlib import Path
import shutil
OUT=Path(__file__).resolve().parent
p=OUT/'compact_protected';p.mkdir(exist_ok=True)
for name in ['CaptureBuffer.cs','CaptureTiming.cs','StructuredEvent.cs','Support.cs','Replay.csproj']:
 shutil.copy2(OUT/'compact_network'/name,p/name)
f=p/'CaptureBuffer.cs';s=f.read_text(encoding='utf-8')
s=s.replace('critical=new Queue<Item>(),details=new Queue<Item>()','critical=new Queue<Item>(),network=new Queue<Item>(),details=new Queue<Item>()')
s=s.replace('criticalBytes,detailBytes','criticalBytes,networkBytes,detailBytes').replace('criticalBytes+detailBytes','criticalBytes+networkBytes+detailBytes')
s=s.replace('else criticalBytes-=size;','else if(queue==network)networkBytes-=size;else criticalBytes-=size;')
s=s.replace('while(details.Count>0&&details.Peek().Time<cutoff)','while(network.Count>0&&network.Peek().Time<cutoff)RemoveEvent(network,true,false);'+chr(10)+'            while(details.Count>0&&details.Peek().Time<cutoff)')
s=s.replace('var queue=anchor?anchors:important?critical:details;','bool net=e.Type==EventType.NetworkSample;var queue=anchor?anchors:net?network:important?critical:details;')
s=s.replace('anchor?Limit*3L/100:important?Limit*7L/100:Limit/2','anchor?Limit*3L/100:net?Limit*5L/100:important?Limit*2L/100:Limit/2')
s=s.replace('anchor?anchorBytes:important?criticalBytes:detailBytes','anchor?anchorBytes:net?networkBytes:important?criticalBytes:detailBytes')
s=s.replace('else if(important)criticalBytes+=eventSize;','else if(net)networkBytes+=eventSize;else if(important)criticalBytes+=eventSize;')
s=s.replace('foreach(var item in details)destination.Add','foreach(var item in network)destination.Add(item.Expand(),now,window);'+chr(10)+'            foreach(var item in details)destination.Add')
s=s.replace('anchors.Concat(critical).Concat(details)','anchors.Concat(critical).Concat(network).Concat(details)')
s=s.replace('critical.Clear();details.Clear();','critical.Clear();network.Clear();details.Clear();').replace('criticalBytes=detailBytes=0','criticalBytes=networkBytes=detailBytes=0')
f.write_text(s,encoding='utf-8')
print('Prepared compact protected candidate: frame40 anchor3 protected2 network5 details50 percent')
