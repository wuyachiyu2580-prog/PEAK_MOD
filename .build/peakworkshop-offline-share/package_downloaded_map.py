import hashlib
import json
from pathlib import Path
import sys
import zipfile

sys.stdout.reconfigure(encoding='utf-8')
workspace = Path('C:/Users/Administrator/Desktop/MOD/PEAK')
source = Path('C:/Users/Administrator/AppData/Roaming/r2modmanPlus-local/PEAK/profiles/2.0.a/BepInEx/PeakWorkshop/maps/_net/asset_20261002_1516-3@db5f05439ea2')
output = workspace / '地图素材/PeakWorkshop离线分享/PeakWorkshop-FromTheMountainRanges-online-r13-20261031.zip'
prefix = 'BepInEx/PeakWorkshop/maps/_net/' + source.name + '/'
link = json.loads((source / '.forge-online.json').read_text(encoding='utf-8-sig'))
map_data = json.loads((source / 'map.json').read_text(encoding='utf-8-sig'))
assert link['source'] == 'download'
assert link['installed'] == link['pkg'] and len(link['pkg']) == 64
assert link['onlineRev'] == 13
files = sorted(p for p in source.rglob('*') if p.is_file())
assert len(files) == 13, 'Downloaded folder changed; inspect again'
assert not output.exists(), 'Do not overwrite an existing archive'

def digest(stream):
    sha = hashlib.sha256()
    for block in iter(lambda: stream.read(1024 * 1024), b''):
        sha.update(block)
    return sha.hexdigest()

records = []
for path in files:
    with path.open('rb') as stream:
        sha = digest(stream)
    records.append({'path': prefix + path.relative_to(source).as_posix(), 'bytes': path.stat().st_size, 'sha256': sha})

manifest = {
    'type': 'PeakWorkshop downloaded map offline copy',
    'prepared_date': '2026-10-31', 'timezone': 'Asia/Shanghai',
    'peakworkshop_version': '0.5.0',
    'title': map_data.get('title'), 'author': map_data.get('author'),
    'map_id': map_data['id'], 'map_json_revision': map_data['revision'],
    'online_id': link.get('onlineId') or link['id'], 'online_revision': link['onlineRev'],
    'cid': link['cid'], 'package': link['pkg'],
    'files': records, 'in_game_verified': False,
}
instructions = '''PeakWorkshop 已下载地图离线分享包

地图：自群山来 | From the Mountain Ranges
来源：已完成的在线下载，在线版本 13。
适配分析基线：PeakWorkshop 0.5.0。

安装方法
1. 提前安装 PEAK、BepInEx、PeakWorkshop 0.5.0 及其必需依赖，退出游戏。
2. 在 r2modman 中选择实际游玩的配置档，打开该配置档目录。
3. 解压本 ZIP，将里面的 BepInEx 文件夹合并到配置档根目录。
   最终应存在：
   BepInEx/PeakWorkshop/maps/_net/asset_20261002_1516-3@db5f05439ea2/map.json
   不要放到 BepInEx/plugins，也不要解成两层 BepInEx/BepInEx。
4. 若同名地图目录已存在，先备份该目录到地图根目录以外，再复制包内目录，避免新旧文件混合。
5. 启动游戏，从 PeakWorkshop 自己的入口查找/选择这张地图，或加入使用相同地图版本的房间。

本 ZIP 是手动安装包，不能只把 ZIP 放进 maps 等待自动安装。
如果 [Map] File 指向自定义地图根目录，则将包内 _net 下的地图目录放入实际地图根目录的 _net 中。
本包不包含游戏、MOD DLL、账号登录信息或个人配置。

说明
- 完整保留原下载目录的 13 个文件，包括隐藏的 .forge-online.json、声音及地图 cache。
- .forge-online.json 保存地图来源和已安装包版本，请保留；不要改地图内容或目录名。
- 与房主要求的内容和包版本一致时，代码会优先使用本地文件，跳过重新下载这张地图；工作坊列表仍可能访问网络。
- 如果房主或在线页面选择了更新版本，仍可能需要更新。多人联机仍需要 Steam/Photon 网络连接。
- 下载目录受 PeakWorkshop 缓存清理规则管理；保留 ZIP，地图被清理后可再次安装。
- 已核验压缩包内所有地图文件逐一 SHA-256 与源文件相同；尚未在接收方干净配置档或双机游戏中验收。

offline-map-manifest.json 记录文件路径、大小、SHA-256 和地图版本；位于压缩包根层，不属于地图内容。
地图内 revision=1001 是作者的 JSON 修订字段，online_revision=13 是在线包版本，二者本来就不相同。
'''
output.parent.mkdir(parents=True, exist_ok=True)
temporary = output.with_suffix('.zip.partial')
assert not temporary.exists(), 'Inspect existing partial archive first'
with zipfile.ZipFile(temporary, 'x', compression=zipfile.ZIP_DEFLATED, compresslevel=6) as archive:
    for path, record in zip(files, records):
        archive.write(path, record['path'])
    archive.writestr('INSTALL.txt', instructions.encode('utf-8-sig'))
    archive.writestr('offline-map-manifest.json', json.dumps(manifest, ensure_ascii=False, indent=2).encode('utf-8'))

with zipfile.ZipFile(temporary) as archive:
    assert set(archive.namelist()) == {r['path'] for r in records} | {'INSTALL.txt', 'offline-map-manifest.json'}
    for path, record in zip(files, records):
        with archive.open(record['path']) as stream:
            assert digest(stream) == record['sha256'], 'Archive content mismatch: ' + record['path']
        with path.open('rb') as stream:
            assert digest(stream) == record['sha256'], 'Source changed: ' + record['path']
assert sorted(p.relative_to(source).as_posix() for p in source.rglob('*') if p.is_file()) == sorted(p.relative_to(source).as_posix() for p in files)
temporary.rename(output)
with output.open('rb') as stream:
    archive_sha = digest(stream)
output.with_suffix('.zip.sha256').write_text(archive_sha + '  ' + output.name + chr(10), encoding='utf-8')
report = {'archive': str(output), 'archive_bytes': output.stat().st_size, 'source_bytes': sum(r['bytes'] for r in records), 'map_files_verified': len(records), 'entries': len(records) + 2, 'archive_sha256': archive_sha, 'online_revision': link['onlineRev'], 'source_unchanged': True, 'in_game_verified': False}
(workspace / '.build/peakworkshop-offline-share/verification.json').write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding='utf-8')
print(json.dumps(report, ensure_ascii=False, indent=2))
