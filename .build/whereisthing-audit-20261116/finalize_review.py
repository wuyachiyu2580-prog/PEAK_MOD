from pathlib import Path
import hashlib
import json
from datetime import datetime

ROOT = Path(__file__).resolve().parents[2]
OUT = Path(__file__).parent
before = json.loads((OUT / 'source-before.json').read_text(encoding='utf-8'))
current = {name: hashlib.sha256((ROOT / name).read_bytes()).hexdigest() for name in before}
changed = [name for name in before if before[name] != current[name]]
assert not changed, changed

review = ROOT / 'memory/mods/WhereIsThing/REVIEW_2026-11-16.md'
review.write_text(review.read_text(encoding='utf-8').replace('](/C:/', '](C:/'), encoding='utf-8')
heading = '## 2026-11-16 WhereIsThing 整体审查完成，问题未修复'
entries = {
    'memory/mods/WhereIsThing/README.md': ('REVIEW_2026-11-16.md', '当前入口为整体审查；晚安莓开发实现仍保留，审查不代表新增修复已经完成。'),
    'memory/mods/WhereIsThing/STATUS.md': ('REVIEW_2026-11-16.md', '开发基线仍为 0.1.3-dev.20261116.napberry。源码哈希核验 23/23 未变，本轮未部署/改 cfg/实机/发行。'),
    'memory/mods/WhereIsThing/TODO.md': ('REVIEW_2026-11-16.md', '建议待办：R1/R2 共享发布 → R3/R4 配置与计时 → 清内部残留 → 测量后优化。下方历史“下一步仅实机”不再是完整待办；原晚安莓实机清单仍保留。本轮未执行这些修复。'),
    'memory/mods/WhereIsThing/DECISIONS.md': ('REVIEW_2026-11-16.md', '本轮只审查与记录，不自动删功能/迁移/协议字段。additionalFilter 为合成条件下的维护缺口，正常 RunSettings 关闭会禁用 GO，不将其写成已确认游戏残留 bug。'),
    'memory/mods/WhereIsThing/FILES.md': ('REVIEW_2026-11-16.md', '新增 REVIEW_2026-11-16.md；隔离证据位于 .build/whereisthing-audit-20261116/：prepare_audit.py、AuditHarness.cs、ExtractedMethods.cs、audit.log、regression.log、source-before.json、audit-summary.json。未新增插件构建/部署产物。'),
    'memory/mods/WhereIsThing/RECENT.md': ('REVIEW_2026-11-16.md', '已审主插件、发现/标签生命周期、目录身份与预设、UI、ModConfig 集成。主要缺口在跨流程状态；184 项 helper 回归通过不能覆盖这些缺口。'),
    'memory/mods/WhereIsThing/temp/current.md': ('../REVIEW_2026-11-16.md', '停点：审查已完成，未修生产代码。8 项审计观察=1 正常对照+6 异常观察（4 根因）+1 合成过滤器边界；不是 8 个实机 bug。后续按用户新指令处理修复，不重跑旧实施脚本、不部署、不动 WhySoLaggy 既有改动。'),
    'memory/README.md': ('mods/WhereIsThing/REVIEW_2026-11-16.md', '新增整体审查入口，近期晚安莓开发实现仍保留，未自动执行修复。'),
    'memory/TODO.md': ('mods/WhereIsThing/REVIEW_2026-11-16.md', 'WhereIsThing 新待办：先共享与计时，再清内部残留、测性能；原晚安莓实机验收继续待办。本轮仅审查，未实施/部署。'),
    'memory/MEMORY_INDEX.md': ('mods/WhereIsThing/REVIEW_2026-11-16.md', '索引新增 REVIEW_2026-11-16.md（问题、残留、性能候选、证据等级与后续验收）；同步 WhereIsThing 六件套/current 与全局入口。'),
    'memory/CHANGELOG.md': ('mods/WhereIsThing/REVIEW_2026-11-16.md', '- [新增] mods/WhereIsThing/REVIEW_2026-11-16.md：整体审查与隔离重现。\n- [修改/索引] WhereIsThing 六件套/current、根 README/TODO/MEMORY_INDEX：记录未修缺口及后续顺序。'),
}
summary = '确认四类状态流问题：相同 payload 跨房/模式漏发布；目录解析后未重新发布；无关房间属性续时并重建窗口；外部常驻→定时保留无穷截止。现有 184/184 回归仍通过，新增隔离审计使用真实方法体和测试替身，未做实机。生产源码未改，问题尚未修复。'
for relative, (link, detail) in entries.items():
    path = ROOT / relative
    raw = path.read_bytes()
    bom = raw.startswith(bytes([239, 187, 191]))
    content = raw.decode('utf-8-sig')
    newline = chr(13) + chr(10) if chr(13) + chr(10) in content else chr(10)
    normalized = content.replace(chr(13) + chr(10), chr(10))
    assert heading not in normalized, relative + ' already updated'
    first, rest = normalized.split(chr(10), 1)
    detail = detail.replace(chr(92) + chr(92) + 'n', chr(10)).replace(chr(92) + 'n', chr(10))
    block = chr(10).join([heading, '', summary, '', detail, '', '[完整审查报告](' + link + ')。仅同步本地 memory；本次没有可用的外部 update_memory 接口。', ''])
    updated = first + chr(10) * 2 + block + chr(10) + rest.lstrip(chr(10))
    path.write_bytes((bytes([239, 187, 191]) if bom else b'') + updated.replace(chr(10), newline).encode('utf-8'))

audit = (OUT / 'audit.log').read_text(encoding='utf-8-sig')
regression = (OUT / 'regression.log').read_text(encoding='utf-8-sig')
assert audit.count('CONFIRMED:') == 8
assert 'All 184 regression checks passed.' in regression
result = {
    'reviewed_at': datetime.now().astimezone().isoformat(),
    'production_cs_verified': len(before),
    'production_cs_changed_since_audit_start': changed,
    'existing_regression_checks_passed': 184,
    'extracted_production_methods': 23,
    'audit_observations': {'total': 8, 'normal_control': 1, 'functional_observations': 6, 'functional_root_causes': 4, 'synthetic_filter_boundary': 1},
    'evidence_limit': 'Extracted production methods and test doubles; not an in-game or real Photon networking test. Existing additionalFilter callers are parent-component/type checks; ordinary RunSettings shutdown disables the GO and is covered by the active check.',
    'production_fixes_applied': False,
    'in_game_validation': False,
    'deployed': False,
    'memory_entries_updated': list(entries),
    'report': str(review.relative_to(ROOT)),
}
(OUT / 'audit-summary.json').write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding='utf-8')
print(json.dumps(result, ensure_ascii=False, indent=2))
