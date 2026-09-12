"""Read-only PEAK asset audit; requires UnityPy and TypeTreeGeneratorAPI."""
import argparse
import json
from pathlib import Path

import UnityPy
from UnityPy.classes.PPtr import PPtr
from UnityPy.helpers.TypeTreeGenerator import TypeTreeGenerator


def audit(data_path):
    env = UnityPy.load(str(data_path / "level3"))
    generator = TypeTreeGenerator(env.file.unity_version)
    generator.load_local_dll_folder(str(data_path / "Managed"))
    env.typetree_generator = generator
    objects = list(env.objects)
    resume = next(o.read() for o in objects if o.type.name == "GameObject"
                  and o.read().m_Name == "UI_MainMenuButton_Resume")
    canvases = [o.read_typetree() for o in objects if o.type.name == "Canvas"
                and o.read().m_GameObject.read().m_Name == "PauseMenu"]
    calls = []
    text = None

    def visit(game_object):
        nonlocal text
        for component in game_object.m_Component:
            obj = component.component.deref()
            if obj.type.name == "MonoBehaviour":
                tree = obj.read_typetree()
                if "m_OnClick" in tree:
                    calls.extend(tree["m_OnClick"]["m_PersistentCalls"]["m_Calls"])
                if "m_fontAsset" in tree:
                    text = (obj, tree)
            elif obj.type.name == "RectTransform":
                for child in obj.read().m_Children:
                    visit(child.read().m_GameObject.read())

    visit(resume)
    assert text is not None, "Resume template has no TMP label"
    owner, label = text
    font = PPtr(**label["m_fontAsset"], assetsfile=owner.assets_file).read_typetree()
    characters = {c["m_Unicode"]: c for c in font["m_CharacterTable"]}
    glyphs = {g["m_Index"]: g for g in font["m_GlyphTable"]}
    face = font["m_FaceInfo"]
    measured = []
    for value, old_width, new_width in [("UNFAVORITE", 116, 176), ("RENAME", 80, 128), ("DELETE", 80, 112)]:
        width = sum(glyphs[characters[ord(c)]["m_GlyphIndex"]]["m_Metrics"]["m_HorizontalAdvance"]
                    for c in value) * 20 * face["m_Scale"] / face["m_PointSize"]
        assert width <= new_width - 32, f"New label area too narrow for {value}"
        measured.append(dict(text=value, advance_at_20=round(width, 3), old_area=old_width - 8,
                             new_area=new_width - 32))
    return dict(unity_version=env.file.unity_version,
                pause_canvas_orders=[c["m_SortingOrder"] for c in canvases],
                old_modal_order=100,
                inherited_calls=[c["m_MethodName"] for c in calls],
                font=face["m_FamilyName"], labels=measured,
                scope="Serialized asset and glyph-metric checks, not live Unity UI verification")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("data_path", type=Path)
    args = parser.parse_args()
    print(json.dumps(audit(args.data_path), ensure_ascii=False, indent=2))
