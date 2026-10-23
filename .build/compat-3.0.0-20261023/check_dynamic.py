from audit import *
api=json.loads(read(OUT/'game-api.json'))
types={t['name']:t for t in api}
def lookup(name): return types.get(name) or next((t for t in api if t['name'].split('/')[-1].split('.')[-1]==name),None)
def members(name,kind):
    t=lookup(name); result=[]
    for _ in range(12):
        if not t: break
        result+=t[kind]; t=lookup(t['baseType']) if t['baseType'] else None
    return result
checks=[]
for typeName,kind,member in [('CharacterData','fields','isInvincible'),('ThornOnMe','fields','popOutTime'),('ScoutStatue','fields','hasAmulets'),('Rope','fields','attachedToAnchor'),('Item','fields','lastThrownCharacter'),('Spawner','methods','GetSpawnPool'),('CharacterHeatEmission','fields','character'),('Lantern','fields','lit'),('Lantern','fields','fuel'),('ZombieManager','fields','zombies')]:
    checks.append({'type':typeName,'kind':kind,'member':member,'found':any(x['name']==member for x in members(typeName,kind))})
monitor=read(ROOT/'MOD开发/WhySoLaggy/WhySoLaggy/Helpers/RpcMonitor.cs')
block=monitor.split('public static readonly HashSet<string> WatchedMethods',1)[1].split('};',1)[0]
watched=set(re.findall(r'"([A-Za-z_][A-Za-z_0-9]*)"',block))
rpc=set()
for p in NEW.rglob('*.cs'):
    rpc.update(re.findall(r'\[PunRPC\]\s+(?:public|private|internal|protected)\s+(?:static\s+)?[\w.<>\[\],]+\s+([A-Za-z_]\w*)\s*\(',read(p)))
check={'reflection':checks,'watched_rpc_count':len(watched),'game_rpc_count':len(rpc),'missing_rpc':sorted(watched-rpc)}
source_patches=[]
for project in projects():
    for path in project.parent.rglob('*.cs'):
        if any(x in path.parts for x in ('obj','bin')): continue
        for match in re.finditer(r'\[HarmonyPatch\(typeof\(([\w.]+)\),\s*"([^"]+)"',read(path)):
            t,n=match.groups(); typ=lookup(t)
            source_patches.append({'project':project.stem,'file':str(path.relative_to(ROOT)),'line':read(path)[:match.start()].count('\n')+1,'target':t+'.'+n,'status':'external' if typ is None else ('found' if any(m['name']==n for m in members(t,'methods')) else 'MISSING')})
        scoped=re.search(r'\[HarmonyPatch\(typeof\(([\w.]+)\)\)\]',read(path))
        if scoped:
            t=scoped.group(1)
            for match in re.finditer(r'\[HarmonyPatch\("([^"]+)"',read(path)):
                n=match.group(1);typ=lookup(t)
                source_patches.append({'project':project.stem,'file':str(path.relative_to(ROOT)),'line':read(path)[:match.start()].count('\n')+1,'target':t+'.'+n,'status':'external' if typ is None else ('found' if any(m['name']==n for m in members(t,'methods')) else 'MISSING')})
check['source_patch_count']=len(source_patches);check['source_patch_issues']=[p for p in source_patches if p['status']!='found'];dump('source-patches.json',source_patches)
dump('dynamic-checks.json',check)
print(json.dumps(check,ensure_ascii=False,indent=2))
meta=json.loads(read(OUT/'metadata-results.json'))
print(json.dumps([{'name':r.get('name'),'file':r['file'],'missing':r.get('missing'),'patch_issues':[p for p in r.get('patches',[]) if p['status']!='found']} for r in meta if r.get('missing') or any(p['status']!='found' for p in r.get('patches',[]))],ensure_ascii=False,indent=2))
