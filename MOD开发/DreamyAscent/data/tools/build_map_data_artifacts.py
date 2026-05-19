#!/usr/bin/env python3
"""Build derived DreamyAscent map-data artifacts from diagnostic samples."""

from __future__ import annotations

import hashlib
import json
from collections import Counter, defaultdict
from datetime import date
from pathlib import Path
from typing import Any


REQUIRED_DIAGNOSTIC_FILES = (
    "RuntimeExport.json",
    "NameMap.json",
    "ObjectCatalog.json",
    "ObjectReferenceMap.json",
    "GeneratedChildrenSnapshot.json",
)

OUTPUT_DIR_NAME = "generated"
PARENT_CHILD_REGISTRY_FILE_NAME = "parent-child-registry.json"
MIN_GENERATED_CHILDREN_SCHEMA_VERSION = 3
PARENT_CHILD_REGISTRY_SCHEMA_VERSION = 2
MAX_PARENT_CHILD_SOURCE_EXAMPLES = 8
MAX_PARENT_CHILD_CHILDREN = 12
MAX_PARENT_CHILD_COMPONENT_FIELDS = 6
MAX_PARENT_CHILD_FIELDS_PER_COMPONENT = 8
MAX_PARENT_CHILD_FIELD_ITEMS = 6

STRUCTURE_OR_MECHANIC_NAME_TOKENS = (
    "bridge",
    "lava",
    "river",
    "luggage",
    "mirage",
    "tornado",
    "dynamite",
    "scorpion",
    "tumbler",
    "bug",
    "bee",
    "spore",
    "exploshroom",
    "poison",
    "geyser",
    "canyon",
    "pillar",
    "platform",
)

SPECIAL_COMPONENT_TOKENS = (
    "PhotonView",
    "MultipleGroundPoints",
    "SingleItemSpawner",
    "GroundPlaceSpawner",
    "SpawnList",
    "SpawnGameObject",
    "EventOnItemCollision",
    "TriggerEvent",
    "MirageLuggage",
    "FakeItem",
    "SpineCheck",
    "Lava",
)

FIRST_PASS_SAFE_NAME_TOKENS = (
    "palmtree",
    "bush",
    "fern",
    "grass",
    "flower",
    "plant",
    "smallrock",
    "rock_lil",
    "rock_round",
)


def stable_hash(value: str, length: int = 12) -> str:
    return hashlib.sha1(value.encode("utf-8")).hexdigest()[:length]


def read_json(path: Path) -> Any:
    return json.loads(path.read_text(encoding="utf-8-sig"))


def read_generated_children_snapshot_summary(path: Path) -> dict[str, Any]:
    """Read only the top-level diagnostic fields needed for validation.

    GeneratedChildrenSnapshot files are intentionally raw and can be hundreds of
    MB each. The artifact builder only needs the header fields plus proof that
    relationshipCandidates exist, so avoid materializing the full file.
    """
    header_lines: list[str] = []
    header_complete = False
    has_relationship_candidates = False

    with path.open("r", encoding="utf-8-sig") as handle:
        for line in handle:
            if not header_complete:
                if '"segments"' in line:
                    header_complete = True
                else:
                    header_lines.append(line.rstrip("\n"))
                    continue

            if '"relationshipCandidates"' in line:
                has_relationship_candidates = True
                break

    while header_lines and not header_lines[-1].strip():
        header_lines.pop()
    if header_lines:
        last = header_lines[-1].rstrip()
        if last.endswith(","):
            header_lines[-1] = last[:-1]

    summary = json.loads("\n".join(header_lines) + "\n}")
    summary["hasRelationshipCandidates"] = has_relationship_candidates
    return summary


def write_json(path: Path, data: Any) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(
        json.dumps(data, ensure_ascii=False, indent=2, sort_keys=True) + "\n",
        encoding="utf-8",
    )


def as_list(value: Any) -> list[Any]:
    return value if isinstance(value, list) else []


def as_dict(value: Any) -> dict[str, Any]:
    return value if isinstance(value, dict) else {}


def normalize_registry_text(value: Any) -> str:
    if value is None:
        return ""

    text = str(value)
    text = text.replace("\r", " ").replace("\n", " ").replace("\t", " ")
    return text.strip()


def normalize_registry_key(value: Any) -> str:
    return normalize_registry_text(value).casefold()


def append_limited(items: list[Any], value: Any, limit: int = 12) -> None:
    if len(items) < limit:
        items.append(value)


def update_brace_state(value: str, depth: int, in_string: bool, escape: bool) -> tuple[int, bool, bool]:
    for ch in value:
        if escape:
            escape = False
            continue

        if ch == "\\" and in_string:
            escape = True
            continue

        if ch == '"':
            in_string = not in_string
            continue

        if in_string:
            continue

        if ch == "{":
            depth += 1
        elif ch == "}":
            depth -= 1

    return depth, in_string, escape


def trim_trailing_comma(lines: list[str]) -> list[str]:
    trimmed = list(lines)
    while trimmed and not trimmed[-1].strip():
        trimmed.pop()
    if trimmed:
        last = trimmed[-1].rstrip()
        if last.endswith(","):
            trimmed[-1] = last[:-1]
    return trimmed


def iter_relationship_candidates(snapshot_path: Path):
    """Yield relationship candidate objects from a huge snapshot file.

    The files are intentionally kept raw and large. This parser scans line by line
    and only materializes the candidate object currently being captured.
    """
    in_relationship_array = False
    capturing_object = False
    current_lines: list[str] = []
    object_depth = 0
    in_string = False
    escape = False

    with snapshot_path.open("r", encoding="utf-8-sig") as handle:
        for line in handle:
            if not in_relationship_array:
                if '"relationshipCandidates"' in line:
                    in_relationship_array = True
                continue

            if not capturing_object:
                stripped = line.strip()
                if stripped.startswith("]"):
                    in_relationship_array = False
                    continue

                if stripped.startswith("{"):
                    capturing_object = True
                    current_lines = [line]
                    object_depth, in_string, escape = update_brace_state(line, 0, False, False)
                    if object_depth == 0:
                        yield json.loads("".join(current_lines))
                        capturing_object = False
                        current_lines = []

                continue

            current_lines.append(line)
            object_depth, in_string, escape = update_brace_state(line, object_depth, in_string, escape)
            if object_depth == 0:
                yield json.loads("".join(trim_trailing_comma(current_lines)))
                capturing_object = False
                current_lines = []


def sorted_counter(counter: Counter[str]) -> dict[str, int]:
    return {key: counter[key] for key in sorted(counter)}


def unique_sorted(values: set[str]) -> list[str]:
    return sorted(value for value in values if value is not None)


def normalize_token(value: str) -> str:
    return "".join(ch for ch in value.lower() if ch.isalnum())


def path_starts_with_any(path: str, prefixes: list[str]) -> bool:
    if not path:
        return True
    for prefix in prefixes:
        if path == prefix or path.startswith(prefix + "/"):
            return True
    return False


def has_enabled_external_modifier(snapshot: dict[str, Any], modifier_name: str) -> bool:
    for modifier in as_list(snapshot.get("externalMapModifiers")):
        if not isinstance(modifier, dict):
            continue
        if modifier.get("enabled") and modifier.get("name") == modifier_name:
            return True
    return False


def prop_map(properties: list[Any]) -> dict[str, Any]:
    result: dict[str, Any] = {}
    for item in properties:
        if not isinstance(item, dict):
            continue
        name = item.get("name")
        if name:
            result[name] = item.get("value")
    return result


def type_list(items: list[Any]) -> list[str]:
    values = []
    for item in items:
        if isinstance(item, dict) and item.get("type"):
            values.append(item["type"])
    return sorted(set(values))


def source_key_to_output_key(source_key: str) -> str:
    return source_key


def discover_sample_dirs(map_data_dir: Path, sample_index: dict[str, Any]) -> list[dict[str, Any]]:
    sample_dirs: list[dict[str, Any]] = []
    for source_key, source_info in as_dict(sample_index.get("sources")).items():
        relative_path = source_info.get("path")
        if not relative_path:
            continue
        source_root = map_data_dir / relative_path
        diagnostics_root = source_root / "DreamyAscent Diagnostics"
        files_root = source_root / "DreamyAscent Files"
        for diagnostic_dir in sorted(path for path in diagnostics_root.glob("*") if path.is_dir()):
            sample_dirs.append(
                {
                    "sourceKey": source_key_to_output_key(source_key),
                    "sourceRole": source_info.get("role"),
                    "sourceRoot": source_root,
                    "diagnosticDir": diagnostic_dir,
                    "diagnosticName": diagnostic_dir.name,
                    "exportJsonFiles": sorted(files_root.glob("*.json")) if files_root.exists() else [],
                }
            )
    return sample_dirs


def build_step_snapshot(step: dict[str, Any]) -> dict[str, Any]:
    step_path = step.get("hierarchyPath") or ""
    properties = prop_map(as_list(step.get("properties")))
    return {
        "stepId": "step:" + stable_hash(step_path or step.get("stepName", "")),
        "name": step.get("stepName"),
        "type": step.get("stepType"),
        "path": step_path,
        "properties": properties,
        "modifierTypes": type_list(as_list(step.get("modifiers"))),
        "constraintTypes": type_list(as_list(step.get("constraints"))),
        "postConstraintTypes": type_list(as_list(step.get("postConstraints"))),
    }


def build_snapshot_artifact(sample_records: list[dict[str, Any]]) -> dict[str, Any]:
    snapshots = []
    step_type_counts: Counter[str] = Counter()
    property_counts: Counter[str] = Counter()
    modifier_counts: Counter[str] = Counter()
    constraint_counts: Counter[str] = Counter()
    post_constraint_counts: Counter[str] = Counter()

    for record in sample_records:
        runtime = record["runtime"]
        map_data = as_dict(runtime.get("map"))
        map_key = map_data.get("mapKey")
        for segment in as_list(map_data.get("segments")):
            segment_name = segment.get("segmentName")
            variant = segment.get("normalizedVariantName")
            groupers_out = []
            step_count = 0
            for grouper in as_list(segment.get("groupers")):
                steps_out = []
                for step in as_list(grouper.get("steps")):
                    step_count += 1
                    if step.get("stepType"):
                        step_type_counts[step["stepType"]] += 1
                    for key in prop_map(as_list(step.get("properties"))):
                        property_counts[key] += 1
                    for key in type_list(as_list(step.get("modifiers"))):
                        modifier_counts[key] += 1
                    for key in type_list(as_list(step.get("constraints"))):
                        constraint_counts[key] += 1
                    for key in type_list(as_list(step.get("postConstraints"))):
                        post_constraint_counts[key] += 1
                    steps_out.append(build_step_snapshot(step))

                grouper_path = grouper.get("hierarchyPath") or ""
                groupers_out.append(
                    {
                        "grouperId": "grouper:" + stable_hash(grouper_path or grouper.get("grouperName", "")),
                        "name": grouper.get("grouperName"),
                        "path": grouper_path,
                        "stepCount": len(steps_out),
                        "steps": steps_out,
                    }
                )

            snapshot_basis = "|".join(
                [
                    record["sourceKey"],
                    record["diagnosticName"],
                    segment_name or "",
                    variant or "",
                    segment.get("segmentPath") or "",
                ]
            )
            snapshots.append(
                {
                    "snapshotId": "snapshot:" + stable_hash(snapshot_basis),
                    "source": record["sourceKey"],
                    "diagnosticDirectory": record["diagnosticName"],
                    "mapKey": map_key,
                    "levelSlot": segment.get("levelSlot"),
                    "segmentName": segment_name,
                    "segmentPath": segment.get("segmentPath"),
                    "variantSelectionType": segment.get("variantSelectionType"),
                    "normalizedVariantName": variant,
                    "activeVariantNames": as_list(segment.get("activeVariantNames")),
                    "activeVariantPaths": as_list(segment.get("activeVariantPaths")),
                    "rootPaths": as_list(segment.get("rootPaths")),
                    "grouperCount": len(groupers_out),
                    "stepCount": step_count,
                    "groupers": groupers_out,
                }
            )

    return {
        "schemaVersion": 1,
        "generatedAt": date.today().isoformat(),
        "summary": {
            "snapshotCount": len(snapshots),
            "stepTypeCounts": sorted_counter(step_type_counts),
            "propertyCounts": sorted_counter(property_counts),
            "modifierTypeCounts": sorted_counter(modifier_counts),
            "constraintTypeCounts": sorted_counter(constraint_counts),
            "postConstraintTypeCounts": sorted_counter(post_constraint_counts),
        },
        "snapshots": snapshots,
    }


def merge_template(registry: dict[str, Any], item: dict[str, Any], record: dict[str, Any]) -> None:
    stable_key = item.get("stableKey") or item.get("id") or item.get("name")
    if not stable_key:
        return
    registry_id = "template:" + stable_hash(stable_key)
    entry = registry.setdefault(
        registry_id,
        {
            "registryId": registry_id,
            "stableKey": stable_key,
            "kind": item.get("kind"),
            "name": item.get("name"),
            "displayName": item.get("displayName"),
            "objectType": item.get("objectType"),
            "gameObjectPath": item.get("gameObjectPath"),
            "scene": item.get("scene"),
            "components": set(),
            "rendererMaterials": set(),
            "roles": set(),
            "segments": set(),
            "variants": defaultdict(set),
            "sampleSources": Counter(),
            "sourceCount": 0,
            "sourceExamples": [],
            "hasChildGeneration": False,
            "hasSingleItemSpawner": False,
            "hasPhotonView": False,
            "childLevelGenStepCount": 0,
            "childSingleItemSpawnerCount": 0,
            "rendererCount": 0,
        },
    )

    entry["roles"].add(item.get("role"))
    entry["segments"].add(item.get("segment"))
    source = as_dict(item.get("source"))
    if source.get("normalizedVariantName"):
        entry["variants"][item.get("segment")].add(source.get("normalizedVariantName"))
    entry["components"].update(as_list(item.get("components")))
    entry["rendererMaterials"].update(as_list(item.get("rendererMaterials")))
    entry["sampleSources"][record["sourceKey"]] += 1
    entry["sourceCount"] += 1
    entry["hasChildGeneration"] = entry["hasChildGeneration"] or bool(item.get("hasChildGeneration"))
    entry["hasSingleItemSpawner"] = entry["hasSingleItemSpawner"] or bool(item.get("hasSingleItemSpawner"))
    entry["hasPhotonView"] = entry["hasPhotonView"] or bool(item.get("hasPhotonView"))
    entry["childLevelGenStepCount"] = max(entry["childLevelGenStepCount"], item.get("childLevelGenStepCount") or 0)
    entry["childSingleItemSpawnerCount"] = max(
        entry["childSingleItemSpawnerCount"],
        item.get("childSingleItemSpawnerCount") or 0,
    )
    entry["rendererCount"] = max(entry["rendererCount"], item.get("rendererCount") or 0)
    append_limited(
        entry["sourceExamples"],
        {
            "itemId": item.get("id"),
            "source": record["sourceKey"],
            "diagnosticDirectory": record["diagnosticName"],
            "segment": item.get("segment"),
            "normalizedVariantName": source.get("normalizedVariantName"),
            "role": item.get("role"),
            "grouperPath": source.get("grouperPath"),
            "stepPath": source.get("stepPath"),
            "defaults": item.get("defaults"),
        },
    )


def merge_material(registry: dict[str, Any], material: dict[str, Any], record: dict[str, Any]) -> None:
    stable_key = material.get("stableKey") or material.get("id") or material.get("name")
    if not stable_key:
        return
    registry_id = "material:" + stable_hash(stable_key)
    entry = registry.setdefault(
        registry_id,
        {
            "registryId": registry_id,
            "stableKey": stable_key,
            "name": material.get("name"),
            "displayName": material.get("displayName"),
            "objectType": material.get("objectType"),
            "shader": material.get("shader"),
            "color": material.get("color"),
            "mainTexture": material.get("mainTexture"),
            "roles": set(),
            "segments": set(),
            "variants": defaultdict(set),
            "sampleSources": Counter(),
            "sourceCount": 0,
            "sourceExamples": [],
        },
    )
    entry["roles"].add(material.get("role"))
    entry["segments"].add(material.get("segment"))
    source = as_dict(material.get("source"))
    if source.get("normalizedVariantName"):
        entry["variants"][material.get("segment")].add(source.get("normalizedVariantName"))
    entry["sampleSources"][record["sourceKey"]] += 1
    entry["sourceCount"] += 1
    append_limited(
        entry["sourceExamples"],
        {
            "materialId": material.get("id"),
            "source": record["sourceKey"],
            "diagnosticDirectory": record["diagnosticName"],
            "segment": material.get("segment"),
            "normalizedVariantName": source.get("normalizedVariantName"),
            "role": material.get("role"),
            "stepPath": source.get("stepPath"),
            "field": source.get("field"),
        },
    )


def risk_tags(entry: dict[str, Any]) -> list[str]:
    tags = []
    name = (entry.get("name") or "").lower()
    components = set(entry.get("components") or [])
    if entry.get("hasPhotonView"):
        tags.append("photon-view")
    if entry.get("hasChildGeneration") or entry.get("childLevelGenStepCount", 0) > 0:
        tags.append("child-generation")
    if entry.get("hasSingleItemSpawner") or entry.get("childSingleItemSpawnerCount", 0) > 0:
        tags.append("single-item-spawner")
    if entry.get("objectType") != "UnityEngine.GameObject":
        tags.append("non-gameobject")
    if any(token in name for token in STRUCTURE_OR_MECHANIC_NAME_TOKENS):
        tags.append("structure-or-mechanic-name")
    if any(any(token in component for token in SPECIAL_COMPONENT_TOKENS) for component in components):
        tags.append("special-component")
    return tags


def is_first_pass_candidate(entry: dict[str, Any], tags: list[str]) -> bool:
    name = (entry.get("name") or "").lower()
    if tags:
        return False
    if "step-prop-prefab" not in entry["roles"]:
        return False
    return any(token in name for token in FIRST_PASS_SAFE_NAME_TOKENS)


def finalize_template(entry: dict[str, Any]) -> dict[str, Any]:
    tags = risk_tags(entry)
    technical_low_risk = not tags and "step-prop-prefab" in entry["roles"]
    recommended_first_pass = is_first_pass_candidate(entry, tags)
    return {
        "registryId": entry["registryId"],
        "stableKey": entry["stableKey"],
        "kind": entry["kind"],
        "name": entry["name"],
        "displayName": entry["displayName"],
        "objectType": entry["objectType"],
        "gameObjectPath": entry["gameObjectPath"],
        "scene": entry["scene"],
        "roles": unique_sorted(entry["roles"]),
        "segments": unique_sorted(entry["segments"]),
        "variants": {segment: unique_sorted(values) for segment, values in sorted(entry["variants"].items())},
        "components": unique_sorted(entry["components"]),
        "rendererMaterials": unique_sorted(entry["rendererMaterials"]),
        "hasChildGeneration": entry["hasChildGeneration"],
        "hasSingleItemSpawner": entry["hasSingleItemSpawner"],
        "hasPhotonView": entry["hasPhotonView"],
        "childLevelGenStepCount": entry["childLevelGenStepCount"],
        "childSingleItemSpawnerCount": entry["childSingleItemSpawnerCount"],
        "rendererCount": entry["rendererCount"],
        "riskTags": tags,
        "technicalLowRiskPlacementCandidate": technical_low_risk,
        "recommendedFirstPassCandidate": recommended_first_pass,
        "sampleSources": sorted_counter(entry["sampleSources"]),
        "sourceCount": entry["sourceCount"],
        "sourceExamples": entry["sourceExamples"],
    }


def finalize_material(entry: dict[str, Any]) -> dict[str, Any]:
    return {
        "registryId": entry["registryId"],
        "stableKey": entry["stableKey"],
        "name": entry["name"],
        "displayName": entry["displayName"],
        "objectType": entry["objectType"],
        "shader": entry["shader"],
        "color": entry["color"],
        "mainTexture": entry["mainTexture"],
        "roles": unique_sorted(entry["roles"]),
        "segments": unique_sorted(entry["segments"]),
        "variants": {segment: unique_sorted(values) for segment, values in sorted(entry["variants"].items())},
        "sampleSources": sorted_counter(entry["sampleSources"]),
        "sourceCount": entry["sourceCount"],
        "sourceExamples": entry["sourceExamples"],
    }


def build_registry_artifact(sample_records: list[dict[str, Any]]) -> dict[str, Any]:
    templates: dict[str, Any] = {}
    materials: dict[str, Any] = {}
    role_counts: Counter[str] = Counter()
    material_role_counts: Counter[str] = Counter()

    for record in sample_records:
        catalog = record["catalog"]
        for item in as_list(catalog.get("items")):
            role_counts[item.get("role") or "unknown"] += 1
            merge_template(templates, item, record)
        for material in as_list(catalog.get("materials")):
            material_role_counts[material.get("role") or "unknown"] += 1
            merge_material(materials, material, record)

    finalized_templates = [finalize_template(item) for item in templates.values()]
    finalized_materials = [finalize_material(item) for item in materials.values()]
    finalized_templates.sort(
        key=lambda item: (
            not item["recommendedFirstPassCandidate"],
            not item["technicalLowRiskPlacementCandidate"],
            item["name"] or "",
        )
    )
    finalized_materials.sort(key=lambda item: item["name"] or "")

    technical_low_risk_count = sum(1 for item in finalized_templates if item["technicalLowRiskPlacementCandidate"])
    recommended_first_pass_count = sum(1 for item in finalized_templates if item["recommendedFirstPassCandidate"])
    risk_counter: Counter[str] = Counter()
    for item in finalized_templates:
        for tag in item["riskTags"]:
            risk_counter[tag] += 1

    return {
        "schemaVersion": 1,
        "generatedAt": date.today().isoformat(),
        "summary": {
            "templateCount": len(finalized_templates),
            "materialCount": len(finalized_materials),
            "technicalLowRiskPlacementCandidateCount": technical_low_risk_count,
            "recommendedFirstPassCandidateCount": recommended_first_pass_count,
            "itemRoleCounts": sorted_counter(role_counts),
            "materialRoleCounts": sorted_counter(material_role_counts),
            "riskTagCounts": sorted_counter(risk_counter),
        },
        "templates": finalized_templates,
        "materials": finalized_materials,
    }


def build_parent_child_registry_artifact(
    sample_records: list[dict[str, Any]],
    registry_artifact: dict[str, Any],
) -> dict[str, Any]:
    templates_out: list[dict[str, Any]] = []
    segments: dict[str, dict[str, Any]] = {}
    template_lookup_by_path: dict[str, list[dict[str, Any]]] = defaultdict(list)
    template_lookup_by_name: dict[str, list[dict[str, Any]]] = defaultdict(list)
    template_child_keys: dict[str, set[str]] = defaultdict(set)
    template_field_keys: dict[str, set[str]] = defaultdict(set)
    source_counts: Counter[str] = Counter()
    risk_counter: Counter[str] = Counter()

    parent_child_template_count = 0
    single_item_template_count = 0

    for segment in as_list(registry_artifact.get("segments")):
        if not isinstance(segment, dict):
            continue
        segment_name = segment.get("segment") or ""
        segments[segment_name] = {
            "segment": segment_name,
            "levelSlot": segment.get("levelSlot", -1),
            "segmentPath": segment.get("segmentPath"),
            "variantSelectionType": segment.get("variantSelectionType"),
            "normalizedVariantName": segment.get("normalizedVariantName"),
            "activeVariantNames": set(as_list(segment.get("activeVariantNames"))),
            "activeVariantPaths": set(as_list(segment.get("activeVariantPaths"))),
            "rootPaths": set(as_list(segment.get("rootPaths"))),
            "displayName": segment.get("displayName"),
            "templateIds": set(),
        }

    for template in as_list(registry_artifact.get("templates")):
        if not isinstance(template, dict):
            continue

        roles = set(as_list(template.get("roles")))
        if "parent-child-template" not in roles and "single-item-prefab" not in roles:
            continue

        finalized_template = finalize_parent_child_template(template)
        templates_out.append(finalized_template)

        game_object_path = normalize_registry_key(finalized_template.get("gameObjectPath"))
        if game_object_path:
            template_lookup_by_path[game_object_path].append(finalized_template)

        template_name = normalize_registry_key(finalized_template.get("name"))
        if template_name:
            template_lookup_by_name[template_name].append(finalized_template)

        if finalized_template["kind"] == "single-item-prefab" or "single-item-spawner" in finalized_template["riskTags"]:
            single_item_template_count += 1
        if "parent-child-template" in roles or "child-generation" in finalized_template["riskTags"]:
            parent_child_template_count += 1

        for key, value in (template.get("sampleSources") or {}).items():
            if key:
                source_counts[key] += int(value or 0)

        for tag in finalized_template["riskTags"]:
            risk_counter[tag] += 1

        for segment_name in finalized_template["segments"]:
            segment_entry = segments.setdefault(
                segment_name,
                {
                    "segment": segment_name,
                    "levelSlot": -1,
                    "segmentPath": None,
                    "variantSelectionType": None,
                    "normalizedVariantName": None,
                    "activeVariantNames": set(),
                    "activeVariantPaths": set(),
                    "rootPaths": set(),
                    "displayName": None,
                    "templateIds": set(),
                },
            )
            segment_entry["templateIds"].add(finalized_template["registryId"])

    for record in sample_records:
        snapshot_path = Path(record["diagnosticDir"]) / "GeneratedChildrenSnapshot.json"
        for candidate in iter_relationship_candidates(snapshot_path):
            if not isinstance(candidate, dict) or candidate.get("sourceKind") != "object-candidate":
                continue

            matched_templates = list(template_lookup_by_path.get(normalize_registry_key(candidate.get("path")), []))
            if not matched_templates:
                matched_templates = list(template_lookup_by_name.get(normalize_registry_key(candidate.get("name")), []))
                if len(matched_templates) != 1:
                    continue

            for template in matched_templates:
                registry_id = template.get("registryId") or ""
                if not registry_id:
                    continue

                for child in as_list(candidate.get("children")):
                    if not isinstance(child, dict):
                        continue

                    child_entry = {
                        "depth": child.get("depth") or 0,
                        "childIndex": child.get("childIndex") or 0,
                        "name": child.get("name"),
                        "path": child.get("path"),
                        "pathHash": child.get("pathHash"),
                        "stableSignature": child.get("stableSignature"),
                        "sourceKind": child.get("sourceKind"),
                        "relationshipType": child.get("relationshipType"),
                        "reason": child.get("reason"),
                        "confidence": child.get("confidence") or 0.0,
                        "activeSelf": bool(child.get("activeSelf")),
                        "activeInHierarchy": bool(child.get("activeInHierarchy")),
                        "rendererCount": child.get("rendererCount") or 0,
                        "colliderCount": child.get("colliderCount") or 0,
                        "levelGenStepCount": child.get("levelGenStepCount") or 0,
                        "propGrouperCount": child.get("propGrouperCount") or 0,
                        "singleItemSpawnerCount": child.get("singleItemSpawnerCount") or 0,
                        "photonViewCount": child.get("photonViewCount") or 0,
                    }
                    child_key = "|".join(
                        [
                            normalize_registry_key(child_entry.get("path")),
                            normalize_registry_key(child_entry.get("stableSignature")),
                            normalize_registry_key(child_entry.get("relationshipType")),
                        ]
                    )
                    if child_key in template_child_keys[registry_id]:
                        continue

                    template_child_keys[registry_id].add(child_key)
                    append_limited(template["children"], child_entry, MAX_PARENT_CHILD_CHILDREN)

                for component in as_list(candidate.get("interestingComponentFields")):
                    if not isinstance(component, dict):
                        continue

                    component_entry = {
                        "componentType": component.get("componentType"),
                        "componentName": component.get("componentName"),
                        "objectPath": component.get("objectPath"),
                        "fields": [],
                    }
                    component_key = "|".join(
                        [
                            normalize_registry_key(component_entry.get("componentType")),
                            normalize_registry_key(component_entry.get("componentName")),
                            normalize_registry_key(component_entry.get("objectPath")),
                        ]
                    )
                    if component_key in template_field_keys[registry_id]:
                        continue

                    for field in as_list(component.get("fields")):
                        if not isinstance(field, dict):
                            continue

                        field_entry = {
                            "name": field.get("name"),
                            "type": field.get("type"),
                            "valueKind": field.get("valueKind"),
                            "value": field.get("value"),
                            "objectPath": field.get("objectPath"),
                            "objectInstanceId": field.get("objectInstanceId") or 0,
                            "count": field.get("count") or 0,
                            "truncated": bool(field.get("truncated")),
                            "items": [],
                        }
                        for item in as_list(field.get("items")):
                            if not isinstance(item, dict):
                                continue
                            field_entry["items"].append(
                                {
                                    "valueKind": item.get("valueKind"),
                                    "value": item.get("value"),
                                    "objectPath": item.get("objectPath"),
                                    "objectInstanceId": item.get("objectInstanceId") or 0,
                                }
                            )
                            if len(field_entry["items"]) >= MAX_PARENT_CHILD_FIELD_ITEMS:
                                break

                        component_entry["fields"].append(field_entry)
                        if len(component_entry["fields"]) >= MAX_PARENT_CHILD_FIELDS_PER_COMPONENT:
                            break

                    if not component_entry["fields"]:
                        continue

                    template_field_keys[registry_id].add(component_key)
                    append_limited(template["interestingComponentFields"], component_entry, MAX_PARENT_CHILD_COMPONENT_FIELDS)

    for template in templates_out:
        template["children"].sort(
            key=lambda item: (
                normalize_registry_key(item.get("path")),
                normalize_registry_key(item.get("stableSignature")),
                item.get("depth") or 0,
                item.get("childIndex") or 0,
            )
        )
        template["interestingComponentFields"].sort(
            key=lambda item: (
                normalize_registry_key(item.get("objectPath")),
                normalize_registry_key(item.get("componentType")),
                normalize_registry_key(item.get("componentName")),
            )
        )

    finalized_segments = [finalize_parent_child_segment(segment) for segment in segments.values()]
    finalized_segments.sort(key=lambda item: (item["segment"] or "", item["normalizedVariantName"] or "", item["segmentPath"] or ""))
    templates_out.sort(key=lambda item: (item["segments"][0] if item.get("segments") else "", item["name"] or "", item["registryId"] or ""))

    summary = {
        "segmentCount": len(finalized_segments),
        "templateCount": len(templates_out),
        "parentChildTemplateCount": parent_child_template_count,
        "singleItemSpawnerTemplateCount": single_item_template_count,
        "technicalLowRiskPlacementCandidateCount": sum(1 for item in templates_out if item["technicalLowRiskPlacementCandidate"]),
        "recommendedFirstPassCandidateCount": sum(1 for item in templates_out if item["recommendedFirstPassCandidate"]),
        "sourceCounts": sorted_counter(source_counts),
        "riskTagCounts": sorted_counter(risk_counter),
    }

    return {
        "schemaVersion": PARENT_CHILD_REGISTRY_SCHEMA_VERSION,
        "generatedAt": date.today().isoformat(),
        "summary": summary,
        "segments": finalized_segments,
        "templates": templates_out,
    }


def finalize_parent_child_template(entry: dict[str, Any]) -> dict[str, Any]:
    source_examples = []
    for example in as_list(entry.get("sourceExamples")):
        if not isinstance(example, dict):
            continue
        source_examples.append(
            {
                "templateId": entry.get("registryId"),
                "source": example.get("source"),
                "diagnosticDirectory": example.get("diagnosticDirectory"),
                "segment": example.get("segment"),
                "normalizedVariantName": example.get("normalizedVariantName"),
                "role": example.get("role"),
                "sourceKind": example.get("role") or entry.get("kind"),
                "reason": ", ".join(entry.get("riskTags") or []) or entry.get("kind"),
                "grouperPath": example.get("grouperPath"),
                "stepPath": example.get("stepPath"),
                "relationshipCount": entry.get("sourceCount") or 0,
                "childCount": entry.get("childLevelGenStepCount") or 0,
                "defaults": example.get("defaults"),
            }
        )

    return {
        "registryId": normalize_registry_text(entry.get("registryId")),
        "stableKey": normalize_registry_text(entry.get("stableKey")),
        "kind": normalize_registry_text(entry.get("kind")),
        "name": normalize_registry_text(entry.get("name")),
        "displayName": normalize_registry_text(entry.get("displayName")),
        "objectType": normalize_registry_text(entry.get("objectType")),
        "gameObjectPath": normalize_registry_text(entry.get("gameObjectPath")),
        "scene": normalize_registry_text(entry.get("scene")),
        "roles": unique_sorted(set(as_list(entry.get("roles")))),
        "segments": unique_sorted(set(as_list(entry.get("segments")))),
        "variants": {segment: unique_sorted(set(values)) for segment, values in sorted(as_dict(entry.get("variants")).items())},
        "components": unique_sorted(set(as_list(entry.get("components")))),
        "rendererMaterials": unique_sorted(set(as_list(entry.get("rendererMaterials")))),
        "hasChildGeneration": bool(entry.get("hasChildGeneration")),
        "hasSingleItemSpawner": bool(entry.get("hasSingleItemSpawner")),
        "hasPhotonView": bool(entry.get("hasPhotonView")),
        "childLevelGenStepCount": entry.get("childLevelGenStepCount") or 0,
        "childSingleItemSpawnerCount": entry.get("childSingleItemSpawnerCount") or 0,
        "rendererCount": entry.get("rendererCount") or 0,
        "riskTags": unique_sorted(set(as_list(entry.get("riskTags")))),
        "technicalLowRiskPlacementCandidate": bool(entry.get("technicalLowRiskPlacementCandidate")),
        "recommendedFirstPassCandidate": bool(entry.get("recommendedFirstPassCandidate")),
        "sampleSources": sorted_counter(Counter(as_dict(entry.get("sampleSources")))),
        "sourceCount": entry.get("sourceCount") or 0,
        "sourceExamples": source_examples,
        "children": [],
        "interestingComponentFields": [],
        "segment": normalize_registry_text(entry.get("segments")[0]) if as_list(entry.get("segments")) else None,
    }


def finalize_parent_child_segment(entry: dict[str, Any]) -> dict[str, Any]:
    return {
        "segment": normalize_registry_text(entry.get("segment")),
        "levelSlot": entry.get("levelSlot", -1),
        "segmentPath": normalize_registry_text(entry.get("segmentPath")),
        "variantSelectionType": normalize_registry_text(entry.get("variantSelectionType")),
        "normalizedVariantName": normalize_registry_text(entry.get("normalizedVariantName")),
        "activeVariantNames": unique_sorted(set(as_list(entry.get("activeVariantNames")))),
        "activeVariantPaths": unique_sorted(set(as_list(entry.get("activeVariantPaths")))),
        "rootPaths": unique_sorted(set(as_list(entry.get("rootPaths")))),
        "displayName": normalize_registry_text(entry.get("displayName")),
        "templateIds": unique_sorted(set(as_list(entry.get("templateIds")))),
    }


def iter_parent_child_groups(snapshot_path: Path):
    """Yield lightweight parent-child groups from a raw snapshot.

    The current implementation intentionally keeps this narrow: it returns the
    top-level segment snapshot and the relationship candidate group payload that
    already exists in the raw file.
    """
    data = read_json(snapshot_path)
    for segment in as_list(data.get("segments")):
        if not isinstance(segment, dict):
            continue
        for group in as_list(segment.get("relationshipCandidates")):
            if isinstance(group, dict):
                yield segment, group


def build_parent_child_group_id(record: dict[str, Any], segment_snapshot: dict[str, Any], group: dict[str, Any]) -> str:
    key_parts = [
        record.get("sourceKey") or "",
        record.get("diagnosticName") or "",
        segment_snapshot.get("segmentName") or "",
        segment_snapshot.get("normalizedVariantName") or "",
        group.get("path") or "",
        group.get("stableSignature") or "",
        group.get("sourceKind") or "",
    ]
    return "pc:" + stable_hash("|".join(key_parts), 16)


def build_parent_child_source_example(record: dict[str, Any], segment_snapshot: dict[str, Any], group: dict[str, Any]) -> dict[str, Any]:
    return {
        "templateId": None,
        "source": record.get("sourceKey"),
        "diagnosticDirectory": record.get("diagnosticName"),
        "segment": segment_snapshot.get("segmentName"),
        "normalizedVariantName": segment_snapshot.get("normalizedVariantName"),
        "role": group.get("role"),
        "sourceKind": group.get("sourceKind"),
        "reason": group.get("reason"),
        "grouperPath": group.get("sourceGrouperPath"),
        "stepPath": group.get("sourceStepPath"),
        "relationshipCount": 1,
        "childCount": group.get("childCount") or 0,
        "defaults": {},
    }


def build_parent_child_child_summary(group: dict[str, Any]) -> dict[str, Any]:
    return {
        "depth": 1,
        "childIndex": 0,
        "name": group.get("name"),
        "path": group.get("path"),
        "pathHash": group.get("pathHash"),
        "stableSignature": group.get("stableSignature"),
        "sourceKind": group.get("sourceKind"),
        "relationshipType": group.get("role"),
        "reason": group.get("reason"),
        "confidence": 1.0 if group.get("role") == "parent-child-template" else 0.8,
        "activeSelf": bool(group.get("activeSelf", True)),
        "activeInHierarchy": bool(group.get("activeInHierarchy", True)),
        "rendererCount": group.get("rendererCount") or 0,
        "colliderCount": group.get("colliderCount") or 0,
        "levelGenStepCount": group.get("childLevelGenStepCount") or 0,
        "propGrouperCount": 0,
        "singleItemSpawnerCount": group.get("childSingleItemSpawnerCount") or 0,
        "photonViewCount": 0,
    }


def build_parent_child_component_fields(group: dict[str, Any]) -> list[dict[str, Any]]:
    fields = []
    for component in as_list(group.get("interestingComponentFields")):
        if not isinstance(component, dict):
            continue
        fields.append(
            {
                "componentType": component.get("componentType"),
                "componentName": component.get("componentName"),
                "objectPath": component.get("objectPath"),
                "fields": as_list(component.get("fields"))[:MAX_PARENT_CHILD_FIELDS_PER_COMPONENT],
            }
        )
    return fields


def check_regression(
    sample_records: list[dict[str, Any]],
    sample_index: dict[str, Any],
    discovery_issues: list[dict[str, Any]],
) -> dict[str, Any]:
    expected_coverage = as_dict(sample_index.get("variantCoverage"))
    selection_types = as_dict(sample_index.get("selectionTypes"))
    purity_rules = as_dict(sample_index.get("purityRules"))
    known_shared = {
        segment: set(as_list(values))
        for segment, values in as_dict(purity_rules.get("knownSharedStepNames")).items()
    }

    coverage: dict[str, dict[str, Counter[str]]] = defaultdict(lambda: defaultdict(Counter))
    issues: list[dict[str, Any]] = list(discovery_issues)
    warnings: list[dict[str, Any]] = []
    shared_step_hits: Counter[str] = Counter()
    diagnostics_checked = 0
    segment_count = 0
    grouper_count = 0
    step_count = 0
    catalog_item_count = 0
    catalog_material_count = 0
    reference_count = 0

    for record in sample_records:
        diagnostics_checked += 1
        runtime = record["runtime"]
        catalog = record["catalog"]
        reference_map = record["referenceMap"]
        catalog_item_count += len(as_list(catalog.get("items")))
        catalog_material_count += len(as_list(catalog.get("materials")))
        reference_count += reference_map.get("ReferenceCount") or len(as_list(reference_map.get("References")))
        map_data = as_dict(runtime.get("map"))
        for segment in as_list(map_data.get("segments")):
            segment_count += 1
            segment_name = segment.get("segmentName")
            variant = segment.get("normalizedVariantName")
            active_paths = as_list(segment.get("activeVariantPaths"))
            groupers = as_list(segment.get("groupers"))
            grouper_count += len(groupers)
            if segment_name in expected_coverage:
                if not groupers:
                    issues.append(
                        {
                            "type": "zero-grouper-segment",
                            "source": record["sourceKey"],
                            "diagnosticDirectory": record["diagnosticName"],
                            "segment": segment_name,
                            "variant": variant,
                        }
                    )
                expected_variants = set(as_list(expected_coverage[segment_name].get("expected")))
                if variant not in expected_variants:
                    issues.append(
                        {
                            "type": "unknown-variant",
                            "source": record["sourceKey"],
                            "diagnosticDirectory": record["diagnosticName"],
                            "segment": segment_name,
                            "variant": variant,
                        }
                    )
                else:
                    coverage[segment_name][variant][record["sourceKey"]] += 1

            if selection_types.get(segment_name) == "BiomeVariant" and active_paths:
                for grouper in groupers:
                    grouper_path = grouper.get("hierarchyPath") or ""
                    if not path_starts_with_any(grouper_path, active_paths):
                        issues.append(
                            {
                                "type": "biome-variant-path-outside-active-root",
                                "source": record["sourceKey"],
                                "diagnosticDirectory": record["diagnosticName"],
                                "segment": segment_name,
                                "variant": variant,
                                "path": grouper_path,
                                "activeVariantPaths": active_paths,
                            }
                        )

            for grouper in groupers:
                for step in as_list(grouper.get("steps")):
                    step_count += 1
                    step_path = step.get("hierarchyPath") or ""
                    step_name = step.get("stepName") or ""
                    if selection_types.get(segment_name) == "BiomeVariant" and active_paths:
                        if not path_starts_with_any(step_path, active_paths):
                            issues.append(
                                {
                                    "type": "biome-variant-step-outside-active-root",
                                    "source": record["sourceKey"],
                                    "diagnosticDirectory": record["diagnosticName"],
                                    "segment": segment_name,
                                    "variant": variant,
                                    "path": step_path,
                                    "activeVariantPaths": active_paths,
                                }
                            )
                    for shared_name in known_shared.get(segment_name, set()):
                        if shared_name != variant and shared_name.lower() in step_name.lower():
                            shared_step_hits[f"{segment_name}|{variant}|{shared_name}"] += 1

            if segment_name == "Roots Segment" and variant:
                active_normalized = normalize_token(variant)
                for grouper in groupers:
                    for step in as_list(grouper.get("steps")):
                        path_text = (step.get("hierarchyPath") or "") + "/" + (step.get("stepName") or "")
                        normalized_path = normalize_token(path_text)
                        for other_variant in as_list(expected_coverage[segment_name].get("expected")):
                            if other_variant == variant:
                                continue
                            other_normalized = normalize_token(other_variant)
                            if other_normalized and other_normalized in normalized_path and active_normalized not in normalized_path:
                                warnings.append(
                                    {
                                        "type": "roots-other-variant-name-in-path",
                                        "source": record["sourceKey"],
                                        "diagnosticDirectory": record["diagnosticName"],
                                        "segment": segment_name,
                                        "variant": variant,
                                        "otherVariant": other_variant,
                                        "path": step.get("hierarchyPath"),
                                    }
                                )

    coverage_report = {}
    for segment_name, info in expected_coverage.items():
        variants = {}
        missing = []
        for variant in as_list(info.get("expected")):
            counts = coverage[segment_name].get(variant, Counter())
            total = sum(counts.values())
            if total == 0:
                missing.append(variant)
            variants[variant] = {
                "total": total,
                "bySource": sorted_counter(counts),
            }
        coverage_report[segment_name] = {
            "expected": as_list(info.get("expected")),
            "covered": len(as_list(info.get("expected"))) - len(missing),
            "missing": missing,
            "variants": variants,
        }
        if missing:
            issues.append(
                {
                    "type": "variant-coverage-missing",
                    "segment": segment_name,
                    "missing": missing,
                }
            )

    status = "pass" if not issues else "fail"
    return {
        "schemaVersion": 1,
        "generatedAt": date.today().isoformat(),
        "status": status,
        "summary": {
            "diagnosticDirectoriesChecked": diagnostics_checked,
            "segmentsChecked": segment_count,
            "groupersChecked": grouper_count,
            "stepsChecked": step_count,
            "catalogItemsRead": catalog_item_count,
            "catalogMaterialsRead": catalog_material_count,
            "objectReferencesRead": reference_count,
            "issueCount": len(issues),
            "warningCount": len(warnings),
        },
        "coverage": coverage_report,
        "sharedStepNameHits": sorted_counter(shared_step_hits),
        "issues": issues,
        "warnings": warnings,
    }


def load_sample_records(map_data_dir: Path, sample_index: dict[str, Any]) -> tuple[list[dict[str, Any]], list[dict[str, Any]]]:
    records: list[dict[str, Any]] = []
    issues: list[dict[str, Any]] = []
    for sample in discover_sample_dirs(map_data_dir, sample_index):
        diagnostic_dir: Path = sample["diagnosticDir"]
        missing = [name for name in REQUIRED_DIAGNOSTIC_FILES if not (diagnostic_dir / name).exists()]
        if missing:
            issues.append(
                {
                    "type": "missing-required-files",
                    "source": sample["sourceKey"],
                    "diagnosticDirectory": sample["diagnosticName"],
                    "missing": missing,
                }
            )
            continue
        try:
            runtime = read_json(diagnostic_dir / "RuntimeExport.json")
            catalog = read_json(diagnostic_dir / "ObjectCatalog.json")
            reference_map = read_json(diagnostic_dir / "ObjectReferenceMap.json")
            name_map = read_json(diagnostic_dir / "NameMap.json")
            generated_children_snapshot = read_generated_children_snapshot_summary(
                diagnostic_dir / "GeneratedChildrenSnapshot.json"
            )
        except Exception as exc:  # pragma: no cover - surfaced in generated report
            issues.append(
                {
                    "type": "json-parse-failed",
                    "source": sample["sourceKey"],
                    "diagnosticDirectory": sample["diagnosticName"],
                    "error": str(exc),
                }
            )
            continue

        if generated_children_snapshot.get("potentiallyDirty"):
            issues.append(
                {
                    "type": "potentially-dirty-generated-children-snapshot",
                    "source": sample["sourceKey"],
                    "diagnosticDirectory": sample["diagnosticName"],
                    "warnings": as_list(generated_children_snapshot.get("warnings")),
                }
            )
            continue

        schema_version = generated_children_snapshot.get("schemaVersion") or 0
        if schema_version < MIN_GENERATED_CHILDREN_SCHEMA_VERSION:
            issues.append(
                {
                    "type": "generated-children-snapshot-schema-too-old",
                    "source": sample["sourceKey"],
                    "diagnosticDirectory": sample["diagnosticName"],
                    "schemaVersion": schema_version,
                    "requiredSchemaVersion": MIN_GENERATED_CHILDREN_SCHEMA_VERSION,
                }
            )
            continue

        if not generated_children_snapshot.get("hasRelationshipCandidates"):
            issues.append(
                {
                    "type": "generated-children-snapshot-missing-relationship-candidates",
                    "source": sample["sourceKey"],
                    "diagnosticDirectory": sample["diagnosticName"],
                    "schemaVersion": schema_version,
                }
            )
            continue

        terrain_randomiser_enabled = has_enabled_external_modifier(
            generated_children_snapshot,
            "TerrainRandomiser",
        )
        source_role = sample.get("sourceRole") or ""
        if source_role.startswith("official-") and terrain_randomiser_enabled:
            issues.append(
                {
                    "type": "terrain-randomiser-sample-in-official-source",
                    "source": sample["sourceKey"],
                    "diagnosticDirectory": sample["diagnosticName"],
                }
            )
            continue

        if "terrain_randomiser" in sample["sourceKey"] and not terrain_randomiser_enabled:
            issues.append(
                {
                    "type": "terrain-randomiser-source-without-terrain-randomiser-marker",
                    "source": sample["sourceKey"],
                    "diagnosticDirectory": sample["diagnosticName"],
                    "sourceClassification": generated_children_snapshot.get("sourceClassification"),
                }
            )
            continue

        records.append(
            {
                **sample,
                "runtime": runtime,
                "catalog": catalog,
                "referenceMap": reference_map,
                "nameMap": name_map,
                "generatedChildrenSnapshot": generated_children_snapshot,
            }
        )
    return records, issues


def main() -> int:
    data_dir = Path(__file__).resolve().parents[1]
    map_data_dir = data_dir / "map-data"
    sample_index_path = map_data_dir / "sample-index.json"
    sample_index = read_json(sample_index_path)
    sample_records, discovery_issues = load_sample_records(map_data_dir, sample_index)

    output_dir = map_data_dir / OUTPUT_DIR_NAME
    snapshot_artifact = build_snapshot_artifact(sample_records)
    registry_artifact = build_registry_artifact(sample_records)
    parent_child_registry_artifact = build_parent_child_registry_artifact(sample_records, registry_artifact)
    regression_report = check_regression(sample_records, sample_index, discovery_issues)

    if regression_report["status"] != "pass":
        write_json(output_dir / "sample-regression-report.pending.json", regression_report)
        print(
            "not-writing-artifacts",
            output_dir,
            "snapshots=" + str(snapshot_artifact["summary"]["snapshotCount"]),
            "templates=" + str(registry_artifact["summary"]["templateCount"]),
            "materials=" + str(registry_artifact["summary"]["materialCount"]),
            "parentChildTemplates=" + str(parent_child_registry_artifact["summary"]["templateCount"]),
            "regression=" + regression_report["status"],
            "pendingReport=" + str(output_dir / "sample-regression-report.pending.json"),
        )
        return 1

    write_json(output_dir / "template-snapshots.json", snapshot_artifact)
    write_json(output_dir / "object-registry-input.json", registry_artifact)
    write_json(output_dir / PARENT_CHILD_REGISTRY_FILE_NAME, parent_child_registry_artifact)
    write_json(output_dir / "sample-regression-report.json", regression_report)

    print(
        "generated",
        output_dir,
        "snapshots=" + str(snapshot_artifact["summary"]["snapshotCount"]),
        "templates=" + str(registry_artifact["summary"]["templateCount"]),
        "materials=" + str(registry_artifact["summary"]["materialCount"]),
        "parentChildTemplates=" + str(parent_child_registry_artifact["summary"]["templateCount"]),
        "regression=" + regression_report["status"],
    )
    return 0 if regression_report["status"] == "pass" else 1


if __name__ == "__main__":
    raise SystemExit(main())
