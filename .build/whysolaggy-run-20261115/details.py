# -*- coding: utf-8 -*-
from pathlib import Path
import json,zipfile,re,collections
p=Path('C:/Users/Administrator/AppData/Roaming/r2modmanPlus-local/PEAK/profiles/2.0.a/BepInEx');root=Path(__file__).resolve().parents[2];d=json.loads((Path(__file__).parent/'audit.json').read_text(encoding='utf-8'))
print('retained range',min(x['first'] for x in d['files'].values()),max(x['last'] for x in d['files'].values()))
print('memory',json.dumps(d['memory'],ensure_ascii=False));print('last fps',json.dumps(d['lastFpsReports'][-1],ensure_ascii=False))
for z in d['zipReports']:print('zip',z['file'],z['first'],z['last'],z['uncompressed'],z['badZipMember'],z['config'])
path=next((p/'WhySoLaggy/diagnostics').glob('*.zip'))
with zipfile.ZipFile(path) as z:
 b=z.read('summary.txt');print('summary bytes',b[:100].hex());print('replacement chars',b.decode('utf-8').count('\ufffd'))
for name in ['CaptureController','UiText','ModConfigLocalization']:
 b=(root/f'MOD开发/WhySoLaggy/WhySoLaggy/Helpers/{name}.cs').read_bytes();print('source',name,'utf8 valid',len(b.decode('utf-8')),'replacement',b.decode('utf-8').count('\ufffd'),'BOM',b[:3].hex())
b=(p/'plugins/WhySoLaggy.dll').read_bytes();print('dll literal expected duration',b.find('持续时间'.encode('utf-16le')),'replacement chars',b.count('\ufffd'.encode('utf-16le')))
lag=(p/'WhySoLaggy.log').read_text(encoding='utf-8',errors='replace').splitlines();session=[x for x in lag if '25a3930ab2ff4b8fb75074f77e37be8a' in x];print('session start',session)
player=Path('C:/Users/Administrator/AppData/LocalLow/LandCrab/PEAK/Player.log').read_text(encoding='utf-8',errors='replace').splitlines();counts=collections.Counter();why=[]
for i,x in enumerate(player):
 if x.startswith('NullReferenceException'):
  counts[next((v.strip() for v in player[i+1:i+5] if v.strip()),'?')]+=1
 if 'WhySoLaggy' in x and ('Exception' in x or 'Error' in x):why.append(x[:200])
print('NRE methods',counts.most_common(10));print('why errors',why[:10])
for f in ['WhySoLaggy.log','WhySoLaggy_Abuse.log']:
 arr=(p/f).read_text(encoding='utf-8',errors='replace').splitlines();cur=[x for x in arr if x.startswith('[2026-11-15')];print(f,'current timestamps',cur[:2],cur[-1:])
