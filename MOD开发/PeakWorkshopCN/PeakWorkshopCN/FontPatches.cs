using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace PeakWorkshopCN
{
    internal static class FontPatches
    {
        private sealed class StyleState { internal Font Original; }
        private static ConditionalWeakTable<GUIStyle, StyleState> Styles = new ConditionalWeakTable<GUIStyle, StyleState>();
        private static readonly List<WeakReference> Tracked = new List<WeakReference>();
        private static readonly Dictionary<TMP_Text, Tuple<TMP_FontAsset, Material>> Texts = new Dictionary<TMP_Text, Tuple<TMP_FontAsset, Material>>();
        private static readonly HashSet<string> Warned = new HashSet<string>();
        private static Font _font;
        private static TMP_FontAsset _tmp, _loadOriginalFont;
        private static Material _loadOriginalMaterial;
        private static bool _loadCaptured;
        private static int _fontRetry, _tmpRetry;
        private static Action<object, TMP_FontAsset> _setLoadFont;
        private static Action<object, Material> _setLoadMaterial;
        private static Assembly _assembly;

        internal static void Install(Harmony harmony, Assembly assembly)
        {
            _assembly = assembly;
            var overlay = assembly.GetType("PeakMapForge.Generation.LoadOverlay", true);
            _setLoadFont = BoundAccess.Setter<TMP_FontAsset>(AccessTools.Field(overlay, "_font"));
            _setLoadMaterial = BoundAccess.Setter<Material>(AccessTools.Field(overlay, "_fontMaterial"));
            Patch(harmony, assembly, "PeakMapForge.Generation.LoadOverlay", "FontFor", nameof(TmpFontPostfix));
            Patch(harmony, assembly, "PeakMapForge.Generation.LoadOverlay", "NewText", nameof(TmpTextPostfix));
            Patch(harmony, assembly, "PeakMapForge.Airport.MenuSignRig", "MakeText", nameof(TmpTextPostfix));
        }

        private static void Patch(Harmony harmony, Assembly assembly, string name, string method, string hook)
        {
            var target = AccessTools.Method(assembly.GetType(name, true), method);
            if (target == null) throw new MissingMethodException(name, method);
            harmony.Patch(target, postfix: new HarmonyMethod(typeof(FontPatches), hook));
        }

        // Called by the common per-method transpiler, only while installing patches.
        internal static void Rewrite(List<CodeInstruction> instructions)
        {
            foreach (var instruction in instructions)
            {
                if (!(instruction.operand is MethodBase method) || method.DeclaringType != typeof(GUIStyle)) continue;
                MethodInfo replacement = null;
                if (instruction.opcode == OpCodes.Newobj)
                {
                    var p = method.GetParameters();
                    if (p.Length == 0) replacement = AccessTools.Method(typeof(FontPatches), nameof(CreateEmpty));
                    else if (p.Length == 1 && p[0].ParameterType == typeof(GUIStyle)) replacement = AccessTools.Method(typeof(FontPatches), nameof(CreateCopy));
                    else if (p.Length == 1 && p[0].ParameterType == typeof(string)) replacement = AccessTools.Method(typeof(FontPatches), nameof(CreateNamed));
                }
                else if (method.Name == "set_font") replacement = AccessTools.Method(typeof(FontPatches), nameof(SetFont));
                else if (method.Name == "get_font") replacement = AccessTools.Method(typeof(FontPatches), nameof(GetOriginalFont));
                if (replacement != null) { instruction.opcode = OpCodes.Call; instruction.operand = replacement; }
            }
        }

        public static GUIStyle CreateEmpty() => Register(new GUIStyle(), null, false);
        public static GUIStyle CreateCopy(GUIStyle source) => Register(new GUIStyle(source), GetOriginalFont(source), true);
        public static GUIStyle CreateNamed(string name) => Register(new GUIStyle(name), null, false);
        private static GUIStyle Register(GUIStyle style, Font original, bool explicitOriginal)
        {
            if (!Styles.TryGetValue(style, out var state))
            {
                Styles.Add(style, state = new StyleState { Original = explicitOriginal ? original : style.font });
                Tracked.Add(new WeakReference(style));
            }
            Apply(style, state);
            return style;
        }
        public static void SetFont(GUIStyle style, Font original)
        {
            if (!Styles.TryGetValue(style, out var state)) { Register(style, original, true); state = Styles.GetOrCreateValue(style); }
            state.Original = original;
            Apply(style, state);
        }
        public static Font GetOriginalFont(GUIStyle style) => Styles.TryGetValue(style, out var state) ? state.Original : style.font;
        private static void Apply(GUIStyle style, StyleState state)
        {
            var desired = Plugin.Chinese ? ChineseFont() ?? state.Original : state.Original;
            if (style.font != desired) style.font = desired;
        }
        private static Font ChineseFont()
        {
            if (_font != null) return _font;
            if (Time.frameCount < _fontRetry) return null;
            _fontRetry = Time.frameCount + 300;
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
                    return _font = font;
                }
                WarnOnce("imgui", "No Chinese IMGUI font available; original font retained.");
            }
            catch (Exception ex) { WarnOnce("imgui", ex.Message); }
            return null;
        }

        private static TMP_FontAsset ChineseTmpFont()
        {
            if (_tmp != null) return _tmp;
            if (Time.frameCount < _tmpRetry) return null;
            _tmpRetry = Time.frameCount + 300;
            try
            {
                var swapper = FontFallbackSwapper.instance;
                if (swapper != null && Covers(swapper.simplifiedChineseFont)) return _tmp = swapper.simplifiedChineseFont;
                var gui = GUIManager.instance;
                if (gui != null)
                {
                    var ascent = gui.GetComponentInChildren<AscentUI>(true);
                    if (Covers(ascent?.text?.font)) return _tmp = ascent.text.font;
                    if (Covers(gui.heroDayText?.font)) return _tmp = gui.heroDayText.font;
                }
                if (swapper != null && Covers(swapper.mainBaseFont)) return _tmp = swapper.mainBaseFont;
                if (Covers(TMP_Settings.defaultFontAsset)) return _tmp = TMP_Settings.defaultFontAsset;
                WarnOnce("tmp", "Chinese TMP font is not ready; lookup will retry.");
            }
            catch (Exception ex) { WarnOnce("tmp", ex.Message); }
            return null;
        }
        private static bool Covers(TMP_FontAsset font) => font != null && font.HasCharacters("中文工坊地图设置ABC012", out uint[] missing, true, true);
        private static void TmpFontPostfix(ref TMP_FontAsset __result, ref Material material)
        {
            if (!_loadCaptured || __result != _tmp)
            { _loadOriginalFont = __result; _loadOriginalMaterial = material; _loadCaptured = true; }
            if (!Plugin.Chinese) return;
            var font = ChineseTmpFont();
            if (font == null) return;
            if (__result != font) _setLoadFont(null, font);
            if (material != font.material) _setLoadMaterial(null, font.material);
            __result = font; material = font.material;
        }
        private static void TmpTextPostfix(object __result)
        {
            if (!(__result is TMP_Text text)) return;
            if (!Texts.ContainsKey(text)) Texts.Add(text, text.font == _tmp && _loadCaptured ? Tuple.Create(_loadOriginalFont, _loadOriginalMaterial) : Tuple.Create(text.font, text.fontSharedMaterial));
            if (Plugin.Chinese) ApplyTmp(text, ChineseTmpFont());
        }
        private static void ApplyTmp(TMP_Text text, TMP_FontAsset font)
        {
            if (text == null || font == null) return;
            if (text.font != font) text.font = font;
            if (text.fontSharedMaterial != font.material) text.fontSharedMaterial = font.material;
        }

        // Event-only seeding covers styles constructed before the plugin installed.
        internal static void SeedExisting()
        {
            if (_assembly == null) return;
            foreach (var name in new[] { "PeakMapForge.Editor.EditorUi", "PeakMapForge.Airport.WorkshopPanel", "PeakMapForge.Teams.TeamUi", "PeakMapForge.Launch.LaunchPanel", "PeakMapForge.Build.ForgeHud" })
            {
                var type = _assembly.GetType(name);
                if (type == null) continue;
                foreach (var field in type.GetFields(AccessTools.allDeclared))
                {
                    if (field.FieldType != typeof(GUIStyle) && field.FieldType != typeof(GUISkin) && !typeof(IDictionary).IsAssignableFrom(field.FieldType)) continue;
                    if (field.IsStatic) Seed(field.GetValue(null));
                    else if (typeof(UnityEngine.Object).IsAssignableFrom(type)) foreach (var owner in Resources.FindObjectsOfTypeAll(type)) Seed(field.GetValue(owner));
                }
            }
        }
        private static void Seed(object value)
        {
            if (value is GUIStyle style) Register(style, null, false);
            else if (value is IDictionary dictionary) { foreach (var item in dictionary.Values) if (item is GUIStyle s) Register(s, null, false); }
            else if (value is GUISkin skin)
            {
                foreach (var s in new[] { skin.label, skin.button, skin.textField, skin.textArea, skin.box, skin.toggle }) Register(s, null, false);
                foreach (var s in skin.customStyles) Register(s, null, false);
            }
        }
        internal static void Restore()
        {
            foreach (var weak in Tracked) if (weak.Target is GUIStyle style && Styles.TryGetValue(style, out var state) && style.font != state.Original) style.font = state.Original;
            foreach (var pair in Texts) if (pair.Key != null) { pair.Key.font = pair.Value.Item1; pair.Key.fontSharedMaterial = pair.Value.Item2; }
            if (_loadCaptured) { _setLoadFont?.Invoke(null, _loadOriginalFont); _setLoadMaterial?.Invoke(null, _loadOriginalMaterial); }
        }
        internal static void Invalidate() { _tmp = null; _fontRetry = _tmpRetry = 0; }
        internal static void RefreshTracked()
        {
            Tracked.RemoveAll(w => !w.IsAlive);
            foreach (var weak in Tracked) if (weak.Target is GUIStyle style && Styles.TryGetValue(style, out var state)) Apply(style, state);
            var stale = new List<TMP_Text>();
            foreach (var text in Texts.Keys) if (text == null) stale.Add(text);
            foreach (var text in stale) Texts.Remove(text);
            if (Plugin.Chinese) foreach (var text in Texts.Keys) ApplyTmp(text, ChineseTmpFont());
        }
        internal static void Dispose()
        {
            Restore(); Tracked.Clear(); Styles = new ConditionalWeakTable<GUIStyle, StyleState>(); Texts.Clear();
            if (_font != null) UnityEngine.Object.Destroy(_font);
            _font = null; _tmp = null; _assembly = null;
        }
        private static void WarnOnce(string key, string message) { if (Warned.Add(key)) Plugin.Warn("Font: " + message); }
    }
}
