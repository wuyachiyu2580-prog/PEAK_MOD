from pathlib import Path
import hashlib,json
OUT=Path(__file__).resolve().parent;ROOT=OUT.parents[1]
heading='## 2026-11-17 实测修复方案已调研，未实施'
report='RESEARCH_PlaytestFixes_2026-11-17.md'
wsl='已用生产EventRing逻辑回放，本次10份ZIP的Sequence集合逐条一致。单纯新增网络分区挤占明细方案否决；紧凑NetworkSample+保护摘要候选在相同区间将缺失582降至253（补回329），无新增原事件丢失，53,651帧保持，3,297条网络字段还原与5组压力检查通过。剩余预录明细仍须LossLedger正确披露；14项时间窗计数策略检查通过但未接入生产。SelfMetrics复用对象验证减少Tuple分配，非Unity性能承诺。'
wit='已确认67个Update峰值≥8ms；57个同录制间隔中56个在4.5–5.3秒，中位5.051885秒。场景雕像发现与网络记录清理均为5秒，不能直接认定单一根因。首选先限时阶段计量，再按证据采用sceneGeneration缓存或分帧清理；保留护符随机偏移、FakeItem隐藏/恢复、附加场景/延迟生成及晚安莓语义。'
boundary='用户本轮只授权先调研。仅新增隔离实验与报告，未改正式源码/正式测试/cfg/安装DLL/发行，未部署或启动游戏；87项文件哈希核验不变。研究运行在.NET 10，不是Unity Mono实机；不能把候选回放和旧78/281正式测试相加，不能标记问题已修复。'
paths=[]
for mod,summary in [('WhySoLaggy',wsl),('WhereIsThing',wit)]:
 for name in ['README','STATUS','TODO','RECENT','DECISIONS','FILES']:
  paths.append((ROOT/'memory/mods'/mod/(name+'.md'),[summary],'../../'+report))
 paths.append((ROOT/'memory/mods'/mod/'temp/current.md',[summary],'../../../'+report))
for name in ['README','TODO','MEMORY_INDEX','CHANGELOG']:paths.append((ROOT/'memory'/(name+'.md'),[wsl,'',wit],report))
done=[]
for p,summary,link in paths:
 raw=p.read_bytes();raw.decode('utf-8-sig')
 if heading.encode('utf-8') in raw:continue
 nl=bytes([13,10]) if bytes([13,10]) in raw else bytes([10]);offset=raw.index(bytes([10]))+1
 lines=[heading,'']+summary+['',boundary,'','[修复设计、被否决方案与验收边界]('+link+')。实验位于.build/playtest-fix-research-20261117/；仅同步本地memory。']
 addition=nl+nl.join(x.encode('utf-8') for x in lines)+nl
 b=OUT/'memory-before-research'/p.relative_to(ROOT);b.parent.mkdir(parents=True,exist_ok=True);assert not b.exists();b.write_bytes(raw)
 p.write_bytes(raw[:offset]+addition+raw[offset:]);check=p.read_bytes();assert check[:offset]+check[offset+len(addition):]==raw
 done.append(str(p.relative_to(ROOT)))
baseline=json.loads((OUT/'source-before.json').read_text(encoding='utf-8'));changed=[p for p,h in baseline.items() if hashlib.sha256(Path(p).read_bytes()).hexdigest()!=h]
assert not changed,changed
r=json.loads((OUT/'research-summary.json').read_text(encoding='utf-8'));r.update({'memoryEntriesUpdated':len(done),'sourceInstalledConfigUnchanged':len(baseline),'productionImplemented':False,'inGameValidated':False});(OUT/'research-summary.json').write_text(json.dumps(r,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(r,ensure_ascii=False,indent=2))
