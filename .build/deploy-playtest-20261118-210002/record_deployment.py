from pathlib import Path
import hashlib,json
root=Path(__file__).resolve().parents[2]
backup=Path(__file__).resolve().parent
d=json.loads((backup/'deployment.json').read_text(encoding='utf-8-sig'))
def digest(p): return hashlib.sha256(p.read_bytes()).hexdigest().upper()
assert all(digest(Path(x['path']))==x['sha256'] for x in d['installed'])
assert all(digest(Path(x['path']))==x['sha256'] for x in d['configs'])
assert all(digest(backup/'plugins'/x['name'])==x['sha256'] for x in d['before'])
header='## 2026-11-18 已部署 2.0.a，等待实机验证'
status='按用户“先装到2.0.a的目录里”要求，21:00（Asia/Shanghai）已安装WhySoLaggy 1.0.5-dev.20261117.playtest及WhereIsThing 0.1.3-dev.20261117.playtest的DLL/PDB到2.0.a/BepInEx/plugins根目录。部署前确认PEAK未运行；递归检查每文件仅一份，4项安装哈希匹配已验证产物，2份cfg哈希保持不变。旧版及配置备份在.build/deploy-playtest-20261118-210002/，部署清单deployment.json。未启动游戏、未做新实机、未修改历史发行。此条覆盖下方“未部署”历史状态。'
nextstep='下一步在2.0.a启动游戏，进入实际扫描场景后开启两MOD的Diagnostics/PhaseTiming；默认45秒自动关闭。先保留现有配置做对照，再读取阶段报告定位5秒尖峰。自动测试仍为WhySoLaggy 105项、WhereIsThing 184+104项；本次仅部署，未重跑测试。'
def add(p,body):
    raw=p.read_bytes();s=raw.decode('utf-8-sig');assert header not in s,p
    first,rest=s.replace(chr(13)+chr(10),chr(10)).split(chr(10),1)
    text=first+chr(10)*2+header+chr(10)*2+body+chr(10)*2+rest.lstrip()
    eol=chr(13)+chr(10) if bytes([13,10]) in raw else chr(10)
    p.write_bytes((bytes([239,187,191]) if raw.startswith(bytes([239,187,191])) else b'')+text.replace(chr(10),eol).encode('utf-8'))
paths=[root/'memory'/(n+'.md') for n in ['README','TODO','MEMORY_INDEX','CHANGELOG']]
for mod in ['WhySoLaggy','WhereIsThing']:
    paths += [root/'memory/mods'/mod/(n+'.md') for n in ['README','STATUS','TODO','RECENT','DECISIONS','FILES','temp/current']]
for p in paths:add(p,status+chr(10)*2+nextstep)
report=root/'memory/IMPLEMENTATION_PlaytestFixes_2026-11-18.md'
add(report,status+chr(10)*2+nextstep+chr(10)*2+'[部署前后哈希与回退备份清单](../.build/deploy-playtest-20261118-210002/deployment.json)。回退时先关闭游戏，只将备份plugins目录中的4个文件复制回目标；cfg备份仅供保留，不覆盖用户后续设置。下文记录的是实施阶段的隔离验收状态。')
add(root/'.build/playtest-fixes-20261117/README.md',status)
for p in paths+[report]:assert p.read_text(encoding='utf-8-sig').splitlines()[2]==header
print(json.dumps({'profile':d['profile'],'installedVerified':len(d['installed']),'configsUnchanged':len(d['configs']),'backupsVerified':len(d['before']),'memoryEntriesUpdated':len(paths),'backup':str(backup)},ensure_ascii=True))
