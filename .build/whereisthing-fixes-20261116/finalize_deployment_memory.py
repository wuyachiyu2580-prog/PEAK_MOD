from pathlib import Path
import hashlib
import json

root = Path(__file__).resolve().parents[2]
build = root / '.build/whereisthing-fixes-20261116'
deployment = json.loads((build / 'deployment-latest.json').read_text(encoding='utf-8-sig'))
for item in deployment['installed'] + deployment['configBefore']:
    assert hashlib.sha256(Path(item['Path']).read_bytes()).hexdigest().upper() == item['SHA256']

heading = '## 2026-11-17 WhereIsThing 已部署实测目录，实机待验'
report = root / 'memory/mods/WhereIsThing/IMPLEMENTATION_Fixes_2026-11-16.md'
destination = Path(deployment['destination']).as_posix()
backup = Path(deployment['backup'])
backup_text = backup.as_posix()
summary = (
    '按用户“放到实测目录”要求，2026-11-17 00:07:42 +08:00 已将 '
    '0.1.3-dev.20261116.fixes 的 DLL/PDB 部署到 2.0.a profile 的 BepInEx/plugins 根目录，'
    '复制前确认游戏未运行。旧子目录 DLL 已备份并移除，递归核对仅各一份 DLL/PDB；配置哈希不变。'
)
boundary = (
    '当前状态以本条为准；下方 2026-11-16 的“未部署”等描述保留为历史。'
    '184/184 helper 与 97/97 状态流程测试共 281 项逻辑检查通过，Release 构建 0 警告 0 错误，'
    '不代表实机通过。下一步启动 2.0.a profile 实测；尚未启动游戏、未实机验证、未发行或提交。'
)
details = [heading, '', summary, '', boundary, '',
    '- 安装目录：' + destination + '/',
    '- 备份目录：' + backup_text + '/',
    '- 旧版备份：previous/WUYACHIYU-WhereIsThing/WhereIsThing.dll；原子目录同名 DLL 已移除，其他文件保留。',
    '- DLL：189952 bytes；SHA256 ' + deployment['installed'][0]['SHA256'] + '。',
    '- PDB：58356 bytes；SHA256 ' + deployment['installed'][1]['SHA256'] + '。',
    '- 配置 com.wuyachiyu.WhereIsThing.cfg 未修改；SHA256 ' + deployment['configBefore'][0]['SHA256'] + '。',
    '- 部署证据：.build/whereisthing-fixes-20261116/deployment-latest.json 与 deployment-20261117-000742/deployment.json。',
    '- 开发标识 0.1.3-dev.20261116.fixes；插件版本 0.1.3；程序集版本 0.1.3.0。',
    '', '本次只同步本地 memory，未调用外部 update_memory。']

paths = [root / 'memory/mods/WhereIsThing' / (name + '.md') for name in ['README', 'STATUS', 'TODO', 'RECENT', 'DECISIONS', 'FILES']]
paths += [root / 'memory/mods/WhereIsThing/temp/current.md']
paths += [root / 'memory' / (name + '.md') for name in ['README', 'TODO', 'MEMORY_INDEX', 'CHANGELOG']]

def insert_after_title(path, lines):
    raw = path.read_bytes()
    raw.decode('utf-8-sig')
    if heading.encode('utf-8') in raw:
        return 'already-recorded'
    newline = b'\r\n' if b'\r\n' in raw else b'\n'
    offset = raw.index(b'\n') + 1
    before = backup / 'memory-before-finalization' / path.relative_to(root)
    before.parent.mkdir(parents=True, exist_ok=True)
    assert not before.exists(), str(before)
    before.write_bytes(raw)
    inserted = newline + newline.join(line.encode('utf-8') for line in lines) + newline
    updated = raw[:offset] + inserted + raw[offset:]
    path.write_bytes(updated)
    check = path.read_bytes()
    assert check == updated
    assert check[:offset] + check[offset + len(inserted):] == raw
    assert check.count(heading.encode('utf-8')) == 1
    return 'updated-and-verified'

results = [{'path': str(report.relative_to(root)), 'status': insert_after_title(report, details)}]
for path in paths:
    if path.parent.name == 'temp':
        link = '../IMPLEMENTATION_Fixes_2026-11-16.md'
    elif path.parent == root / 'memory':
        link = 'mods/WhereIsThing/IMPLEMENTATION_Fixes_2026-11-16.md'
    else:
        link = 'IMPLEMENTATION_Fixes_2026-11-16.md'
    lines = [heading, '', summary, '', boundary, '',
        '[实施与部署记录](' + link + ')。哈希、安装/备份路径详见该记录；仅同步本地 memory。']
    results.append({'path': str(path.relative_to(root)), 'status': insert_after_title(path, lines)})
print(json.dumps({'memoryFiles': len(results), 'historyBytesPreserved': True, 'results': results}, ensure_ascii=False, indent=2))
