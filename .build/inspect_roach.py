import struct,pathlib
p=pathlib.Path('MOD开发/UprightCockroach/model-import/uprightcockroach.roach');f=p.open('rb');print(f.read(8))
def s():n=struct.unpack('<i',f.read(4))[0];return f.read(n).decode()
def m():return struct.unpack('<16f',f.read(64))
nb=struct.unpack('<i',f.read(4))[0];print('bones',nb)
for i in range(nb):
 n,d,pa=s(),s(),s();rest=m();bind=m();print(i,n,d,pa,[round(rest[j],3) for j in (3,7,11)])
nt=struct.unpack('<i',f.read(4))[0]
for i in range(nt):n=struct.unpack('<i',f.read(4))[0];f.seek(n,1)
print('tex',nt);nm=struct.unpack('<i',f.read(4))[0];print('meshes',nm)
for pi in range(nm):
 n=s();cat,mat,vc,ic=struct.unpack('<4i',f.read(16));mins=[999]*3;maxs=[-999]*3
 for v in range(vc):
  vals=struct.unpack('<8f',f.read(32));pos=vals[:3]
  for j,x in enumerate(pos):mins[j]=min(mins[j],x);maxs[j]=max(maxs[j],x)
  f.seek(32,1)
 f.seek(ic*4,1);print(pi,n,cat,vc,ic//3,'bounds',*[round(x,2) for x in mins+maxs])

