from audit import *
import difflib
data=json.loads(read(OUT/'source-diff.json'))
used=sorted({t for v in data['impact'].values() for t in v['changed_types']})
def clean(t): return [l for l in t.splitlines() if not re.match(r'\s*//',l)]
for name in used+['ConnectionService.cs','ReconnectHandler.cs','NetworkConnector.cs','PlatformBootstrap.cs']:
    diff=list(difflib.unified_diff(clean(read(OLD/name)),clean(read(NEW/name)),fromfile='2.6.b/'+name,tofile='3.0.0/'+name,n=3))
    (OUT/(Path(name).stem+'.diff.txt')).write_text('\n'.join(diff),encoding='utf-8')
    print(name,len(diff))
if len(sys.argv)>1:
    for name in sys.argv[1:]:
        print(read(OUT/(name+'.diff.txt')))
