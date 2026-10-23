from pathlib import Path
import re, json, hashlib, subprocess, sys, time, xml.etree.ElementTree as ET
ROOT=Path(__file__).resolve().parents[2]
OUT=Path(__file__).resolve().parent
OLD=ROOT/'引用参考代码/反编译/作废/2.6.b/Assembly-CSharp'
NEW=ROOT/'引用参考代码/反编译/3.0.0/Assembly-CSharp'
def read(p): return p.read_text(encoding='utf-8-sig',errors='replace')
def dump(name,data): (OUT/name).write_text(json.dumps(data,ensure_ascii=False,indent=2),encoding='utf-8')
def norm(t):
    t=re.sub(r'(?m)^\s*//.*$', '', t)
    t=re.sub(r'0x[0-9A-Fa-f]+', '<hex>', t) if False else t
    return re.sub(r'\s+','',t)
def projects():
    return sorted(p for p in (ROOT/'MOD开发').rglob('*.csproj') if not any(x in p.parts for x in ('作废','obj','bin','tests','tools')) and not p.parent.name.endswith('.Tests'))
def snapshot():
    files={}
    for p in projects():
        for f in p.parent.rglob('*'):
            if f.suffix in ('.cs','.csproj') and not any(x in f.parts for x in ('obj','bin')): files[str(f)]=hashlib.sha256(f.read_bytes()).hexdigest()
    names={p.stem+'.dll' for p in projects()}
    profiles=Path('C:/Users/Administrator/AppData/Roaming/r2modmanPlus-local/PEAK/profiles')
    for base in [profiles/'2.0.a/BepInEx/plugins',profiles/'terrain/BepInEx/plugins',ROOT/'测试环境/BepInEx/plugins']:
        if base.exists():
            for f in base.rglob('*.dll'):
                if f.name in names: files[str(f)]=hashlib.sha256(f.read_bytes()).hexdigest()
    return files
def scan():
    old={str(p.relative_to(OLD)):p for p in OLD.rglob('*.cs')}
    new={str(p.relative_to(NEW)):p for p in NEW.rglob('*.cs')}
    changed=[k for k in new.keys()&old.keys() if norm(read(old[k]))!=norm(read(new[k]))]
    added=sorted(new.keys()-old.keys()); removed=sorted(old.keys()-new.keys())
    impact={}
    for p in projects():
        sources=[f for f in p.parent.rglob('*.cs') if not any(x in f.parts for x in ('obj','bin'))]
        text='\n'.join(read(f) for f in sources)
        tokens=set(re.findall(r'\b[A-Za-z_][A-Za-z_0-9]*\b',text))
        impact[p.stem]={'project':str(p.relative_to(ROOT)),'sources':len(sources),'changed_types':[k for k in sorted(changed) if Path(k).stem in tokens], 'removed_types':[k for k in removed if Path(k).stem in tokens]}
    data={'old_count':len(old),'new_count':len(new),'changed':sorted(changed),'added':added,'removed':removed,'impact':impact}
    dump('source-diff.json',data); dump('before-hashes.json',snapshot())
    print(json.dumps(data,ensure_ascii=False,indent=2))
def build():
    results=[]
    for p in projects():
        dest=OUT/'build'/p.stem; dest.mkdir(parents=True,exist_ok=True)
        log=OUT/(p.stem+'.build.log')
        cmd=['dotnet','build',str(p),'-c','Release','--nologo','-v:minimal','-p:OutputPath='+str(dest)+'/', '-p:AppendTargetFrameworkToOutputPath=false']
        start=time.time()
        try:
            proc=subprocess.run(cmd,cwd=str(ROOT),capture_output=True,encoding='utf-8',errors='replace',timeout=180)
            output=proc.stdout+proc.stderr; code=proc.returncode
        except subprocess.TimeoutExpired as e:
            output='TIMEOUT '+str(e); code=-1
        log.write_text(output,encoding='utf-8')
        errors=list(dict.fromkeys(line.strip() for line in output.splitlines() if re.search(r': (?:error|warning) [A-Z]+[0-9]+',line)))
        result={'project':p.stem,'exit':code,'seconds':round(time.time()-start,1),'diagnostics':errors,'log':str(log.relative_to(ROOT))}
        results.append(result); dump('build-results.json',results)
        print(json.dumps(result,ensure_ascii=False),flush=True)
    before=json.loads(read(OUT/'before-hashes.json')); after=snapshot()
    changes=[k for k in before if before[k]!=after.get(k)]
    dump('preservation.json',{'checked_files':len(before),'changed':changes})
    print(json.dumps({'preservation_checked':len(before),'changed':changes},ensure_ascii=False))
if __name__=='__main__':
    if len(sys.argv)>1 and sys.argv[1]=='build': build()
    else: scan()
