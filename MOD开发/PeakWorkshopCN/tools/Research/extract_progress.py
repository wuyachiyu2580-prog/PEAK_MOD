"""Verify serialized stage titles/biomes without loading or modifying the game."""
import json
import sys
from pathlib import Path
ROOT = Path(__file__).resolve().parents[4]
sys.path.insert(0, str(ROOT / '.build/peakworkshopcn-research/deps'))
import UnityPy
from UnityPy.helpers.TypeTreeGenerator import TypeTreeGenerator

print('Loading shipped assets', flush=True)
env = UnityPy.load(r'C:\SteamLibrary\steamapps\common\PEAK\PEAK_Data\data.unity3d')
gen = TypeTreeGenerator('6000.3.15f1')
gen.load_local_game(r'C:\SteamLibrary\steamapps\common\PEAK')
env.typetree_generator = gen
result = []
scripts = {}
for obj in env.objects:
    if obj.type.name != 'MonoBehaviour':
        continue
    head = obj.parse_monobehaviour_head()
    ptr = head.m_Script
    if ptr.m_PathID == 0:
        continue
    key = (obj.assets_file.name, ptr.m_FileID, ptr.m_PathID)
    if key not in scripts:
        try:
            scripts[key] = ptr.deref_parse_as_object().m_ClassName
        except Exception:
            scripts[key] = ''
    if scripts[key] != 'MountainProgressHandler':
        continue
    data = obj.read_typetree()
    result.append({'asset_file': obj.assets_file.name, 'path_id': obj.path_id,
                   'points': [{k: p.get(k) for k in ('title', 'biome')} for p in data['progressPoints']]})
out = ROOT / 'MOD开发/PeakWorkshopCN/research/2026-10-05-terms-performance/game-progress-points.json'
out.write_text(json.dumps(result, ensure_ascii=True, indent=2), encoding='utf-8')
print(json.dumps(result, ensure_ascii=True, indent=2), flush=True)
if not any(p.get('title') == 'GLOOM' and p.get('biome') == 8 for r in result for p in r['points']):
    raise RuntimeError('GLOOM / BiomeType.Swamp evidence was not found')
