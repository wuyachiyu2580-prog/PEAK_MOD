from pathlib import Path
import hashlib,json
ROOT=Path(__file__).resolve().parents[2]
OUT=Path(__file__).resolve().parent
heading='## 2026-11-17 一局实测复核：运行确认，仍有待修缺口'
report='PLAYTEST_WhereIsThing_WhySoLaggy_2026-11-17.md'
wsl='WhySoLaggy retention构建已有本局运行证据：10份保留ZIP、53,651帧均连续，约20秒预录完整；但8份包报告关键事件省略合计328条，按Sequence对照实际少582条，预录部分省略未传入manifest。主线程SelfMainMs在保留Gameplay段均值0.249ms、P99 1.628ms、峰值85.046ms，需分阶段取证。写盘/RPC队列丢弃为0；CSV+JSONL合计约603MiB，JSONL已轮换掉本局前段。'
wit='WhereIsThing fixes构建正常加载：700份周期报告累计Update 699,949次、全计时覆盖、0异常，均值0.117934ms/调用；诊断窗口反复出现约5秒一次的8–15ms尖峰，最大14.7521ms。5秒场景校验/Statue全局查询为优先候选，尚未证实具体子方法。退出配置RestrictClientPresets=false、Persistent；保留用户现值，默认仍true。晚安莓/共享/菜单等功能矩阵不能仅凭日志判通过。'
common='本局CSV覆盖19:38–21:34，安装DLL与部署哈希一致。Player.log中Compass相关空引用17,124条，不能误归因到这两个MOD；GetDisplayName/LOC: 0未复现。此次仅审计并保存报告/证据，未改生产源码、配置、DLL或发行，未启动游戏。当前属于部分实机验收，覆盖下方从未实机的旧阶段状态；旧记录保留。'
paths=[]
for mod,summary in [('WhySoLaggy',wsl),('WhereIsThing',wit)]:
 for name in ['README','STATUS','TODO','RECENT','DECISIONS','FILES']:
  paths.append((ROOT/'memory/mods'/mod/(name+'.md'),[summary],'../../'+report))
 paths.append((ROOT/'memory/mods'/mod/'temp/current.md',[summary],'../../../'+report))
for name in ['README','TODO','MEMORY_INDEX','CHANGELOG']:
 paths.append((ROOT/'memory'/(name+'.md'),[wsl,'',wit],report))
results=[]
for path,summary,link in paths:
 raw=path.read_bytes();raw.decode('utf-8-sig')
 if heading.encode('utf-8') in raw:continue
 nl=bytes([13,10]) if bytes([13,10]) in raw else bytes([10]);offset=raw.index(bytes([10]))+1
 lines=[heading,'']+summary+['',common,'','[本局完整审计、数据口径与待修优先级]('+link+')。证据：.build/playthrough-20261117/；只同步本地memory。']
 insert=nl+nl.join(line.encode('utf-8') for line in lines)+nl
 backup=OUT/'memory-before-review'/path.relative_to(ROOT);backup.parent.mkdir(parents=True,exist_ok=True);assert not backup.exists();backup.write_bytes(raw)
 expected=raw[:offset]+insert+raw[offset:];path.write_bytes(expected);actual=path.read_bytes()
 assert actual==expected and actual[:offset]+actual[offset+len(insert):]==raw
 assert actual.count(heading.encode('utf-8'))==1
 results.append(str(path.relative_to(ROOT)))
baseline=json.loads((OUT/'review-baseline.json').read_text(encoding='utf-8'))
changed=[p for p,h in baseline.items() if hashlib.sha256(Path(p).read_bytes()).hexdigest()!=h]
assert not changed,changed
assert (ROOT/'memory'/report).read_text(encoding='utf-8').startswith('# 2026-11-17')
print(json.dumps({'memoryUpdated':results,'historyBytesPreserved':True,'sourceInstalledConfigHashesUnchanged':len(baseline),'report':str(ROOT/'memory'/report)},ensure_ascii=False,indent=2))
