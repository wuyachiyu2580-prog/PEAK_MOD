import json, pathlib, UnityPy
out=pathlib.Path(__file__).resolve().parents[1]/'model-import/game-shaders.json'
env=UnityPy.load('C:/SteamLibrary/steamapps/common/PEAK/PEAK_Data/data.unity3d')
result=[]
for obj in env.objects:
    if obj.type.name=='Shader':
        data=obj.read_typetree()
        parsed=data.get('m_ParsedForm',{})
        name=parsed.get('m_Name',data.get('m_Name',''))
        props=parsed.get('m_PropInfo',{}).get('m_Props',[])
        result.append({'name':name,'properties':[p.get('m_Name','') for p in props]})
out.write_text(json.dumps(result,indent=2),encoding='utf-8')
print(json.dumps([r for r in result if any(t in r['name'] for t in ['Standard','Unlit','Diffuse','Character','Lit'])],ensure_ascii=False))
