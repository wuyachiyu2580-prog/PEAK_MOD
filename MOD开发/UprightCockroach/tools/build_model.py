"""Convert this validated FBX to a versioned, textured Scout-skinned model.
No FBX/Unity runtime dependency. Original FBX is never written.
Landmarks are an initial manual rig, not an automatic rig quality guarantee.
"""
import hashlib, json, math, pathlib, struct, shutil
from inspect_fbx import Fbx, child, value, properties
from PIL import Image, ImageDraw

BASE=pathlib.Path(__file__).resolve().parents[1]
OUT=BASE/'model-import'
RIG=json.loads((OUT/'rig-landmarks.json').read_text())
REF=json.loads((BASE/'scout-reference.json').read_text())
SCALE=RIG['source_scale']
SOURCE=BASE/'3d66.com_JDH5455253615.fbx'
EXPECTED='8d8f6567677e0c55f1180a92eea78ec2c598e5b7fcccc86586c645650f36c72e'

def add(a,b): return [x+y for x,y in zip(a,b)]
def sub(a,b): return [x-y for x,y in zip(a,b)]
def mul(a,s): return [x*s for x in a]
def dot(a,b): return sum(x*y for x,y in zip(a,b))
def cross(a,b): return [a[1]*b[2]-a[2]*b[1],a[2]*b[0]-a[0]*b[2],a[0]*b[1]-a[1]*b[0]]
def norm(a): return math.sqrt(dot(a,a))
def unit(a): return mul(a,1/max(norm(a),1e-10))
def mix(a,b,t): return add(mul(a,1-t),mul(b,t))
def clamp(t): return max(0,min(1,t))
def inverse(m):
    a=[list(m[i*4:i*4+4])+[float(i==j) for j in range(4)] for i in range(4)]
    for i in range(4):
        k=max(range(i,4),key=lambda k:abs(a[k][i])); a[i],a[k]=a[k],a[i]
        a[i]=[x/a[i][i] for x in a[i]]
        for j in range(4):
            if j!=i:
                d=a[j][i]; a[j]=[x-d*y for x,y in zip(a[j],a[i])]
    return [v for row in a for v in row[4:]]
def point(m,p): return [sum(m[r*4+c]*p[c] for c in range(3))+m[r*4+3] for r in range(3)]
def mm(a,b): return [sum(a[r*4+k]*b[k*4+c] for k in range(4)) for r in range(4) for c in range(4)]
def pos(m): return [m[i] for i in (3,7,11)]
def translated(m,delta):
    m=m[:]
    for i,v in zip((3,7,11),delta):m[i]+=v
    return m
def frame(p,axis):
    y=unit(axis); x=unit(cross(y,[0,1,0] if abs(y[1])<0.9 else [1,0,0])); z=cross(x,y)
    return [x[0],y[0],z[0],p[0],x[1],y[1],z[1],p[1],x[2],y[2],z[2],p[2],0,0,0,1]
def rotate(v,a,b):
    a=unit(a); b=unit(b); c=max(-1,min(1,dot(a,b))); k=cross(a,b)
    if c < -0.99999:
        axis=unit(cross(a,[1,0,0] if abs(a[0])<0.9 else [0,1,0])); return sub(mul(axis,2*dot(axis,v)),v)
    return add(add(v,cross(k,v)),mul(cross(k,cross(k,v)),1/(1+c)))

REST={n:inverse(m) for n,m in zip(REF['bones'],REF['bindposes'])}
BONES=[]
INDEX={}
def bone(name,driver,rest,parent=''):
    INDEX[name]=len(BONES); REST[name]=rest
    BONES.append({'name':name,'driver':driver,'parent':parent,'rest':rest,'bindpose':inverse(rest)})
for n in ['Hip','Mid','Torso','Head','Arm_L','Elbow_L','Hand_L','Arm_R','Elbow_R','Hand_R','Leg_L','Knee_L','Foot_L','Leg_R','Knee_R','Foot_R']:
    bone(n,n,REST[n])
for side in ['L','R']:
    for part,source in [('ExtraArm','Arm'),('ExtraElbow','Elbow'),('ExtraHand','Hand')]:
        n=part+'_'+side; d=source+'_'+side; bone(n,d,translated(REST[d],RIG['extra_offset']))

SPINE=RIG['spine']
def body_position(p):
    if p[2]>=SPINE[-1]['point'][2]:
        a=SPINE[-1]; return add(pos(REST[a['bone']]),mul(sub(p,a['point']),SCALE))
    for a,b in zip(SPINE,SPINE[1:]):
        if p[2] < b['point'][2]:
            t=(p[2]-a['point'][2])/(b['point'][2]-a['point'][2])
            if t<0:return add(pos(REST[a['bone']]),mul(sub(p,a['point']),SCALE))
            return add(mix(pos(REST[a['bone']]),pos(REST[b['bone']]),t),mul(sub(p,mix(a['point'],b['point'],t)),SCALE))
    raise AssertionError()

for ant in RIG['antennae']:
    pts=[body_position(p) for p in ant['points']]
    for i in range(2):
        n=ant['name']+('_Lower' if i==0 else '_Upper')
        parent='Head' if i==0 else ant['name']+'_Lower'
        bone(n,'',frame(pts[i],sub(pts[i+1],pts[i])),parent)

def weights(names,values):
    pairs=[(INDEX[n],w) for n,w in zip(names,values) if w>1e-6]
    s=sum(w for _,w in pairs)
    return [(n,w/s) for n,w in pairs]

def limb_vertex(p,limb):
    pts=limb['points']+[limb['tip']]
    lengths=[norm(sub(b,a)) for a,b in zip(pts,pts[1:])]
    distances=[]
    for i,(a,b) in enumerate(zip(pts,pts[1:])):
        delta=sub(b,a); t=clamp(dot(sub(p,a),delta)/dot(delta,delta)); q=mix(a,b,t)
        distances.append((dot(sub(p,q),sub(p,q)),i,t,q))
    _,i,t,q=min(distances)
    j=limb['joint_index']; names=limb['bones']; targets=[pos(REST[n]) for n in names]
    # Keep the shoulder/hip socket at the torso surface. The anatomical source
    # shoulder is not at the Scout joint and must not be moved away from it.
    targets[0] = body_position(pts[0])
    tipdir=unit(sub(targets[2],targets[1])); targets.append(add(targets[2],mul(tipdir,lengths[-1]*SCALE)))
    if i<j:
        along=(sum(lengths[:i])+t*lengths[i])/sum(lengths[:j]); a,b=targets[0:2]; idx=0
    else:
        along=t; idx=i-j+1; a,b=targets[idx:idx+2]
    # Preserve each insect segment's cross-section while straightening its chain.
    transformed=add(mix(a,b,along),mul(rotate(sub(p,q),sub(pts[i+1],pts[i]),sub(b,a)),SCALE))
    if idx==0:
        blend=clamp((along-0.85)/0.3); w=weights(names[:2],[1-blend,blend])
    elif idx==1:
        if along<0.15:
            blend=0.5+along/0.3; w=weights(names[:2],[1-blend,blend])
        else:
            blend=clamp((along-0.87)/0.26); w=weights(names[1:],[1-blend,blend])
    else:
        blend=clamp(0.5+along/0.35); w=weights(names[1:],[1-blend,blend])
    if idx==0 and along<0.28:
        anchor=INDEX['Torso' if 'Arm' in limb['name'] else 'Hip']
        blend=clamp(along/0.28)
        w=[(anchor,1-blend)]+[(bi,weight*blend) for bi,weight in w]
    return transformed,w

def transform_vertex(p,number):
    for limb in RIG['limbs']:
        if number in limb['parts']:return limb_vertex(p,limb)
    for ant in RIG['antennae']:
        if ant['part']==number:
            a,b,c=ant['points']; t=clamp((p[2]-b[2]+4)/8)
            return body_position(p),weights([ant['name']+'_Lower',ant['name']+'_Upper'],[1-t,t])
    if number in (3,4,5,6,7,8,14):return add(pos(REST['Head']),mul(sub(p,SPINE[-1]['point']),SCALE)),weights(['Head'],[1])
    target=body_position(p)
    if number in (11,12,13,24):return target,weights(['Torso'],[1])
    # Body has no joints: blend it smoothly across spine and neck.
    z=p[2]
    if z<66:return target,weights(['Hip'],[1])
    if z<86:return target,weights(['Hip','Mid'],[1-(z-66)/20,(z-66)/20])
    if z<104:return target,weights(['Mid','Torso'],[1-(z-86)/18,(z-86)/18])
    t=clamp((z-104)/11); return target,weights(['Torso','Head'],[1-t,t])

def build():
    assert hashlib.sha256(SOURCE.read_bytes()).hexdigest()==EXPECTED
    fbx=Fbx(SOURCE); root={'children':fbx.nodes}; objects=child(root,'Objects')['children']
    con=[n['props'] for n in child(root,'Connections')['children'] if n['name']=='C']
    models={n['props'][0]:n for n in objects if n['name']=='Model'}
    mats={n['props'][0]:n for n in objects if n['name']=='Material'}
    videos={n['props'][0]:value(n,'Content') for n in objects if n['name']=='Video'}
    tex={c[2]:videos[c[1]] for c in con if c[0]=='OO' and c[1] in videos}
    diffuse={c[2]:tex[c[1]] for c in con if c[0]=='OP' and len(c)>3 and c[3]=='DiffuseColor' and c[1] in tex}
    material_for={c[2]:c[1] for c in con if c[0]=='OO' and c[1] in mats}
    model_for={c[1]:c[2] for c in con if c[0]=='OO' and c[2] in models}
    transforms=[properties(n) for n in models.values() if n['props'][2]=='Mesh']
    assert all(t==transforms[0] or all(t.get(k)==transforms[0].get(k) for k in ['PreRotation','Lcl Translation','Lcl Rotation','Lcl Scaling','GeometricTranslation']) for t in transforms)
    meshes=[]; textures=[]; mat_index={}; previews=[]; source_tris=0
    for number,n in enumerate([n for n in objects if n['name']=='Geometry' and n['props'][2]=='Mesh'],1):
        inds=value(n,'PolygonVertexIndex'); source_tris+=len(inds)//3
        if number in RIG['exclude_parts']:continue
        mid=material_for[model_for[n['props'][0]]]
        if mid not in mat_index:
            assert mid in diffuse,'Missing diffuse texture'
            mat_index[mid]=len(textures); textures.append(diffuse[mid])
        verts=value(n,'Vertices'); uv_node=child(n,'LayerElementUV'); ns=child(n,'LayerElementNormal')
        assert value(uv_node,'MappingInformationType')=='ByPolygonVertex' and value(uv_node,'ReferenceInformationType')=='IndexToDirect'
        assert value(ns,'MappingInformationType')=='ByPolygonVertex' and value(ns,'ReferenceInformationType')=='Direct'
        assert value(child(n,'LayerElementMaterial'),'MappingInformationType')=='AllSame'
        uvs=value(uv_node,'UV'); ui=value(uv_node,'UVIndex'); normals=value(ns,'Normals')
        positions=[list(verts[i:i+3]) for i in range(0,len(verts),3)]
        moved=[transform_vertex(p,number) for p in positions]
        lookup={}; outverts=[]; outindices=[]; smooth={}
        for i,idx in enumerate(inds):
            assert (idx<0)==(i%3==2),'Only triangulated source supported'
            vi=-idx-1 if idx<0 else idx; uv=(uvs[ui[i]*2],uvs[ui[i]*2+1]); normal=tuple(round(v,5) for v in normals[i*3:i*3+3])
            key=(vi,uv,normal)
            if key not in lookup:
                lookup[key]=len(outverts); outverts.append({'p':moved[vi][0],'w':moved[vi][1],'uv':uv,'smooth':(tuple(positions[vi]),normal),'n':[0,0,0]})
            outindices.append(lookup[key])
        # Recompute area-weighted normals after rest-pose transformation, keeping UV seams smooth.
        for i in range(0,len(outindices),3):
            tri=[outverts[k] for k in outindices[i:i+3]]; a,b,c=[v['p'] for v in tri]
            fn=cross(sub(b,a),sub(c,a))
            for v in tri:smooth[v['smooth']]=add(smooth.get(v['smooth'],[0,0,0]),fn)
        for v in outverts:v['n']=unit(smooth[v['smooth']])
        # Keep the visual groups separate so the client can show a small head,
        # wing cases and a shortened antenna chain in first person without
        # enabling every leg/body mesh in front of the camera.
        head_parts={3,4,5,6,7,8}
        shell_parts={11,12,13,14,24}
        category=2 if number in (1,2) else 3 if number in head_parts else 4 if number in shell_parts else 1 if number == 15 or any(number in l['parts'] and 'Arm' in l['name'] for l in RIG['limbs']) else 0
        name=('Roach_Antenna_' if category==2 else 'Roach_Head_' if category==3 else 'Roach_Shell_' if category==4 else 'Roach_Arm_' if category==1 else 'Roach_Body_')+str(number).zfill(2)
        mesh={'name':name,'part':number,'geometry_id':n['props'][0],'category':category,'material':mat_index[mid],'vertices':outverts,'indices':outindices}
        meshes.append(mesh)
    package=OUT/'uprightcockroach.roach'; package.parent.mkdir(parents=True,exist_ok=True)
    with package.open('wb') as f:
        def i(v):f.write(struct.pack('<i',v))
        def floats(v):f.write(struct.pack('<'+'f'*len(v),*v))
        def string(v):b=v.encode(); i(len(b)); f.write(b)
        f.write(b'URCH0001'); i(len(BONES))
        for b in BONES:
            string(b['name']);string(b['driver']);string(b['parent']);floats(b['rest']);floats(b['bindpose'])
        i(len(textures))
        for t in textures:i(len(t));f.write(t)
        i(len(meshes))
        for m in meshes:
            string(m['name']);i(m['category']);i(m['material']);i(len(m['vertices']));i(len(m['indices']))
            for v in m['vertices']:
                floats(v['p']+v['n']+list(v['uv'])); w=v['w']+[(0,0)]*(4-len(v['w']))
                for bi,weight in w:i(bi);floats([weight])
            for idx in m['indices']:i(idx)
    manifest={'format':'URCH0001','source_sha256':EXPECTED,'package_sha256':hashlib.sha256(package.read_bytes()).hexdigest(),'bytes':package.stat().st_size,'excluded_parts':RIG['exclude_parts'],'source_triangles':source_tris,'triangles':sum(len(m['indices'])//3 for m in meshes),'meshes':[{k:v for k,v in m.items() if k not in ('vertices','indices')}|{'vertices':len(m['vertices']),'triangles':len(m['indices'])//3} for m in meshes],'texture_count':len(textures),'bones':BONES,'rig_quality':'Initial landmark-based binding. Requires in-game pose review.'}
    (OUT/'rigged-manifest.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8')
    preview(meshes,textures,'rigged-preview.png')
    validate(meshes)
    print(json.dumps({k:manifest[k] for k in ['bytes','triangles','texture_count','package_sha256']}))
    return meshes,textures

def preview(meshes,textures,filename,pose=None):
    import io
    images=[Image.open(io.BytesIO(t)).convert('RGB') for t in textures]; faces=[]
    for m in meshes:
        tex=images[m['material']]; verts=m['vertices']
        for i in range(0,len(m['indices']),3):
            tri=[verts[k] for k in m['indices'][i:i+3]]; uv=[sum(v['uv'][j] for v in tri)/3 for j in range(2)]
            color=tex.getpixel((min(tex.width-1,int(uv[0]%1*tex.width)),min(tex.height-1,int((1-uv[1]%1)*tex.height))))
            pts=[]
            for v in tri:
                p=v['p']
                if pose:
                    p=[0,0,0]
                    for bi,w in v['w']:p=add(p,mul(point(pose[bi],v['p']),w))
                pts.append(p)
            normal=unit(cross(sub(pts[1],pts[0]),sub(pts[2],pts[0]))); shade=0.6+0.4*abs(dot(normal,[-0.3,-0.6,0.74]))
            faces.append((pts,tuple(min(255,int(c*shade)) for c in color)))
    canvas=Image.new('RGB',(1800,900),(37,41,48))
    for view,angle in enumerate([0,math.pi/2,math.pi]):
        ca,sa=math.cos(angle),math.sin(angle); transformed=[]
        for pts,col in faces:
            q=[(x*ca+y*sa,z,-x*sa+y*ca) for x,y,z in pts];transformed.append((sum(p[2] for p in q)/3,q,col))
        pp=[p for _,q,_ in transformed for p in q]; xmin=min(p[0] for p in pp);xmax=max(p[0] for p in pp);ymin=min(p[1] for p in pp);ymax=max(p[1] for p in pp)
        scale=min(560/(xmax-xmin),820/(ymax-ymin));draw=ImageDraw.Draw(canvas)
        for _,q,col in sorted(transformed,key=lambda f:f[0],reverse=True):draw.polygon([(view*600+300+(p[0]-(xmin+xmax)/2)*scale,470-(p[1]-(ymin+ymax)/2)*scale) for p in q],fill=col)
        draw.text((view*600+15,15),['Scout bind pose / front','Side','Back'][view] if not pose else ['Pose stress / front','Side','Back'][view],fill='white')
    canvas.save(OUT/filename)

def validate(meshes):
    identity=[float(r==c) for r in range(4) for c in range(4)]
    assert len(meshes)==24
    assert all(m['part'] not in RIG['exclude_parts'] for m in meshes)
    for b in BONES:assert max(abs(a-c) for a,c in zip(mm(b['rest'],b['bindpose']),identity))<1e-5
    for m in meshes:
        assert len(m['indices'])%3==0
        for v in m['vertices']:
            assert all(math.isfinite(x) for x in v['p']+v['n']+list(v['uv']))
            assert abs(sum(w for _,w in v['w'])-1)<1e-6
            assert all(0<=bi<len(BONES) and 0<=w<=1 for bi,w in v['w'])
        assert min(m['indices'])>=0 and max(m['indices'])<len(m['vertices'])
    assert hashlib.sha256(SOURCE.read_bytes()).hexdigest()==EXPECTED

if __name__=='__main__': build()
