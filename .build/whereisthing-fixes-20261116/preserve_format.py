from pathlib import Path
import json
ROOT=Path(__file__).resolve().parents[2]
OUT=Path(__file__).parent
baseline=json.loads((OUT/'baseline.json').read_text(encoding='utf-8'))
restored=[];preserved=[]
for name in baseline:
    if not name.endswith('.cs') or 'WhereIsThing' not in name:continue
    path=ROOT/name;backup=OUT/'backup'/name
    old=backup.read_bytes();new=path.read_bytes()
    if old==new:continue
    old_text=old.decode('utf-8-sig').replace('\r\n','\n')
    new_text=new.decode('utf-8-sig').replace('\r\n','\n')
    if old_text==new_text:
        path.write_bytes(old);restored.append(name)
    else:
        newline='\r\n' if b'\r\n' in old else '\n'
        bom=bytes([239,187,191]) if old.startswith(bytes([239,187,191])) else b''
        path.write_bytes(bom+new_text.replace('\n',newline).encode('utf-8'));preserved.append(name)
print(json.dumps({'format_only_files_restored':len(restored),'edited_files_format_preserved':len(preserved)}))
