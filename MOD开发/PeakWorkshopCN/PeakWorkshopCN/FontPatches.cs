using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace PeakWorkshopCN
{
    internal static class FontPatches
    {
        private static Font _font;
        private static TMP_FontAsset _tmp;
        private static FieldInfo _loadFont, _loadMaterial;
        private static TMP_FontAsset _loadOriginalFont;
        private static Material _loadOriginalMaterial;
        private static bool _loadCaptured;
        private static int _retryFrame;
        private static readonly Dictionary<GUIStyle, Font> StyleFonts = new Dictionary<GUIStyle, Font>();
        private static readonly Dictionary<Tuple<object, FieldInfo>, Font> FieldFonts = new Dictionary<Tuple<object, FieldInfo>, Font>();
        private static readonly Dictionary<TMP_Text, Tuple<TMP_FontAsset, Material>> TmpFonts = new Dictionary<TMP_Text, Tuple<TMP_FontAsset, Material>>();
        private static readonly Dictionary<Type, FieldInfo[]> Fields = new Dictionary<Type, FieldInfo[]>();
        private static readonly HashSet<string> Warned = new HashSet<string>();

        internal static void Install(Harmony harmony, Assembly assembly)
        {
            var overlay = assembly.GetType("PeakMapForge.Generation.LoadOverlay");
            _loadFont = AccessTools.Field(overlay, "_font");
            _loadMaterial = AccessTools.Field(overlay, "_fontMaterial");
            foreach (var target in new[]
            {
                "PeakMapForge.Editor.EditorUi:Styles", "PeakMapForge.Teams.TeamUi:FindFonts",
                "PeakMapForge.Airport.WorkshopPanel:FindFonts", "PeakMapForge.Launch.LaunchPanel:Styles",
                "PeakMapForge.Build.ForgeHud:OnGUI", "PeakMapForge.Npc.NpcHud:OnGUI"
            }) Patch(harmony, assembly, target, nameof(StyleFieldsPostfix));
            foreach (var target in new[]
            {
                "PeakMapForge.Teams.TeamUi:Style", "PeakMapForge.Airport.WorkshopPanel:St",
                "PeakMapForge.Airport.WorkshopPanel:_fieldStyle"
            }) Patch(harmony, assembly, target, nameof(StylePostfix));
            Patch(harmony, assembly, "PeakMapForge.Generation.LoadOverlay:FontFor", nameof(TmpFontPostfix));
            Patch(harmony, assembly, "PeakMapForge.Generation.LoadOverlay:NewText", nameof(TmpTextPostfix));
            Patch(harmony, assembly, "PeakMapForge.Airport.MenuSignRig:MakeText", nameof(TmpTextPostfix));
        }

        private static void Patch(Harmony harmony, Assembly assembly, string target, string hook)
        {
            try
            {
                int colon = target.IndexOf(':');
                var type = assembly.GetType(target.Substring(0, colon));
                var method = AccessTools.Method(type, target.Substring(colon + 1));
                if (method == null) { WarnOnce(target, "Font hook not found: " + target); return; }
                harmony.Patch(method, postfix: new HarmonyMethod(typeof(FontPatches), hook));
            }
            catch (Exception ex) { WarnOnce(target, "Font hook skipped: " + target + ": " + ex.Message); }
        }

        private static Font ChineseFont()
        {
            if (_font != null) return _font;
            if (Time.frameCount < _retryFrame) return null;
            _retryFrame = Time.frameCount + 300;
            try
            {
                var available = new HashSet<string>(Font.GetOSInstalledFontNames(), StringComparer.OrdinalIgnoreCase);
                foreach (var name in new[] { "Microsoft YaHei", "SimHei", "SimSun" })
                {
                    if (!available.Contains(name)) continue;
                    var font = Font.CreateDynamicFontFromOSFont(name, 16);
                    font.RequestCharactersInTexture("中文工坊地图设置保存ABC012", 16);
                    if (!font.HasCharacter('中') || !font.HasCharacter('文')) { UnityEngine.Object.Destroy(font); continue; }
                    font.hideFlags = HideFlags.HideAndDontSave;
                    _font = font;
                    return font;
                }
                WarnOnce("imgui", "No Chinese IMGUI font was available; original font retained.");
            }
            catch (Exception ex) { WarnOnce("imgui", "Chinese font initialization: " + ex.Message); }
            return null;
        }

        private static void Apply(GUIStyle style, Font font)
        {
            if (style == null || font == null) return;
            if (!StyleFonts.ContainsKey(style)) StyleFonts.Add(style, style.font);
            style.font = font;
        }

        private static void StyleFieldsPostfix(object __instance, MethodBase __originalMethod)
        {
            if (!Plugin.Chinese) return;
            var font = ChineseFont();
            if (font == null) return;
            var type = __originalMethod.DeclaringType;
            if (!Fields.TryGetValue(type, out var fields)) Fields[type] = fields = type.GetFields(AccessTools.allDeclared);
            foreach (var field in fields)
            {
                if (field.IsInitOnly || (!field.IsStatic && __instance == null)) continue;
                object owner = field.IsStatic ? null : __instance;
                if (field.FieldType == typeof(Font))
                {
                    var key = Tuple.Create(owner, field);
                    if (!FieldFonts.ContainsKey(key)) FieldFonts.Add(key, field.GetValue(owner) as Font);
                    field.SetValue(owner, font);
                }
                else if (field.FieldType == typeof(GUIStyle))
                {
                    var style = field.GetValue(owner) as GUIStyle;
                    if (style != null && style.font == _font && !StyleFonts.ContainsKey(style))
                        StyleFonts.Add(style, OriginalField(type, owner, type.Name == "EditorUi" ? "_fontBody" : "_body"));
                    Apply(style, font);
                }
                else if (field.FieldType == typeof(GUISkin) && field.Name == "_skin")
                {
                    var skin = field.GetValue(owner) as GUISkin;
                    if (skin == null) continue;
                    foreach (var style in new[] { skin.label, skin.button, skin.textField, skin.textArea, skin.box, skin.toggle }) Apply(style, font);
                    foreach (var style in skin.customStyles) Apply(style, font);
                }
            }
        }

        private static Font OriginalField(Type type, object owner, string name)
        {
            var field = AccessTools.Field(type, name);
            if (field == null) return null;
            owner = field.IsStatic ? null : owner;
            return FieldFonts.TryGetValue(Tuple.Create(owner, field), out var font) ? font : field.GetValue(owner) as Font;
        }

        private static void StylePostfix(GUIStyle __result, object __instance, MethodBase __originalMethod, object[] __args)
        {
            if (!Plugin.Chinese || __result == null) return;
            if (__result.font == _font && !StyleFonts.ContainsKey(__result))
            {
                var type = __originalMethod.DeclaringType;
                string field = type.Name == "TeamUi" ? ((bool)__args[2] ? "_title" : "_body") :
                    (__originalMethod.Name == "St" ? (__args[0].ToString() == "Title" ? "_fTitle" : __args[0].ToString() == "Hand" ? "_fHand" : "_fBody") : "_fBody");
                var original = OriginalField(type, __instance, field) ?? OriginalField(type, __instance, type.Name == "TeamUi" ? "_body" : "_fBody");
                StyleFonts.Add(__result, original);
            }
            Apply(__result, ChineseFont());
        }

        private static TMP_FontAsset ChineseTmpFont()
        {
            if (_tmp != null) return _tmp;
            try
            {
                var gui = GUIManager.instance;
                if (gui != null)
                {
                    var ascent = gui.GetComponentInChildren<AscentUI>(true);
                    if (Covers(ascent?.text?.font)) return _tmp = ascent.text.font;
                    if (Covers(gui.heroDayText?.font)) return _tmp = gui.heroDayText.font;
                }
                var swapper = FontFallbackSwapper.instance;
                if (swapper != null)
                {
                    if (Covers(swapper.mainBaseFont)) return _tmp = swapper.mainBaseFont;
                    if (Covers(swapper.simplifiedChineseFont)) return _tmp = swapper.simplifiedChineseFont;
                }
                var text = UnityEngine.Object.FindAnyObjectByType<TextMeshProUGUI>();
                if (Covers(text?.font)) return _tmp = text.font;
                if (Covers(TMP_Settings.defaultFontAsset)) return _tmp = TMP_Settings.defaultFontAsset;
            }
            catch (Exception ex) { WarnOnce("tmp", "Chinese TMP font lookup: " + ex.Message); }
            return null;
        }

        private static bool Covers(TMP_FontAsset font) => font != null && font.HasCharacters("中文工坊地图设置ABC012", out uint[] missing, true, true);

        private static void TmpFontPostfix(ref TMP_FontAsset __result, ref Material material)
        {
            if (!Plugin.Chinese) return;
            var font = ChineseTmpFont();
            if (font == null) return;
            if (!_loadCaptured || (__result != font && __result != _tmp))
            {
                _loadOriginalFont = __result;
                _loadOriginalMaterial = material;
                _loadCaptured = true;
            }
            _loadFont?.SetValue(null, font);
            _loadMaterial?.SetValue(null, font.material);
            __result = font;
            material = font.material;
        }

        private static void TmpTextPostfix(object __result, MethodBase __originalMethod)
        {
            if (!(__result is TMP_Text text)) return;
            if (!TmpFonts.ContainsKey(text))
                TmpFonts.Add(text, __originalMethod.DeclaringType.Name == "LoadOverlay" && text.font == _tmp && _loadCaptured
                    ? Tuple.Create(_loadOriginalFont, _loadOriginalMaterial) : Tuple.Create(text.font, text.fontSharedMaterial));
            if (Plugin.Chinese) ApplyTmp(text, ChineseTmpFont());
        }

        private static void ApplyTmp(TMP_Text text, TMP_FontAsset font)
        {
            if (text == null || font == null) return;
            text.font = font;
            text.fontSharedMaterial = font.material;
        }

        internal static void Restore()
        {
            foreach (var pair in StyleFonts) pair.Key.font = pair.Value;
            foreach (var pair in FieldFonts)
            {
                if (pair.Key.Item1 is UnityEngine.Object obj && obj == null) continue;
                pair.Key.Item2.SetValue(pair.Key.Item1, pair.Value);
            }
            foreach (var pair in TmpFonts)
                if (pair.Key != null) { pair.Key.font = pair.Value.Item1; pair.Key.fontSharedMaterial = pair.Value.Item2; }
            if (_loadCaptured)
            {
                _loadFont?.SetValue(null, _loadOriginalFont);
                _loadMaterial?.SetValue(null, _loadOriginalMaterial);
            }
        }

        internal static void Invalidate() { _tmp = null; _retryFrame = 0; }
        internal static void RefreshTracked()
        {
            var stale = new List<TMP_Text>();
            foreach (var text in TmpFonts.Keys) if (text == null) stale.Add(text);
            foreach (var text in stale) TmpFonts.Remove(text);
            if (!Plugin.Chinese) return;
            var font = ChineseTmpFont();
            foreach (var text in TmpFonts.Keys) ApplyTmp(text, font);
            if (_loadCaptured && font != null)
            {
                _loadFont?.SetValue(null, font);
                _loadMaterial?.SetValue(null, font.material);
            }
        }
        internal static void Dispose()
        {
            Restore();
            StyleFonts.Clear();
            FieldFonts.Clear();
            TmpFonts.Clear();
            Fields.Clear();
            if (_font != null) UnityEngine.Object.Destroy(_font);
            _font = null;
            _tmp = null;
        }
        private static void WarnOnce(string key, string message) { if (Warned.Add(key)) Plugin.Warn(message); }
    }
}
