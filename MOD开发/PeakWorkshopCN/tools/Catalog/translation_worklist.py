"""Stable translation review IDs and deterministic compilation into embedded JSON."""
import json
from pathlib import Path

BASE = Path(__file__).resolve().parents[2]
DATA = BASE / 'PeakWorkshopCN/Data'
REVIEW = BASE / 'audit/unified-implementation'
REVIEW.mkdir(parents=True, exist_ok=True)
work = REVIEW / 'translation-worklist.json'
if not work.exists():
    inventory = json.loads((BASE / 'PeakWorkshopCN/audit/2026-10-05-revision/template-inventory.json').read_text(encoding='utf-8'))
    known = {}
    for path in DATA.glob('Translations.*.json'):
        known.update(json.loads(path.read_text(encoding='utf-8-sig')))
    rows = {}
    for entry in inventory:
        if entry['En'] not in known:
            rows.setdefault(entry['En'], set()).add(entry['Source'])
    result = [{'id': i + 1, 'english': en, 'sources': sorted(rows[en])} for i, en in enumerate(sorted(rows))]
    work.write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding='utf-8')
rows = json.loads(work.read_text(encoding='utf-8'))
(REVIEW / 'translation-worklist.txt').write_text('\n'.join(f"{r['id']}\t{json.dumps(r['english'], ensure_ascii=False)}" for r in rows), encoding='utf-8')
reviewed = {}
for path in sorted(REVIEW.glob('translations-*.tsv')):
    for line in path.read_text(encoding='utf-8').splitlines():
        if not line or line.startswith('#'):
            continue
        index, text = line.split('\t', 1)
        index = int(index)
        if index in reviewed:
            raise ValueError(f'Duplicate review id: {index}')
        reviewed[index] = json.loads(text)
mapping = {r['english']: reviewed[r['id']] for r in rows if r['id'] in reviewed}
import re
for en, zh in mapping.items():
    if sorted(re.findall(r'(?<!\{)\{\d+\}(?!\})', en)) != sorted(re.findall(r'(?<!\{)\{\d+\}(?!\})', zh)):
        raise ValueError('Changed template parameters: ' + en)
if mapping:
    (DATA / 'Translations.Unified.json').write_text(json.dumps(mapping, ensure_ascii=False, indent=2), encoding='utf-8')
print(f'Translation review: {len(mapping)}/{len(rows)} entries')
