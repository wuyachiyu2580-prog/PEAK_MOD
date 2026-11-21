from pathlib import Path
import hashlib, json
from datetime import datetime

ROOT = Path(__file__).resolve().parents[2]
OUT = Path(__file__).parent
baseline = json.loads((OUT/'source-before.json').read_text(encoding='utf-8'))
prior = json.loads((ROOT/'.build/whereisthing-audit-20261116/source-before.json').read_text(encoding='utf-8'))
for name, expected in {**baseline, **prior}.items():
    assert hashlib.sha256((ROOT/name).read_bytes()).hexdigest() == expected, name
log=(OUT/'probe.log').read_text(encoding='utf-8-sig')
assert 'All 47 research candidate checks passed.' in log
assert log.count('PASS:') == 47
report='RESEARCH_Fixes_2026-11-16.md'
assert (ROOT/'memory/mods/WhereIsThing'/report).exists()
heading='## 2026-11-16 WhereIsThing 修复方案已调研，未实施'
summary='用户要求先调研如何修。确定共享按 Room 会话/actor/模式去重、目录就绪自动补发、无关/重复属性过滤、内容刷新与计时分离；保留协议 2、旧配置、默认客机限制和三秒房主过渡。隔离候选 47/47 检查通过，不能当作生产修复或实机通过。'
entries={
    'memory/mods/WhereIsThing/README.md':'当前接续入口为修复方案；上轮四类问题仍在生产代码中，未执行修复。',
    'memory/mods/WhereIsThing/STATUS.md':'生产 23 个 .cs 与项目文件共 24 个哈希未变；正式 Tests/cfg/安装/发行未改。此前 184 项回归沿用上轮结果，本轮未重跑。',
    'memory/mods/WhereIsThing/TODO.md':'待用户安排实施：先共享/目录/计时，再生命周期与内部残留，再低风险优化。缩小晚分类、段缓存、池化/虚拟列表需测量和实机；原晚安莓验收仍待完成。',
    'memory/mods/WhereIsThing/RECENT.md':'补查本地 Photon 提交 bool 与回显区别、首次目录懒加载、完整属性快照、房主宽限、配置与窗口循环。原型覆盖失败节流、同名新房、空库与自动加载、无关回调、宽限到期、扫描模式切换。',
    'memory/mods/WhereIsThing/DECISIONS.md':'自动同步/目录/换房只刷新内容不续时；显式用户操作保留原计时语义。主线程统一观察配置；只缓存已提交而非声称服务器确认。保留兼容，不引入新 RPC/心跳/强制房主策略。',
    'memory/mods/WhereIsThing/FILES.md':'新增修复方案报告；实验目录 .build/whereisthing-fix-research-20261116/ 含 prepare_probe.py、CandidateMethods.cs、ResearchScenarios.cs、Harness.cs、Probe.csproj、probe.log、source-before.json、research-summary.json。仅实验副本，不直接复制部署。',
    'memory/mods/WhereIsThing/temp/current.md':'停点：调研完成，47 项为实验副本而非生产修复。接续先读方案；正式实施须补主 Update/菜单/真实 Photon 集成。不要重跑旧实施脚本、直接部署或动 WhySoLaggy 改动。目录自动就绪与空库防迁移是本轮补充；有一 prefab 不证明分批注入完全结束。',
    'memory/README.md':'WhereIsThing 修复方案及隔离验证已保存；当前开发基线仍 0.1.3-dev.20261116.napberry。',
    'memory/TODO.md':'WhereIsThing 待办顺序已细化：正确性 → 生命周期/内部残留 → 低风险优化 → 实测后复杂优化。仅调研，问题尚未在生产代码修复。',
    'memory/MEMORY_INDEX.md':'新增 mods/WhereIsThing/RESEARCH_Fixes_2026-11-16.md：可执行修复设计、兼容边界、47 项隔离证据和正式验收清单。',
    'memory/CHANGELOG.md':'- [新增] mods/WhereIsThing/RESEARCH_Fixes_2026-11-16.md：修复方法与隔离候选验证。\n- [修改/索引] WhereIsThing 六件套/current 与根 README/TODO/MEMORY_INDEX：记录未实施状态与后续顺序。',
}
for relative, detail in entries.items():
    path=ROOT/relative
    raw=path.read_bytes(); bom=raw.startswith(bytes([239,187,191]))
    text=raw.decode('utf-8-sig'); newline='\r\n' if '\r\n' in text else '\n'
    text=text.replace('\r\n','\n')
    assert heading not in text, relative
    first,rest=text.split('\n',1)
    link='../'+report if '/temp/' in relative else report if '/mods/' in relative else 'mods/WhereIsThing/'+report
    block='\n\n'.join([heading,summary,detail,'[修复方案与验证边界]('+link+')。只同步本地 memory，无可用外部 update_memory 接口。'])
    updated=first+'\n\n'+block+'\n\n'+rest.lstrip('\n')
    path.write_bytes((bytes([239,187,191]) if bom else b'')+updated.replace('\n',newline).encode('utf-8'))
    assert heading in path.read_text(encoding='utf-8-sig').splitlines()[:4]
result={
    'completed_at':datetime.now().astimezone().isoformat(),
    'research_candidate_checks_passed':47,
    'production_files_verified':len(baseline),
    'prior_audit_cs_verified':len(prior),
    'production_files_changed':[],
    'formal_regression_status':'Prior audit: 184 passed; not rerun this turn',
    'production_fixes_applied':False,'in_game_validation':False,'deployed':False,
    'evidence_limit':'Experimental modified copies of extracted methods with game/network/config doubles; no formal plugin build or Unity/Photon integration validation.',
    'memory_entries_updated':list(entries),
    'report':'memory/mods/WhereIsThing/'+report
}
(OUT/'research-summary.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(result,ensure_ascii=False,indent=2))
