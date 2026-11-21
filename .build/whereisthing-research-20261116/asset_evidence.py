import json
import struct
import hashlib
from pathlib import Path
import UnityPy

ROOT = Path(__file__).resolve().parents[2]
DATA = Path('C:/SteamLibrary/steamapps/common/PEAK/PEAK_Data')
env = UnityPy.load(str(DATA / 'resources.assets'))
objects = {}
for obj in env.objects:
    if obj.type.name in ('MonoBehaviour', 'MonoScript', 'GameObject'):
        objects[obj.path_id] = obj
script_cache = {}
games = {pid: obj.read().m_Name for pid, obj in objects.items() if obj.type.name == 'GameObject'}
behaviours = {}
for pid, obj in objects.items():
    if obj.type.name != 'MonoBehaviour':
        continue
    header = obj.read(check_read=False)
    script_key = (header.m_Script.m_FileID, header.m_Script.path_id)
    if script_key not in script_cache:
        script_cache[script_key] = header.m_Script.read().m_ClassName if header.m_Script.path_id else ''
    script = script_cache[script_key]
    behaviours.setdefault(header.m_GameObject.path_id, []).append((pid, script, obj.get_raw_data()))

def parse_action(pid, cls, raw):
    flags = struct.unpack_from('<10I', raw, 32) if len(raw) >= 72 else []
    result = {'path_id': pid, 'class': cls, 'enabled': bool(raw[12]), 'on_consumed': bool(flags[9]) if flags else None}
    names = ('pressed', 'held', 'released', 'cast_finished', 'cancelled', 'secondary_cast_finished', 'secondary_pressed', 'secondary_held', 'secondary_cancelled', 'consumed')
    result['triggers'] = [name for name, flag in zip(names, flags) if flag]
    if cls == 'Action_InflictPoison':
        result.update(zip(('duration', 'rate', 'delay'), struct.unpack_from('<fff', raw, 72)))
    elif cls == 'Action_ModifyStatus':
        status, amount, skeleton = struct.unpack_from('<ifI', raw, 72)
        result.update(status=status, amount=amount, if_skeleton=bool(skeleton))
    elif cls == 'Action_RandomMushroomEffect':
        index, debug, effect, force = struct.unpack_from('<iIif', raw, 72)
        result.update(index=index, use_debug=bool(debug), debug_effect=effect, force=force)
    elif cls in ('Action_RestoreHunger', 'Action_GiveExtraStamina'):
        result['amount'] = struct.unpack_from('<f', raw, 72)[0]
    return result

items = []
for go, components in behaviours.items():
    for pid, cls, raw in components:
        if cls not in ('Item', 'Backpack'):
            continue
        size = struct.unpack_from('<i', raw, 112)[0]
        assert 0 <= size < 500, (pid, size)
        ui_name = raw[116:116 + size].decode('utf-8')
        file_id, target_id = struct.unpack_from('<iq', raw, 84)
        actions = [parse_action(p, c, data) for p, c, data in components if c in
                   ('Action_InflictPoison', 'Action_ModifyStatus', 'Action_RandomMushroomEffect', 'Action_RestoreHunger', 'Action_GiveExtraStamina')]
        items.append({'path_id': pid, 'game_object': go, 'prefab': games.get(go, ''), 'ui_name': ui_name,
                      'secret_identity': {'file_id': file_id, 'path_id': target_id}, 'actions': actions})

interesting = [item for item in items if any(word in item['prefab'].lower() for word in
              ('mushroom ', 'shroomberry', 'kingberry', 'apple berry', 'berrynana', 'mandrake', 'medicinal', 'egg'))]
assert len(items) > 0, 'No Item assets resolved; do not record an empty successful audit.'
assert len([item for item in items if item['prefab'].startswith('Shroomberry_')]) == 5
assert len([item for item in items if item['prefab'] in ('Mushroom Cluster', 'Mushroom Cluster Poison', 'Mushroom Lace', 'Mushroom Lace Poison', 'Mushroom Normie', 'Mushroom Normie Poison')]) == 6
assembly = DATA / 'Managed/Assembly-CSharp.dll'
report = {'scope': 'resources.assets only; assets are not proof of current spawnability',
          'method': 'UnityPy MonoBehaviour headers plus version-specific offsets verified against PEAK 3.1.0 C# fields; not a full generated type tree',
          'assembly_sha256': hashlib.sha256(assembly.read_bytes()).hexdigest(),
          'resources_sha256': hashlib.sha256((DATA / 'resources.assets').read_bytes()).hexdigest(),
          'item_asset_count': len(items), 'items': interesting,
          'secret_identity_items': [item for item in items if item['secret_identity']['path_id'] != 0]}
output = Path(__file__).with_name('asset-evidence.json')
output.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding='utf-8')
print(json.dumps({'output': str(output), 'item_count': len(items), 'selected': len(interesting),
                  'secret_identity': [(x['prefab'], x['secret_identity']) for x in report['secret_identity_items']]}, ensure_ascii=False))
