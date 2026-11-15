import sys, json, UnityPy
sys.stdout.reconfigure(encoding='utf-8')
env = UnityPy.load('C:/SteamLibrary/steamapps/common/PEAK/PEAK_Data/level4')
names = {}
for obj in env.objects:
    if obj.type.name != 'MonoBehaviour': continue
    try:
        data = obj.read(check_read=False)
        script = data.m_Script.read()
        name = script.m_ClassName
        if name not in ('MapHandler', 'MountainProgressHandler'): continue
        raw = obj.get_raw_data()
        print(name, obj.path_id, len(raw), raw[:140].hex(), flush=True)
        names[name] = obj.path_id
    except Exception as error:
        names[type(error).__name__] = names.get(type(error).__name__, 0) + 1
print('read_failures', names, flush=True)
