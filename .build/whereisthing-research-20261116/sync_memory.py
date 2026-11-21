from pathlib import Path

root = Path(__file__).resolve().parents[2]
report = 'IMPLEMENTATION_PresetsAndTraits_2026-11-16.md'
summary = ('用户认可的改动已整合到开发源码：客机 RestrictClientPresets 默认 true（本地偏好），'
           '本地多预设并集、放置物门控、蘑菇毒/无毒品种及 Clone 识别、地面过滤与标签范围优化。'
           '143/143 逻辑回归，当前 PEAK 3.1.0 隔离 Release 0 警告 0 错误。'
           '开发标识 0.1.3-dev.20261116.presets；未部署、未实机、未改发行。')
details = {
 'README.md': '毒浆果、蘑莓本局效果、烹饪风险、次数/燃料和同名蛋仅为候选，尚未实施。',
 'STATUS.md': '曼德拉草/药用根茎、陷阱箱识别已经存在；不能把额外研究当成功能已实现。安装未由本轮更新。',
 'TODO.md': '下一步实机：默认受限/关闭/再开启、入退房/换房主、旧 UI 回调、多选持久化、六种蘑菇中文显示、距离边界/绳索/点燃信号棒。额外候选未获进一步实施要求。',
 'RECENT.md': '新增72项逻辑检查；182个 resources Item/Backpack 资产中35个相关条目保留证据，不是可获取物品总数。',
 'DECISIONS.md': '最新用户认可整合，覆盖同日只比较的限制。客机开关不是房主强制规则；共享协议保持。无毒后缀明确为品种，实例烹饪风险另议。保留网络发现，按实际标签位置过滤，避免根位置导致绳索漏标。',
 'FILES.md': '新 helper：ThingPresetSelection.cs、ThingItemTraits.cs、ThingLabelRange.cs；回归 PresetAndTraitTests.cs。证据/构建日志/复核脚本位于 .build/whereisthing-research-20261116/。隔离DLL为 bin/WhereIsThing.dll，185344 bytes，SHA256 D9456F7E5B72CF2EFCA9B7BA190F1358A4FB330021B81BBE1A7B4F52C4CE8B34。构建覆盖 OutputPath，命令见报告。',
 'temp/current.md': '本轮完成，不重跑中间迁移脚本 implement.py，最终源码还经过定点补丁。限制实际状态变化关闭编辑器，修改回调加守卫。asset-evidence.json 已核验182资产/六种蘑菇/五色蘑莓。接续按用户后续安排做实机或候选功能。'
}
blocks = {}
for name, detail in details.items():
    link = ('../' if name.startswith('temp/') else '') + report
    blocks['memory/mods/WhereIsThing/' + name] = summary + '\n\n' + detail + '\n\n[实施与额外能力调研](' + link + ')。此条覆盖下方只对比、未授权移植状态；旧记录保留为历史。'
link = 'mods/WhereIsThing/' + report
blocks['memory/README.md'] = summary + '\n\n[WhereIsThing实施与研究](' + link + ')。'
blocks['memory/TODO.md'] = summary + '\n\n待实机主客机切换、多选/旧配置、蘑菇/距离显示；额外候选只调研。下方未安排移植为历史。[详情](' + link + ')。'
blocks['memory/MEMORY_INDEX.md'] = '- [WhereIsThing整合及识别能力调研](' + link + ')：默认客机限制、143项回归、隔离构建、额外候选证据。\n- 六件套/current已同步；外部update_memory接口不可用，仅维护本地memory。'
blocks['memory/CHANGELOG.md'] = '- [新增] ' + link + '：用户确认后的源码整合、默认客机限制、143项验证及3.1.0资源调研。\n- [修改/索引] WhereIsThing六件套/current与根README/TODO/MEMORY_INDEX同步；隔离构建0/0，未部署/实机/发行，仅本地memory。'
heading = '## 2026-11-16 客机配置与改版整合完成，额外识别仅调研'
for relative, body in blocks.items():
    path = root / relative
    original = path.read_text(encoding='utf-8-sig')
    assert heading not in original, 'already updated: ' + relative
    first, separator, rest = original.partition('\n')
    path.write_text(first + '\n\n' + heading + '\n\n' + body + '\n\n' + rest.lstrip('\n'), encoding='utf-8')
    print(relative)
