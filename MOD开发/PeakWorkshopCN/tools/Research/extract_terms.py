"""Read only: extract PEAK's shipped localization table for terminology research."""
import csv
import hashlib
import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[4]
TEMP = ROOT / ".build/peakworkshopcn-research"
OUT = ROOT / "MOD开发/PeakWorkshopCN/research/2026-10-05-terms-performance"
sys.path.insert(0, str(TEMP / "deps"))
import UnityPy

SOURCE = Path(r"C:\SteamLibrary\steamapps\common\PEAK\PEAK_Data\data.unity3d")
KEYS = {"SHORE", "TROPICS", "ROOTS", "ALPINE", "MESA", "CALDERA", "THE KILN",
        "GLOOM", "THE CITADEL", "NADIR", "PEAK", "BEACH", "JUNGLE", "SNOW",
        "DESERT", "VOLCANO", "SWAMP", "AREA_VOID"}
OUT.mkdir(parents=True, exist_ok=True)
print("Loading shipped UnityFS bundle (read only)", flush=True)
env = UnityPy.load(str(SOURCE))
found = []
progress = []
for obj in env.objects:
    node = obj.serialized_type.node if obj.serialized_type else None
    if obj.type.name == "MonoBehaviour" and node and any(c.m_Name == "progressPoints" for c in node.m_Children):
        tree = obj.read_typetree()
        progress.append({"asset_file": obj.assets_file.name, "path_id": obj.path_id,
                         "progress_points": [{k: p.get(k) for k in ("title", "biome")} for p in tree["progressPoints"]]})
    if obj.type.name != "TextAsset":
        continue
    data = obj.read()
    if data.m_Name != "SerializedTermsData":
        continue
    raw = data.m_Script
    if isinstance(raw, str):
        raw = raw.encode("utf-8", "surrogateescape")
    terms = json.loads(raw)
    (TEMP / "SerializedTermsData.json").write_bytes(raw)
    rows = [{"key": key, "english": value[0], "simplified_chinese": value[9]}
            for key, value in terms.items() if key in KEYS]
    with (OUT / "official-area-terms.csv").open("w", encoding="utf-8-sig", newline="") as f:
        w = csv.DictWriter(f, fieldnames=["key", "english", "simplified_chinese"])
        w.writeheader()
        w.writerows(rows)
    found.append({"asset_file": obj.assets_file.name, "path_id": obj.path_id,
                  "name": data.m_Name, "table_sha256": hashlib.sha256(raw).hexdigest(),
                  "table_bytes": len(raw), "term_count": len(terms), "language_indices": {"English": 0, "SimplifiedChinese": 9},
                  "selected_terms": rows, "selected_missing_keys": sorted(KEYS - terms.keys())})
if len(found) != 1:
    raise RuntimeError(f"Expected one SerializedTermsData, found {len(found)}")
print("Hashing source bundle", flush=True)
with SOURCE.open("rb") as f:
    digest = hashlib.file_digest(f, "sha256").hexdigest()
result = {"source": str(SOURCE), "source_bytes": SOURCE.stat().st_size,
          "source_sha256": digest, "unitypy_version": UnityPy.__version__, "tables": found}
(OUT / "game-progress-points.json").write_text(json.dumps(progress, ensure_ascii=False, indent=2), encoding="utf-8")
(OUT / "localization-evidence.json").write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding="utf-8")
print(json.dumps({**result, "progress_components": len(progress)}, ensure_ascii=True, indent=2), flush=True)
