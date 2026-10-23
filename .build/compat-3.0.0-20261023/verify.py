from audit import *
before=json.loads(read(OUT/'before-hashes.json'));after=snapshot()
changes=[k for k in before if before[k]!=after.get(k)]
dump('preservation.json',{'checked_files':len(before),'changed':changes,'stage':'final after all builds/tests'})
updated=json.loads(read(OUT/'memory-updated-files.json'))
missing=[]
for rel in updated:
    p=ROOT/rel;t=read(p)
    assert '## 2026-10-23 PEAK 3.0.0 兼容性审核' in t,rel
    first=t.split('## 2026-10-23 PEAK 3.0.0 兼容性审核',1)[1].split('\n## ',1)[0]
    for link in re.findall(r'\]\(([^)]+)\)',first):
        if not (p.parent/link).exists():missing.append({'file':rel,'link':link})
roots=[ROOT/'引用参考代码/反编译/作废/2.6.b',ROOT/'引用参考代码/反编译/3.0.0']
targets=['PhotonUnityNetworking/Photon.Pun/PhotonNetwork.cs','PhotonUnityNetworking/Photon.Pun/PhotonView.cs','Assembly-CSharp/StaminaBar.cs','Assembly-CSharp/BarAffliction.cs','Assembly-CSharp/CustomizationRefs.cs','Assembly-CSharp/ScoutStatue.cs','Assembly-CSharp/Item.cs','Assembly-CSharp/Spawner.cs','Zorro.Settings.Runtime/Zorro.Settings.UI/EnumSettingUI.cs','Zorro.Settings.Runtime/Zorro.Settings/SettingInputUICell.cs']
comparison=[]
for rel in targets:
    paths=[r/rel for r in roots]
    comparison.append({'file':rel,'both_present':all(p.exists() for p in paths),'normalized_equal':norm(read(paths[0]))==norm(read(paths[1])) if all(p.exists() for p in paths) else None})
dump('additional-api-comparison.json',comparison)
result={'preserved_files':len(before),'changes':changes,'memory_files_checked':len(updated),'missing_new_links':missing,'api_comparison':comparison}
dump('final-verification.json',result)
print(json.dumps(result,ensure_ascii=False,indent=2))
assert not changes and not missing
