from pathlib import Path
import hashlib,json,xml.etree.ElementTree as ET

root=Path.cwd();out=root/'.build/playtest-fixes-20261117'
def digest(p): return hashlib.sha256(p.read_bytes()).hexdigest().upper()
def write(p,text):
    raw=p.read_bytes() if p.exists() else b''
    eol=chr(13)+chr(10) if bytes([13,10]) in raw else chr(10)
    bom=bytes([239,187,191]) if raw.startswith(bytes([239,187,191])) else b''
    p.write_bytes(bom+text.replace(chr(13)+chr(10),chr(10)).replace(chr(10),eol).encode('utf-8'))
def prepend_section(p,section):
    s=p.read_text(encoding='utf-8-sig');header=section.splitlines()[0]
    assert header not in s,('Already documented',p)
    first,body=s.split(chr(10),1);write(p,first+chr(10)*2+section.strip()+chr(10)*2+body.lstrip())

base=json.loads((out/'baseline.json').read_text(encoding='utf-8-sig'))
protected=[];changed=[]
for name,expected in base.items():
    p=Path(name);assert p.exists(),name
    actual=digest(p)
    if actual!=expected.upper():changed.append(str(p))
    if 'AppData' in name or '发行' in name:protected.append({'path':name,'sha256':actual,'unchanged':actual==expected.upper()})
assert all(x['unchanged'] for x in protected)
trx=max((out/'results').glob('*.trx'),key=lambda p:p.stat().st_mtime)
c=ET.parse(trx).find('.//{http://microsoft.com/schemas/VisualStudio/TeamTest/2010}Counters').attrib
assert int(c['failed'])==0 and int(c['passed'])==105,c
assert 'All 184 regression checks passed.' in (out/'wit-regression.log').read_text(encoding='utf-8-sig')
assert 'All 104 production state-flow checks passed.' in (out/'stateflow.log').read_text(encoding='utf-8-sig')
replay=json.loads((out/'replay-result.json').read_text());caps=replay['captures']
summary={'captures':len(caps),'frames':sum(x['frames'] for x in caps),'networkFieldChecks':sum(x['networkFieldChecks'] for x in caps),'networkMismatches':sum(x['networkMismatches'] for x in caps),'newlyMissingOldRows':sum(x['actualDifferenceMissing'] for x in caps),'sameRangeMissing':sum(sum(x['missing'].values()) for x in caps),'sameRangeRecovered':582-sum(sum(x['missing'].values()) for x in caps),'firstPreRollLoss':{k:v for k,v in caps[0]['preRollLoss'].items() if k!='rows'},'peakPayloadBytes':replay['peakPayloadBytes'],'remainingPayloadBytes':replay['remainingPayloadBytes'],'stressChecks':len(replay['stressChecks'])}
assert summary['newlyMissingOldRows']==summary['networkMismatches']==summary['remainingPayloadBytes']==0
artifacts=[]
for folder,names in [('whysolaggy-release',['WhySoLaggy.dll','WhySoLaggy.pdb']),('whereisthing/bin',['WhereIsThing.dll','WhereIsThing.pdb'])]:
    for name in names:
        p=out/folder/name;artifacts.append({'path':str(p),'bytes':p.stat().st_size,'sha256':digest(p)})
assert (out/'whysolaggy-release/WhySoLaggy.dll').read_bytes()==(out/'whysolaggy/bin/WhySoLaggy.dll').read_bytes()
source_files={}
for mod in ['WhereIsThing','WhySoLaggy']:
    for p in (root/'MOD开发'/mod).rglob('*'):
        if p.is_file() and p.suffix in ('.cs','.csproj','.ps1') and not any(x in p.parts for x in ['bin','obj','发行','.vs']):source_files[str(p.relative_to(root))]=digest(p)
record={'verifiedDate':'2026-11-18','changedFromTurnBaseline':changed,'protected':protected,'artifacts':artifacts,'tests':{'WhySoLaggy':105,'WhereIsThingHelper':184,'WhereIsThingStateFlow':104,'trx':str(trx)},'replay':summary,'sourceHashes':source_files}
write(out/'verification.json',json.dumps(record,ensure_ascii=False,indent=2))

report=root/'memory/IMPLEMENTATION_PlaytestFixes_2026-11-18.md'
artifact_rows=chr(10).join('| '+Path(a['path']).name+' | '+str(a['bytes'])+' | '+a['sha256']+' |' for a in artifacts)
text='''# 2026-11-18 实测问题修复实施与验收

用户在修复调研后回复“开始”，11月18日要求“继续”。本次已完成代码、正式回归、同批日志回放与隔离 Release 构建。下方研究阶段的“未实施”状态已被本报告覆盖，但历史证据保留。

代码和构建标识保留本轮开始日期：WhySoLaggy 1.0.5-dev.20261117.playtest、WhereIsThing 0.1.3-dev.20261117.playtest；插件/程序集仍为1.0.5/1.0.5.0与0.1.3/0.1.3.0。2026-11-18为最终验收日期，不能把11月17日的实测日志误写成新版本实机结果。

## 已实施

### WhySoLaggy

- CaptureLossLedger按被丢事件原MonotonicSeconds记录，5类别×3原因×128个1秒桶。类别为Frame/Anchor/Protected/Network/Detail，原因Capacity/Oversize/SharedBudget；自然时间过期不算容量损失。
- Begin请求预录范围固定为[started−20, started]，只继承一次独立快照；完整桶精确计数，边界桶给上下界，历史覆盖不足或账本分配失败标Unknown，UpperBound为null。不会把累计滚动损失全搬入新录制。
- Recording统计保留原兼容口径：目标缓冲在复制预录和正式录制期间发生的损失，含复制拒收；与继承的PreRoll不重复。旧Dropped字段不含继承损失，Network是旧Critical的子集。新的manifest.LossStatistics使用独立schema=1，事件schema仍为2。
- summary.txt和loss-summary.txt说明范围、上下界、未知与旧字段口径，支持英/简/繁；manifest按阶段、类别、原因输出。
- 独立CaptureNetwork使用固定数字列、类型/存在掩码和共享字符串身份。保留大整数、ulong、原数值类型、浮点、null及缺字段；未知字段或不支持类型回退通用记录，不删除字段。共享身份使用长度前缀避免分隔符碰撞。
- 分区为帧40%（含固定账本与快照预留）、锚点3%、保护摘要2%、网络5%、明细50%。滚动4MiB、录制8MiB、共享载荷32MiB上限不变。账本及快照预留65,792 bytes计费，小缓冲不足时明确Unknown。释放/Detach/导出引用覆盖正式回归；这是载荷账面上限，不等于整个CLR堆占用。
- SelfMetrics保持锁和原计数语义，更新时复用Metric对象，消除每次Tuple.Create分配；未据此声称Unity Mono帧率提升。
- 新增Logging/StructuredDiskFormat=Both|Jsonl|Csv，默认Both。主线程请求按IO队列顺序切换，先刷旧缓冲，失败时保留旧策略并等待重试；各格式轮换语义保持。内存采集和ZIP中的JSONL/CSV独立，三种格式均通过ZIP内容/匿名化回归。
- 阶段计时覆盖Update、FrameBoundary、Seal、Statistics、Freeze、RollingAdd、RecordingAdd、PreRollCopy、TimingWindowFlush、RpcPump、Hud、Pack。Pack是EventRing.Add包络，包含到期清理/压缩/入队，不应当作纯编码时间。
- 首次阶段日志在IO线程创建目录后写入，避免尚未生成ZIP时目录不存在；正式测试覆盖此路径。

### WhereIsThing

- 新增同规格限时聚合计时：整体Update、网络发现、延迟分类重试、实际网络清理、标签刷新/投影、Scene00至Scene19全部场景步骤。
- 记录峰值对应的已索引对象数、标签数、有效选择代数、sceneGeneration、选中物品数、位置范围与地图范围。这些是采样时上下文，不是每个步骤的逐对象处理计数。
- 本轮没有改为雕像缓存或分帧清理；5秒尖峰的具体子方法仍待实机阶段数据确认。晚安莓未摘/已摘统一入口、果实锚点/空株隐藏、根茎/曼德拉草、共享/客机限制规则保持。

### 两个MOD计时共同约定

Diagnostics/PhaseTiming默认false；开启一次窗口，PhaseTimingSeconds默认45，允许30–60秒。到期自动关闭配置，重新开启才再次采样。每次窗口固定内存聚合Count/TotalMs/MaxMs/P95UpperMs，不逐帧输出日志；P95UpperMs为直方图上界，GC.CollectionCount差分仅表示次数，不能转换成暂停时长。阶段包含嵌套，不可相加当总耗时；计时器开销与调度暂停也会进入墙钟测量。

WhySoLaggy输出BepInEx/WhySoLaggy/phase-timing.log并写插件日志；WhereIsThing输出BepInEx/LogOutput.log中的PhaseTiming报告。阶段报告不自动加入匿名ZIP。

## 验证结果

| 验证 | 最终结果 |
| --- | --- |
| WhySoLaggy .NET Framework 4.7.2 MSTest | 105/105通过（原78项保留） |
| WhereIsThing helper回归 | 184/184通过 |
| WhereIsThing生产状态流程/计时回归 | 104/104通过（原97项保留） |
| 两个真实游戏依赖Release构建 | 均0警告、0错误 |
| 正式缓冲代码回放 | 10份录制，53,651帧，旧事件新增丢失0 |
| 网络样本逐字段还原 | 3,297条，差异0 |
| 回放压力检查 | 5组通过，最终载荷预算0 |

同原始比较范围的缺失由582降至253，补回329条（NetworkSample316、FpsReport6、SpikeFrame6、AbuseAlert1），没有排挤原有明细。第一份包剩余253条预录明细缺失，新账本给出211–262条Bounded范围，实际253位于其中；其余9份预录损失Exact 0。此处不将新旧导出边界差异的额外1条计入329。

正式回放直接链接六个当前生产源码文件，预算类和Size公式从生产提取。回放在.NET 10运行；正式单元测试、逻辑替身和静态Release构建均不代替Unity/Photon实机验收。帧率、GC暂停改善和WhereIsThing周期尖峰消除仍未证明。

## 产物及保护核验

隔离输出位于工作区.build/playtest-fixes-20261117/：

- whysolaggy-release/WhySoLaggy.dll及.pdb；DLL与执行105项测试的whysolaggy/bin/WhySoLaggy.dll逐字节一致。
- whereisthing/bin/WhereIsThing.dll及.pdb。
- results/保存历次TRX；以verification.json指向的最终105项成功记录为准。较早的失败记录保留排错轨迹。
- whysolaggy-tests.log、whysolaggy-build.log、whereisthing-build.log、wit-regression.log、stateflow.log。
- replay-result.json、replay.log、replay/；verification.json含源码/产物/受保护文件哈希与结果汇总。
- backup/与baseline.json保存本轮开始时的已有改动。没有重置Git HEAD或覆盖上轮未提交工作。

| 文件 | bytes | SHA256 |
| --- | ---: | --- |
'''+artifact_rows+'''

收尾核对66个已安装DLL/PDB/cfg及历史发行文件全部与本轮基线一致。本轮未部署到2.0.a、未启动游戏、未改用户配置、未提交或发行。客机配置RestrictClientPresets=false保留，代码默认仍true。安装目录中的旧dev构建仍是上一局运行版本。

## 下一轮实测

1. 使用本报告列出的隔离DLL/PDB安排部署；确认版本后，再在游戏内开启限时阶段计时。当前尚未部署，旧安装版本不会出现这些新配置。
2. 保持原重诊断/共享/选择配置，先运行一段PhaseTiming关闭的对照；不要同时改变ManualPluginProfiling、ManualPatchProfiling或持续日志格式。
3. 在实际扫描场景把两MOD的Diagnostics/PhaseTiming设为true，45秒足够覆盖多次5秒周期；采集结束确认自动变false并保留PhaseTiming报告。不要仅在启动菜单开启导致进入场景前已超时。
4. 对比Scene00Statues、NetworkCleanup、其他场景步骤与Update包络，查看峰值上下文；确认根因后再选sceneGeneration缓存或分批清理。重复一轮关闭计时对照，避免把诊断开销当成常驻成本。
5. 做一份30秒手动录制（需要时延长/触发60秒），核对20秒预录、连续帧、LossStatistics及独立Network损失；网络/异常洪泛下允许有界丢失但必须披露。
6. 继续检查晚安莓采摘/恢复/空株、护符隐藏恢复和延迟生成、CurrentSegment/WholeMap、客机本地/共享预设及菜单开关。Compass异常作为独立因素记录，不混作本轮修复效果。

参考：[实机复核](PLAYTEST_WhereIsThing_WhySoLaggy_2026-11-17.md)、[修复方案调研](RESEARCH_PlaytestFixes_2026-11-17.md)。只同步本地memory。
'''
write(report,text)

header='## 2026-11-18 实测修复已实施，隔离验收通过'
shared='已按用户“开始/继续”完成正式修改；构建标识保留20261117.playtest。当前未部署、未启动游戏，66个安装DLL/PDB/cfg及历史发行文件核验不变。旧“未实施”段落仅为历史，以本条为准。'
wsl='WhySoLaggy已接入预录LossLedger及范围/未知披露、独立紧凑网络存储与2%保护/5%网络配额、SelfMetrics对象复用、默认关闭的30–60秒阶段计时、默认Both的可选持续日志格式。105/105正式测试、真实依赖Release零警告零错误；10包回放保持53,651帧、网络3,297条零字段差异、补回329条且无新增旧事件丢失。剩余253条预录明细以211–262上下界披露；最终共享预算0。'
wit='WhereIsThing已加入默认关闭、45秒默认/30–60秒范围的阶段计时，覆盖网络清理、重试、20个场景步骤、标签刷新/投影及Update，并记录峰值上下文。184+104=288项回归通过，真实依赖Release零警告零错误。未改扫描语义；5秒尖峰尚待实机归因，不宣称性能问题已消除。'
pending='下一步：部署后在实际扫描场景采集45秒阶段报告并做开关对照，再决定雕像缓存/网络清理分批方案；继续晚安莓、护符、共享与菜单实机矩阵。Unity Mono收益仍待验。'
for name in ['README','TODO','MEMORY_INDEX','CHANGELOG']:
    section=header+chr(10)*2+wsl+chr(10)*2+wit+chr(10)*2+shared+chr(10)*2+pending+chr(10)*2+'[实施报告、配置用法、隔离产物与哈希](IMPLEMENTATION_PlaytestFixes_2026-11-18.md)。'
    prepend_section(root/'memory'/(name+'.md'),section)
for mod,content in [('WhySoLaggy',wsl),('WhereIsThing',wit)]:
    for name in ['README','STATUS','TODO','RECENT','DECISIONS','FILES','temp/current']:
        link='../../../' if name.startswith('temp/') else '../../'
        extra=''
        if name=='FILES':extra='关键新增源码：'+('Helpers/CaptureLossLedger.cs、CaptureNetwork.cs、BoundedPhaseProfiler.cs、DiagnosticPhases.cs及Tests/PlaytestFixTests.cs。' if mod=='WhySoLaggy' else 'Helpers/BoundedPhaseProfiler.cs、WhereIsThingPlugin.Diagnostics.cs；StateFlow直接编译生产计时器。')
        if name=='DECISIONS':extra='嵌套阶段不可相加，P95为直方图上界，GC差分不是暂停时长；缺乏子方法证据时先计量，不盲目替换发现API或缓存身份。'
        section=header+chr(10)*2+content+chr(10)*2+shared+chr(10)*2+pending+chr(10)*2+extra+chr(10)*2+'[实施报告、验证边界与下一轮实测]('+link+'IMPLEMENTATION_PlaytestFixes_2026-11-18.md)。'
        prepend_section(root/'memory/mods'/mod/(name+'.md'),section)
prepend_section(root/'MOD开发/WhySoLaggy/CHANGELOG.md','''## 开发构建 2026-11-17（2026-11-18 验收）

- 修复预录容量省略未继承：按原事件时间窗导出独立损失上下界与Unknown状态，保留旧Dropped计数含义。
- 紧凑存储网络样本，独立网络与保护摘要配额，保留未知字段回退及数值/null语义；不增加原载荷上限或缩减明细配额。
- SelfMetrics复用统计对象；新增默认关闭的限时阶段计时，以及默认Both的StructuredDiskFormat可选输出。
- 105/105正式测试通过；同批10包回放补回329条、无新增旧事件丢失、53,651帧保留。Release零警告零错误，隔离构建未部署；Unity性能收益待实测。
''')
prepend_section(root/'MOD开发/WhereIsThing/CHANGELOG.md','''## 开发构建 2026-11-17（2026-11-18 验收）

- 新增Diagnostics/PhaseTiming与PhaseTimingSeconds：默认关闭，一次30–60秒窗口，结束自动关闭。
- 聚合网络发现/重试/清理、20个场景步骤、标签刷新/投影和Update；附峰值对象/标签/选择代数等上下文。P95为直方图上界，阶段嵌套不可相加。
- 原扫描/晚安莓/共享行为保持；先采集证据再决定周期尖峰优化。
- 184项helper+104项生产流程检查通过；真实依赖Release零警告零错误。本次隔离构建未部署、未实机。
''')
p=root/'MOD开发/WhereIsThing/Tests/README.md';s=p.read_text(encoding='utf-8-sig').replace('The 97 checks compile','The 104 checks compile')
s+='''
## Bounded phase timing (2026-11-18)

Seven additional state-flow checks compile the production BoundedPhaseProfiler: default-off behavior, P95 upper bounds, deadline stop, peak context, no self-restart, explicit rearm and stale scope rejection. Total: 184 helper + 104 state-flow checks. Production scan policy is unchanged. Config persistence and Unity/Photon runtime behavior still require in-game checks.
''';write(p,s)
write(out/'README.md','''# 实测修复隔离产物

2026-11-18最终验收；开发标识20261117.playtest。未部署到游戏profile。

- WhySoLaggy: whysolaggy-release/WhySoLaggy.dll + WhySoLaggy.pdb；105项测试通过。
- WhereIsThing: whereisthing/bin/WhereIsThing.dll + WhereIsThing.pdb；184+104项逻辑测试通过。
- verification.json为最终源码、二进制、安装保护哈希与验证结果。
- 回放10包、53,651帧，补回329条、无新增旧事件丢失；不等于Unity实机性能结论。
- 备份在backup/；instrument*.py是已执行的一次性编辑脚本，不要重复执行。

[完整实施与实测说明](../../memory/IMPLEMENTATION_PlaytestFixes_2026-11-18.md)
''')
print(json.dumps({'report':str(report),'memoryEntrypoints':18,'protectedUnchanged':len(protected),'tests':record['tests'],'artifacts':artifacts},ensure_ascii=False,indent=2))
