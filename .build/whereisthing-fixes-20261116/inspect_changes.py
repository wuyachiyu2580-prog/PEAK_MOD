from pathlib import Path
import json,hashlib,difflib
ROOT=Path(__file__).resolve().parents[2]
OUT=Path(__file__).parent
baseline=json.loads((OUT/'baseline.json').read_text(encoding='utf-8'))
changed=[]
for name,h in baseline.items():
    p=ROOT/name
    if hashlib.sha256(p.read_bytes()).hexdigest()!=h:
        if 'WhySoLaggy' in name or '发行' in name:raise AssertionError('Unrelated change: '+name)
        old=(OUT/'backup'/name).read_text(encoding='utf-8-sig').splitlines()
        new=p.read_text(encoding='utf-8-sig').splitlines()
        diff=list(difflib.unified_diff(old,new,fromfile=name+' (turn start)',tofile=name))
        changed.append({'file':name,'diff_lines':len(diff)})
        if diff:(OUT/('diff-'+p.name+'.txt')).write_text('\n'.join(diff),encoding='utf-8')
print(json.dumps({'changed_files':changed,'whysolaggy_and_release':'unchanged'},ensure_ascii=False,indent=2))
