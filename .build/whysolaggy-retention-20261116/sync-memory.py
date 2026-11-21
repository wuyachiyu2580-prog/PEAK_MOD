from pathlib import Path

root = Path(__file__).resolve().parents[2]
memory = root / 'memory'
nl = chr(10)
title = '## 2026-11-16 录制保留修复已部署，实机待验'
body = ('已完成上次实机反馈后中断的录制保留修复：帧/关键事件/明细分区，方法按秒汇总并保留有限尖峰；'
        '导出显示真实覆盖时间及分类省略。78/78回归通过，Release零警告零错误。'
        '构建1.0.5-dev.20261116.retention（插件仍1.0.5）于00:19安装2.0.a/plugins根目录，'
        '仅一份DLL/PDB，旧版可回退，用户配置哈希未变。发行未改、未上传/提交。')
detail = {
    'README.md': '下一步：在同一profile实测新的30/60秒录制，核对触发前时间线和省略统计。',
    'STATUS.md': '当前已安装修复版；旧日志没有LOC: 0，但新版游戏表现尚未验收。不要重复部署11月15日旧DLL。',
    'TODO.md': nl.join(['- [x] 完成保留修复、压缩/导出回归、单份部署及回退记录。', '- [ ] 新版实机检查预录20秒、30/60秒采集时间线与分类省略。', '- [ ] 原五模式A/B、30分钟四人/重连及完整三语言界面矩阵继续待验。']),
    'RECENT.md': '后来旧构建实测再次确认60.01秒录制只剩3.369秒证据；本轮压测200FPS、30方法、20秒预录+60秒采集保住全部帧与方法总计。',
    'DECISIONS.md': '帧与关键状态不能被方法洪泛挤占；正常时间窗过期不算容量丢失。尖峰明细是窗口总计子集，不重复相加；采样耗时不外推。载荷有界不等于实机无开销。',
    'FILES.md': '新增CaptureBuffer.cs、CaptureTiming.cs、CaptureTimingWindow.cs及CaptureRetentionTests.cs。证据/产物/回退均在.build/whysolaggy-retention-20261116/；DLL SHA256：97F1320DE575B44034F251F6FBFC2A1A2250BC4BF736C0E89FA2EFDB80AA328E。',
}
def prepend(path, parts):
    text = path.read_text(encoding='utf-8-sig')
    if title in text:
        raise RuntimeError('Already synchronized: ' + str(path))
    first, rest = text.split(nl, 1)
    path.write_text(first + nl*2 + (nl*2).join(parts) + nl*2 + rest.lstrip(nl), encoding='utf-8')

for name, extra in detail.items():
    prepend(memory / 'mods/WhySoLaggy' / name, [title, body, extra, '[实施、证据及回退](IMPLEMENTATION_Retention_2026-11-16.md)。'])
for name in ['README.md', 'TODO.md', 'MEMORY_INDEX.md', 'CHANGELOG.md']:
    parts = [title, body, '[WhySoLaggy修复及下一步](mods/WhySoLaggy/IMPLEMENTATION_Retention_2026-11-16.md)。实机性能与联机矩阵仍未完成。']
    if name == 'CHANGELOG.md':
        parts.append(nl.join(['- [新增] mods/WhySoLaggy/IMPLEMENTATION_Retention_2026-11-16.md：恢复中断修复、最新旧运行审计、78项测试、安装和回退。', '- [修改/索引] WhySoLaggy六件套/current与根README/TODO/MEMORY_INDEX同步本次交付，历史记录保留。']))
    prepend(memory / name, parts)
current = memory / 'mods/WhySoLaggy/temp/current.md'
text = current.read_text(encoding='utf-8')
start = text.index('## 2026-11-16 恢复中断')
end = text.index('## 2026-11-15', start)
parts = [title, body, '下一步：用户运行新构建后读取新日志/ZIP，核对预录20秒及30/60秒时间线；原实机矩阵继续待验。此次没有启动游戏。', '[修复/审计/部署/回退](../IMPLEMENTATION_Retention_2026-11-16.md)。旧对话最后进度已接完，本地未提交改动是本次与上次保留修复。']
current.write_text(text[:start] + (nl*2).join(parts) + nl*2 + text[end:], encoding='utf-8')
print('Synchronized 6 project files, 4 root files, and current.md')
