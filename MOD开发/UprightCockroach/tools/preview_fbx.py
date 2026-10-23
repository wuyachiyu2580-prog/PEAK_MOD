"""Diagnostic orthographic preview for this FBX; not a production FBX importer."""
import json
import math
import pathlib
import sys
from PIL import Image, ImageDraw
from inspect_fbx import Fbx, child, value

base = pathlib.Path(__file__).resolve().parents[1]
report = json.loads((base / 'model-import/fbx-inspection.json').read_text(encoding='utf-8'))
fbx = Fbx(pathlib.Path(report['input']))
objects = child({'children': fbx.nodes}, 'Objects')['children']
connections = report['connections']
models = {m['id']: m for m in report['models']}
materials = {m['id']: m for m in report['materials']}
textures = {t['id']: t for t in report['textures']}
images = {v['id']: Image.open(v['extracted']).convert('RGB') for v in report['embedded_images']}
texture_images = {c[2]:images[c[1]] for c in connections if c[0]=='OO' and c[1] in images}
diffuse = {c[2]:texture_images[c[1]] for c in connections if c[0]=='OP' and c[3]=='DiffuseColor' and c[1] in texture_images}
model_materials = {c[2]:c[1] for c in connections if c[0]=='OO' and c[1] in materials}
mesh_models = {c[1]:c[2] for c in connections if c[0]=='OO' and c[2] in models}

# This file has no hierarchy or varying local rotations. All mesh nodes have
# the same -90deg X pre-rotation and geometric translation. Use source Z-up
# coordinates centered for a diagnostic preview, preserving relative geometry.
transforms = [m['properties'] for m in models.values() if m['type']=='Mesh']
signature = lambda p: [p.get(k) for k in ('Lcl Translation','Lcl Rotation','Lcl Scaling','PreRotation','GeometricTranslation')]
assert all(signature(p)==signature(transforms[0]) for p in transforms), 'Requires full transform evaluator'
faces = []
mesh_list = []
for n in objects:
    if n['name']!='Geometry' or n['props'][2]!='Mesh':
        continue
    mesh_id = n['props'][0]
    number = len(mesh_list)+1
    v = value(n,'Vertices')
    vertices = list(zip(v[0::3],v[1::3],v[2::3]))
    indices = value(n,'PolygonVertexIndex')
    uv_layer = child(n,'LayerElementUV')
    assert value(uv_layer,'MappingInformationType')=='ByPolygonVertex'
    assert value(uv_layer,'ReferenceInformationType')=='IndexToDirect'
    uv = value(uv_layer,'UV')
    uv_index = value(uv_layer,'UVIndex')
    tex = diffuse.get(model_materials.get(mesh_models[mesh_id]))
    tris = []
    poly = []
    poly_uv = []
    for i, index in enumerate(indices):
        poly.append(vertices[-index-1 if index<0 else index])
        ui=uv_index[i]*2
        poly_uv.append((uv[ui],uv[ui+1]))
        if index<0:
            assert len(poly)==3
            u=sum(x[0] for x in poly_uv)/3
            vv=sum(x[1] for x in poly_uv)/3
            color=tex.getpixel((min(tex.width-1,int((u%1)*tex.width)),min(tex.height-1,int((1-vv%1)*tex.height)))) if tex else (160,110,70)
            a,b,c=poly
            ab=[b[k]-a[k] for k in range(3)]
            ac=[c[k]-a[k] for k in range(3)]
            normal=[ab[1]*ac[2]-ab[2]*ac[1],ab[2]*ac[0]-ab[0]*ac[2],ab[0]*ac[1]-ab[1]*ac[0]]
            length=math.sqrt(sum(x*x for x in normal)) or 1
            shade=0.55+0.45*abs(sum(normal[k]*(-0.3,-0.6,0.74)[k] for k in range(3))/length)
            color=tuple(min(255,int(x*shade)) for x in color)
            tris.append((list(poly),color,number))
            poly=[]
            poly_uv=[]
    # Exclude the 2-triangle floor from framing, not from the source file.
    if len(tris)>2:
        faces.extend(tris)
    mesh_list.append({'number':number,'id':mesh_id,'triangles':len(tris),'faces':tris})

def render(tris, angle, size, label, fixed_bounds=None):
    ca,sa=math.cos(angle),math.sin(angle)
    transformed=[]
    for tri,color,num in tris:
        points=[(x*ca+y*sa,z,-x*sa+y*ca) for x,y,z in tri]
        transformed.append((sum(p[2] for p in points)/3,points,color))
    allp=[p for _,pts,_ in transformed for p in pts]
    xmin,xmax=min(p[0] for p in allp),max(p[0] for p in allp)
    ymin,ymax=min(p[1] for p in allp),max(p[1] for p in allp)
    if fixed_bounds:
        xmin,xmax,ymin,ymax=fixed_bounds
    w,h=size
    scale=min((w-30)/(xmax-xmin or 1),(h-60)/(ymax-ymin or 1))
    image=Image.new('RGB',size,(37,41,48))
    draw=ImageDraw.Draw(image)
    for _,pts,col in sorted(transformed,key=lambda x:x[0],reverse=True):
        xy=[(w/2+(p[0]-(xmin+xmax)/2)*scale,h/2+12-(p[1]-(ymin+ymax)/2)*scale) for p in pts]
        draw.polygon(xy,fill=col)
    draw.text((12,10),label,fill='white')
    return image

out=base/'model-import'
canvas=Image.new('RGB',(1800,900))
for i,a in enumerate([0,math.pi/2,math.pi]):
    canvas.paste(render(faces,a,(600,900),['Front (source -Y)','Side (source +X)','Back (source +Y)'][i]),(i*600,0))
canvas.save(out/'model-preview.png')
sheet=Image.new('RGB',(1500,1500),(37,41,48))
for i,m in enumerate(mesh_list):
    sheet.paste(render(m['faces'],0,(250,300),'%02d / %d tris'%(m['number'],m['triangles'])),((i%6)*250,(i//6)*300))
sheet.save(out/'mesh-parts.png')
(out/'mesh-parts.json').write_text(json.dumps([{k:v for k,v in m.items() if k!='faces'} for m in mesh_list],indent=2),encoding='utf-8')
print(json.dumps({'preview':str(out/'model-preview.png'),'parts':str(out/'mesh-parts.png')}))
