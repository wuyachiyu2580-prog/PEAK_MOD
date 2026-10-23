"""Diagnostic source landmarks and Scout bind-pose inspection."""
import json, math, pathlib
from PIL import Image, ImageDraw
import preview_fbx as p

out=p.out
kept=[f for f in p.faces if f[2] not in (9,26,27,28,29)]
canvas=Image.new('RGB',(1800,900))
for i,a in enumerate([0,math.pi/2,math.pi]):
    canvas.paste(p.render(kept,a,(600,900),['Clean front - 4 arms','Clean side','Clean back'][i]),(i*600,0))
canvas.save(out/'clean-preview.png')

def inverse(m):
    a=[list(m[i*4:i*4+4])+[float(i==j) for j in range(4)] for i in range(4)]
    for i in range(4):
        k=max(range(i,4),key=lambda k:abs(a[k][i]))
        a[i],a[k]=a[k],a[i]
        d=a[i][i]
        a[i]=[x/d for x in a[i]]
        for j in range(4):
            if j!=i:
                d=a[j][i]; a[j]=[x-d*y for x,y in zip(a[j],a[i])]
    return [v for row in a for v in row[4:]]

d=json.loads((p.base/'scout-reference.json').read_text())
rest={n:inverse(m) for n,m in zip(d['bones'],d['bindposes'])}
core=['Hip','Mid','Torso','Head','Arm_L','Elbow_L','Hand_L','Arm_R','Elbow_R','Hand_R','Leg_L','Knee_L','Foot_L','Leg_R','Knee_R','Foot_R']
(out/'scout-rest.json').write_text(json.dumps({n:{'matrix':rest[n],'position':[rest[n][i] for i in (3,7,11)]} for n in core},indent=2))
print(json.dumps({n:[round(rest[n][i],4) for i in (3,7,11)] for n in core}))

for number in (1,2,16,17,20,21,22,23):
    faces=next(m['faces'] for m in p.mesh_list if m['number']==number)
    panel=Image.new('RGB',(1350,580),(37,41,48))
    for view,(u,v) in enumerate([(0,2),(1,2),(0,1)]):
        dr=ImageDraw.Draw(panel)
        pts=[q for tri,c,n in faces for q in tri]
        lo=[math.floor(min(q[k] for q in pts)/10)*10-5 for k in (u,v)]
        hi=[math.ceil(max(q[k] for q in pts)/10)*10+5 for k in (u,v)]
        scale=min(390/(hi[0]-lo[0]),490/(hi[1]-lo[1]))
        def xy(q): return (view*450+40+(q[u]-lo[0])*scale,535-(q[v]-lo[1])*scale)
        depth=({0,1,2}-{u,v}).pop()
        for tri,c,n in sorted(faces,key=lambda f:sum(q[depth] for q in f[0]),reverse=True):
            dr.polygon([xy(q) for q in tri],fill=c)
        for k in range(int(lo[0])+5,int(hi[0]),10):
            x=view*450+40+(k-lo[0])*scale; dr.line((x,40,x,535),fill=(75,75,75)); dr.text((x,540),str(k),fill='white')
        for k in range(int(lo[1])+5,int(hi[1]),10):
            y=535-(k-lo[1])*scale; dr.line((view*450+40,y,view*450+435,y),fill=(75,75,75)); dr.text((view*450+5,y),str(k),fill='white')
        dr.text((view*450+15,12),'Part %02d / %s-%s source axes'%(number,'XYZ'[u],'XYZ'[v]),fill='white')
    panel.save(out/('landmarks-%02d.png'%number))
