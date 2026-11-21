from pathlib import Path
import hashlib
import json
import subprocess

root = Path(__file__).resolve().parents[2]
report = 'IMPLEMENTATION_PresetsAndTraits_2026-11-16.md'
files = list((root / 'memory/mods/WhereIsThing').glob('*.md'))
files += [root / 'memory/mods/WhereIsThing/temp/current.md']
files += [root / 'memory' / name for name in ('README.md', 'TODO.md', 'MEMORY_INDEX.md', 'CHANGELOG.md')]
links = []
for path in files:
    for chunk in path.read_text(encoding='utf-8-sig').split('](')[1:]:
        target = chunk.split(')', 1)[0]
        if report in target:
            links.append((str(path.relative_to(root)), (path.parent / target).is_file()))
assert len(links) == 10 and all(exists for _, exists in links), links
assert report in (root / 'memory/CHANGELOG.md').read_text(encoding='utf-8-sig')
audit = json.loads((Path(__file__).parent / 'asset-evidence.json').read_text(encoding='utf-8'))
assert audit['item_asset_count'] == 182
assert len(audit['secret_identity_items']) == 1
berries = [item for item in audit['items'] if item['prefab'].startswith('Shroomberry_')]
assert len(berries) == 5
assert all(any(action['class'] == 'Action_ModifyStatus' and action['enabled'] and action['on_consumed']
               and action['status'] == 10 and abs(action['amount'] - 0.05) < 0.000001
               for action in item['actions']) for item in berries)
check = subprocess.run(['git', 'diff', '--check', '--', 'MOD开发/WhereIsThing', 'memory/mods/WhereIsThing',
                        'memory/README.md', 'memory/TODO.md', 'memory/MEMORY_INDEX.md', 'memory/CHANGELOG.md'],
                       cwd=root, capture_output=True, text=True, encoding='utf-8', errors='replace')
assert check.returncode == 0, (check.stdout + check.stderr)[:3000]
identity = json.loads((Path(__file__).parent / 'build-identity.json').read_text(encoding='utf-8-sig'))
assert hashlib.sha256((Path(__file__).parent / 'bin/WhereIsThing.dll').read_bytes()).hexdigest().upper() == identity['SHA256']
summary = {'memory_links': len(links), 'all_links_valid': True, 'asset_count': 182,
           'five_shroomberries_have_spores': True, 'diff_check_exit': check.returncode,
           'isolated_artifact_hash_matches': True, 'deployed': False, 'in_game_tested': False}
(Path(__file__).parent / 'final-verification.json').write_text(json.dumps(summary, indent=2), encoding='utf-8')
print(json.dumps(summary))
