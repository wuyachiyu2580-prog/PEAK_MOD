"""Offline numerical skinning stress check. Not a substitute for Unity playtesting."""
import json, math
import build_model as b

def translation(v):return [1,0,0,v[0],0,1,0,v[1],0,0,1,v[2],0,0,0,1]
def rotate_at(p,axis,angle):
    a=math.radians(angle); c=math.cos(a);s=math.sin(a)
    if axis=='y':r=[c,0,s,0,0,1,0,0,-s,0,c,0,0,0,0,1]
    elif axis=='x':r=[1,0,0,0,0,c,-s,0,0,s,c,0,0,0,0,1]
    else:r=[c,-s,0,0,s,c,0,0,0,0,1,0,0,0,0,1]
    return b.mm(b.mm(translation(p),r),translation(b.mul(p,-1)))

meshes,textures=b.build()
identity=translation([0,0,0]); pose=[identity[:] for _ in b.BONES]
for side,sign in [('L',-1),('R',1)]:
    arm='Arm_'+side; elbow='Elbow_'+side; hand='Hand_'+side
    shoulder=rotate_at(b.pos(b.REST[arm]),'y',sign*58)
    bend=b.mm(shoulder,rotate_at(b.pos(b.REST[elbow]),'z',-sign*65))
    for name,transform in [(arm,shoulder),(elbow,bend),(hand,bend)]:
        pose[b.INDEX[name]]=transform
        offset=b.RIG['extra_offset'];extra='Extra'+name
        pose[b.INDEX[extra]]=b.mm(b.mm(translation(offset),transform),translation(b.mul(offset,-1)))
    leg='Leg_'+side; knee='Knee_'+side; foot='Foot_'+side
    upper=rotate_at(b.pos(b.REST[leg]),'x',sign*15)
    lower=b.mm(upper,rotate_at(b.pos(b.REST[knee]),'x',25))
    for name,transform in [(leg,upper),(knee,lower),(foot,lower)]:pose[b.INDEX[name]]=transform
    upperant='Antenna_'+side+'_Upper'
    pose[b.INDEX[upperant]]=rotate_at(b.pos(b.REST[upperant]),'y',sign*12)

maximum=0; bad=0; statistics=[]
for m in meshes:
    transformed=[]
    for v in m['vertices']:
        p=[0,0,0]
        for bi,w in v['w']:p=b.add(p,b.mul(b.point(pose[bi],v['p']),w))
        assert all(math.isfinite(x) for x in p)
        maximum=max(maximum,b.norm(p));transformed.append(p)
    # Inspect unexpectedly stretched edges compared with the actual rest mesh.
    ratios=[]
    for i in range(0,len(m['indices']),3):
        tri=m['indices'][i:i+3]
        for a,c in zip(tri,tri[1:]+tri[:1]):
            original=b.norm(b.sub(m['vertices'][a]['p'],m['vertices'][c]['p']))
            if original>0.03:ratios.append(b.norm(b.sub(transformed[a],transformed[c]))/original)
    statistics.append({'part':m['part'],'max_edge_stretch':max(ratios) if ratios else 0,'edges_over_4x':sum(r>4 for r in ratios)})
assert maximum<15
b.preview(meshes,textures,'pose-stress-preview.png',pose)
(b.OUT/'pose-validation.json').write_text(json.dumps({'finite':True,'maximum_distance':maximum,'parts':statistics,'scope':'Synthetic offline pose, not Unity animation or game validation'},indent=2))
print(json.dumps(statistics))
