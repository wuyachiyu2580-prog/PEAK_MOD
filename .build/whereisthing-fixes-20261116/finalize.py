from pathlib import Path
import hashlib,json,re
from datetime import datetime

ROOT=Path(__file__).resolve().parents[2]
OUT=Path(__file__).parent
baseline=json.loads((OUT/'baseline.json').read_text(encoding='utf-8'))
unrelated=[name for name in baseline if 'WhySoLaggy' in name or '发行' in name]
for name in unrelated:
    assert hashlib.sha256((ROOT/name).read_bytes()).hexdigest()==baseline[name],name
reg=(OUT/'regression.log').read_text(encoding='utf-8-sig')
flow=(OUT/'stateflow.log').read_text(encoding='utf-8-sig')
build=(OUT/'build.log').read_text(encoding='utf-8-sig')
assert 'All 184 regression checks passed.' in reg
assert 'All 97 production state-flow checks passed.' in flow
assert flow.count('PASS:')==97
assert '0 个警告' in build and '0 个错误' in build
dll=OUT/'bin/WhereIsThing.dll'
dll_hash=hashlib.sha256(dll.read_bytes()).hexdigest().upper()
assert dll_hash=='5FC795194989B4F0A47FF73B50914642FA86390526CD90EF2268B7D482AFBBC5'
assert dll.stat().st_size==189952
report='IMPLEMENTATION_Fixes_2026-11-16.md'
heading='## 2026-11-16 WhereIsThing 审查修复已实施，未部署'
summary='用户已授权开始修改。共享会话/发布失败重试、目录自动就绪补发、无关属性过滤、扫描计时与菜单生命周期已修；有效选择缓存、标签绑定/分类复用、距离与设置局部更新及确定残留清理已完成。184/184 helper + 97/97 生产流程回归，真实依赖 Release 0 警告 0 错误。开发标识 0.1.3-dev.20261116.fixes，未部署/实机/发行/提交。'
entries={
 'memory/mods/WhereIsThing/README.md':'当前接续以实施报告为准；下方审查/调研的“未修复”是历史状态。晚安莓统一入口、旧迁移、默认客机限制和协议 2 保持。',
 'memory/mods/WhereIsThing/STATUS.md':'生产修复已落地，程序集仍 0.1.3.0。产物仅 .build/whereisthing-fixes-20261116/bin/，DLL 189952 bytes；SHA256 '+dll_hash+'。逻辑测试不代表实机已通过。',
 'memory/mods/WhereIsThing/TODO.md':'下一步实机验证主客机/晚加入/换房/断线、配置计时、菜单中采摘/到期、背包与绳锚绑定、晚安莓/范围/中文布局。晚分类范围裁剪、段缓存、搜索复用、对象池/虚拟列表及保存防抖留待测量/独立设计；本轮不宣称性能收益。',
 'memory/mods/WhereIsThing/RECENT.md':'新增生产 State.cs 与正式 Tests/StateFlow 项目；真实主 Update/重试/绑定/Picker.Tick/文本方法构建时提取测试。修复发送、初始化、计时和菜单的跨流程缺口，共 281 项逻辑检查通过。',
 'memory/mods/WhereIsThing/DECISIONS.md':'自动共享/目录/房间状态更新不续时，显式用户操作保留原语义；已提交不等于服务器确认。晚分类无变化保留原 record，绑定变更必须重建；desired key 在复用前记录。复杂缓存与池化不抢先实施。',
 'memory/mods/WhereIsThing/FILES.md':'新增 WhereIsThingPlugin.State.cs、Tests/StateFlow 四文件及实施报告。隔离目录含 DLL/PDB、build/regression/stateflow 日志、baseline.json、backup/、final-summary.json。一次性 implement_state/implement_lifecycle/prepare_tests 脚本不可重复应用。',
 'memory/mods/WhereIsThing/temp/current.md':'停点：本轮修改与验证完成，尚未部署/实机。先读实施报告，不重做研究原型、不重跑一次性脚本。97 项正式流程测试使用当前生产方法，已替代 47 项候选原型作为本批验证。WhySoLaggy 与历史发行逐文件哈希未变。',
 'memory/README.md':'WhereIsThing 本批修复已完成并保存隔离产物；未安装，实机验收仍待执行。',
 'memory/TODO.md':'WhereIsThing 审查四类缺口及低风险优化已落地，下一步为实机与性能测量；上轮“先修共享/计时”待办已完成，复杂优化仍未执行。',
 'memory/MEMORY_INDEX.md':'新增 mods/WhereIsThing/IMPLEMENTATION_Fixes_2026-11-16.md，索引修复内容、281 项逻辑验证、隔离产物及未完成实机范围。',
 'memory/CHANGELOG.md':'- [新增] mods/WhereIsThing/IMPLEMENTATION_Fixes_2026-11-16.md：修复与验证记录。\n- [修改/索引] WhereIsThing 六件套/current、根 README/TODO/MEMORY_INDEX：同步已实施未部署状态；保留历史调研。',
}
for name,detail in entries.items():
    p=ROOT/name;raw=p.read_bytes();bom=raw.startswith(bytes([239,187,191]))
    content=raw.decode('utf-8-sig');newline='\r\n' if '\r\n' in content else '\n'
    content=content.replace('\r\n','\n');assert heading not in content,name
    first,rest=content.split('\n',1)
    link='../'+report if '/temp/' in name else report if '/mods/' in name else 'mods/WhereIsThing/'+report
    block='\n\n'.join([heading,summary,detail,'[实施与验收记录]('+link+')。仅同步本地 memory，无可用外部 update_memory 接口。'])
    content=first+'\n\n'+block+'\n\n'+rest.lstrip('\n')
    p.write_bytes((bytes([239,187,191]) if bom else b'')+content.replace('\n',newline).encode('utf-8'))
    assert heading in p.read_text(encoding='utf-8-sig').splitlines()[:4]
changed=[name for name in baseline if hashlib.sha256((ROOT/name).read_bytes()).hexdigest()!=baseline[name]]
source=ROOT/'MOD开发/WhereIsThing/WhereIsThing'
hashes={str(p.relative_to(ROOT)):hashlib.sha256(p.read_bytes()).hexdigest() for p in source.rglob('*') if p.is_file() and 'obj' not in p.parts and 'bin' not in p.parts}
result={
 'completed_at':datetime.now().astimezone().isoformat(),
 'development_version':'0.1.3-dev.20261116.fixes','assembly_version':'0.1.3.0',
 'helper_checks_passed':184,'production_flow_checks_passed':97,'total_logic_checks':281,
 'release_build':{'framework':'net4.7.2','game_baseline':'PEAK 3.1.0','warnings':0,'errors':0},
 'artifact':{'path':str(dll.relative_to(ROOT)),'bytes':dll.stat().st_size,'sha256':dll_hash},
 'changed_existing_files':changed,'unrelated_and_release_files_hash_verified':len(unrelated),
 'production_files_final_hashes':hashes,'memory_entries_updated':list(entries),
 'deployed':False,'in_game_validation':False,'performance_measured':False,
 'formal_release_changed':False,'committed':False,
 'deferred':'In-game acceptance; retry narrowing, shared scope caches, search reuse, save batching, object pooling and virtual lists require measurement or separate integration design.',
 'report':'memory/mods/WhereIsThing/'+report
}
(OUT/'final-summary.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({k:v for k,v in result.items() if k not in ['production_files_final_hashes','changed_existing_files']},ensure_ascii=True,indent=2))
