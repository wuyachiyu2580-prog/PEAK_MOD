from pathlib import Path
import re
import json
import hashlib
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[2]
OUT = Path(__file__).parent
SRC = ROOT / 'MOD开发/WhereIsThing/WhereIsThing'
TEST = ROOT / 'MOD开发/WhereIsThing/Tests'
text = (SRC / 'WhereIsThingPlugin.cs').read_text(encoding='utf-8-sig')

def extract(name):
    match = re.search(r'(?m)^        (?:private|public|internal) (?:static )?[^\n]+\b' + re.escape(name) + r'(?:<[^>]+>)?\(', text)
    assert match, name
    start = match.start()
    brace = text.index('{', match.end())
    depth = 1
    cursor = brace + 1
    while depth:
        depth += (text[cursor] == '{') - (text[cursor] == '}')
        cursor += 1
    return text[start:cursor]

methods = ['PublishShareStateIfNeeded', 'MarkShareDirty', 'ComputePayloadHash', 'UpdateRoomState',
           'OnRoomPropertiesUpdate', 'OnMasterClientSwitched', 'ClearSessionOverlay',
           'ApplySharedStateFromRoomProperties', 'TryGetRoomInt', 'TryGetRoomString',
           'GetCurrentMasterActorNumber', 'HasUsableSessionPresets', 'GetSharedPresetSource',
           'GetCurrentEffectivePreset', 'GetCurrentLocalPreset', 'GetEditableLocalPreset',
           'RefreshEffectivePresetState', 'IsClientPresetRestricted', 'RefreshActiveSceneTargets',
           'AddSceneLabel', 'TryLoadCatalog', 'BuildBuiltInPresetCache', 'ResolveBuiltInPresetTargets']
generated = 'using System;\nusing System.Linq;\nusing System.Collections.Generic;\nusing System.Globalization;\nusing UnityEngine;\nusing Photon.Pun;\nusing PhotonHashtable = System.Collections.Generic.Dictionary<string, object>;\nnamespace WhereIsThing { public sealed partial class WhereIsThingPlugin {\n'
generated += '\n\n'.join(extract(name) for name in methods) + '\n}}\n'
(OUT / 'ExtractedMethods.cs').write_text(generated, encoding='utf-8')
stubs = (TEST / 'GameStubs.cs').read_text(encoding='utf-8-sig')
stubs = stubs.replace('public void LogInfo(string text) {}', 'public void LogInfo(string text) {} public void LogWarning(string text) {}')
stubs = stubs.replace('.Concat(NapberryPlants).OfType<T>()', '.Concat(NapberryPlants).Concat(AuditSceneTargets).OfType<T>()')
(OUT / 'GameStubs.cs').write_text(stubs, encoding='utf-8')
project = ET.Element('Project', Sdk='Microsoft.NET.Sdk')
properties = ET.SubElement(project, 'PropertyGroup')
for key, value in [('OutputType','Exe'),('TargetFramework','net10.0'),('EnableDefaultCompileItems','false'),('Nullable','disable')]:
    ET.SubElement(properties,key).text=value
group=ET.SubElement(project,'ItemGroup')
for file in ['AuditHarness.cs','ExtractedMethods.cs','GameStubs.cs']:
    ET.SubElement(group,'Compile',Include=file)
for element in ET.parse(TEST/'WhereIsThing.Regression.csproj').findall('.//Compile'):
    relative=element.attrib['Include']
    if relative.startswith('../WhereIsThing/'):
        ET.SubElement(group,'Compile',Include=str((TEST/relative).resolve()))
ET.indent(project)
ET.ElementTree(project).write(OUT/'Audit.csproj',encoding='utf-8',xml_declaration=False)
hashes={str(path.relative_to(ROOT)):hashlib.sha256(path.read_bytes()).hexdigest() for path in SRC.rglob('*.cs') if 'obj' not in path.parts and 'bin' not in path.parts}
(OUT/'source-before.json').write_text(json.dumps(hashes,indent=2),encoding='utf-8')
print(json.dumps({'extracted_methods':len(methods),'source_files_hashed':len(hashes)}))
