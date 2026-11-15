import sys, struct, UnityPy
sys.stdout.reconfigure(encoding='utf-8')
env=UnityPy.load('C:/SteamLibrary/steamapps/common/PEAK/PEAK_Data/level4')
objects={o.path_id:o for o in env.objects if o.assets_file.name=='level4'}
def pos(pid):
    if pid not in objects: return None
    t=objects[pid].read(check_read=False)
    p=t.m_LocalPosition
    result=[p.x,p.y,p.z]
    if t.m_Father.m_PathID:
        parent=pos(t.m_Father.m_PathID)
        if parent: result=[a+b for a,b in zip(result,parent)]
    return [round(x,2) for x in result]
def go(pid):
    if pid not in objects: return None
    g=objects[pid].read(check_read=False)
    tid=g.m_Component[0].component.m_PathID
    return [g.m_Name,pos(tid)]
raw=objects[580041].get_raw_data(); off=44
n=struct.unpack_from('<i',raw,off)[0];off+=4
print('segments',n)
for i in range(n):
    biome=struct.unpack_from('<i',raw,off)[0];off+=4
    refs=[]
    for k in range(6):
        fid,pid=struct.unpack_from('<iq',raw,off);off+=12;refs.append(pid)
    variant=struct.unpack_from('<iii',raw,off);off+=12
    print(i,biome,[go(x) for x in refs[:2]],variant)
raw=objects[621120].get_raw_data();off=32
n=struct.unpack_from('<i',raw,off)[0];off+=4
print('progress',n)
for i in range(n):
    fid,pid=struct.unpack_from('<iq',raw,off);off+=12
    size=struct.unpack_from('<i',raw,off)[0];off+=4
    title=raw[off:off+size].decode();off=(off+size+3)//4*4
    off+=12
    ach,biome=struct.unpack_from('<ii',raw,off);off+=8
    print(title,biome,pos(pid))
