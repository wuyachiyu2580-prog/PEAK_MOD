using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace WhySoLaggy
{
    // Private to this plugin: no ModConfig/PEAKLib assembly dependency or shared state.
    internal static class ModConfigUiAdapter
    {
        private const string Guid = "com.github.PEAKModding.PEAKLib.ModConfig";
        private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        private static readonly HashSet<MethodBase> Patched = new HashSet<MethodBase>();
        private static readonly HashSet<string> Warnings = new HashSet<string>();
        private static readonly Dictionary<Tuple<Type, string>, MemberInfo> MemberCache = new Dictionary<Tuple<Type, string>, MemberInfo>();
        private static string _fileName, _modName;
        private static Func<ConfigEntryBase, string> _name;
        private static Func<string, string> _token;
        private static Action<string> _warn;
        private static Type _inputCell, _row, _tab;
        private static MonoBehaviour _menu;
        private static Coroutine _refresh;

        internal static void Install(Harmony harmony, string fileName, string modName,
            Func<ConfigEntryBase, string> name, Func<string, string> token, Action<string> warn)
        {
            _fileName = fileName; _modName = modName; _name = name; _token = token; _warn = warn;
            if (harmony == null || !Chainloader.PluginInfos.TryGetValue(Guid, out var info) || info?.Instance == null) return;
            Assembly assembly = info.Instance.GetType().Assembly;
            foreach (Type type in assembly.GetTypes().Where(t => t.FullName != null &&
                t.FullName.StartsWith("PEAKLib.ModConfig.SettingOptions.BepInEx", StringComparison.Ordinal) && !t.ContainsGenericParameters && !t.IsAbstract))
                Patch(harmony, DeclaredImplementation(type, "GetDisplayName", Type.EmptyTypes), nameof(DisplayNamePostfix));

            Type menu = assembly.GetType("PEAKLib.ModConfig.Components.ModSettingsMenu") ??
                assembly.GetType("PEAKLib.ModConfig.Components.ModdedSettingsMenu");
            if (menu == null) { Warn("menu", "Unsupported ModConfig menu type."); return; }
            foreach (string method in new[] { "OnEnable", "ShowSettings", "SetSection", "UpdateSectionTabs" })
                Patch(harmony, DeclaredImplementation(menu, method,
                    method == "SetSection" || method == "UpdateSectionTabs" ? new[] { typeof(string) } : Type.EmptyTypes), nameof(MenuPostfix));
            foreach (string method in new[] { "OnDisable", "OnDestroy" })
            {
                MethodInfo existing = DeclaredImplementation(menu, method, Type.EmptyTypes);
                if (existing != null) Patch(harmony, existing, nameof(MenuClosedPostfix));
            }
            _inputCell = AccessTools.TypeByName("Zorro.Settings.SettingInputUICell");
            _row = AccessTools.TypeByName("SettingsUICell");
            _tab = assembly.GetType("PEAKLib.ModConfig.Components.ModdedTABSButton");
            Type enumUi = AccessTools.TypeByName("Zorro.Settings.UI.EnumSettingUI");
            Patch(harmony, DeclaredImplementation(enumUi, "RefreshText", Type.EmptyTypes), nameof(ControlPostfix));
            foreach (Type type in assembly.GetTypes().Where(t => _inputCell != null && _inputCell.IsAssignableFrom(t) && !t.IsAbstract))
                foreach (MethodInfo method in type.GetMethods(Members).Where(m => m.Name == "Setup" && m.GetParameters().Length == 2))
                    Patch(harmony, method, nameof(ControlPostfix));
        }

        internal static MethodInfo DeclaredImplementation(Type type, string name, Type[] parameters)
        {
            for (; type != null; type = type.BaseType)
            {
                MethodInfo method = type.GetMethod(name, Members, null, parameters, null);
                if (method != null) return method.ContainsGenericParameters || method.IsAbstract ? null : method;
            }
            return null;
        }

        private static void Patch(Harmony harmony, MethodInfo method, string callback)
        {
            if (method == null) { Warn(callback, "A ModConfig hook is unavailable: " + callback); return; }
            if (Patched.Contains(method)) return;
            try
            {
                harmony.Patch(method, postfix: new HarmonyMethod(typeof(ModConfigUiAdapter), callback));
                Patched.Add(method);
            }
            catch (Exception ex) { Warn(method.ToString(), "ModConfig hook skipped: " + method + ": " + ex.Message); }
        }

        internal static bool Owns(ConfigEntryBase entry, string fileName)
        {
            return entry?.ConfigFile != null && !string.IsNullOrEmpty(fileName) &&
                string.Equals(Path.GetFileName(entry.ConfigFile.ConfigFilePath), fileName, StringComparison.OrdinalIgnoreCase);
        }

        internal static ConfigEntryBase Entry(object setting)
        {
            if (setting == null) return null;
            foreach (Type contract in setting.GetType().GetInterfaces())
            {
                PropertyInfo property = contract.GetProperty("ConfigBase", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (property != null && typeof(ConfigEntryBase).IsAssignableFrom(property.PropertyType))
                    return property.GetValue(setting, null) as ConfigEntryBase;
            }
            return Read(setting, "ConfigBase") as ConfigEntryBase;
        }

        private static void DisplayNamePostfix(object __instance, ref string __result)
        {
            try
            {
                ConfigEntryBase entry = Entry(__instance);
                if (!Owns(entry, _fileName)) return;
                string text = _name(entry);
                if (!string.IsNullOrEmpty(text)) __result = text;
            }
            catch (Exception ex) { Warn("name", ex.Message); }
        }

        private static void MenuPostfix(MonoBehaviour __instance, MethodBase __originalMethod)
        {
            // Unity can stop coroutines on disable without executing their finally block.
            if (_menu != __instance || __originalMethod.Name == "OnEnable") StopRefresh();
            _menu = __instance;
            RefreshVisibleUi();
        }

        private static void MenuClosedPostfix(MonoBehaviour __instance)
        {
            if (_menu != __instance) return;
            StopRefresh(); _menu = null;
        }

        internal static void RefreshVisibleUi()
        {
            if (_menu == null || !_menu.isActiveAndEnabled) { StopRefresh(); _menu = null; return; }
            if (_refresh == null) _refresh = _menu.StartCoroutine(DeferredRefresh(_menu));
        }

        private static IEnumerator DeferredRefresh(MonoBehaviour owner)
        {
            yield return null;
            try
            {
                if (owner == null || owner != _menu || !owner.isActiveAndEnabled) yield break;
                RefreshMenu(owner);
            }
            finally { if (owner == _menu) _refresh = null; }
        }

        private static void RefreshMenu(MonoBehaviour owner)
        {
            try
            {
                RefreshTabs(owner, "ModTabController", false);
                if (SameMod(Read(owner, "selectedMod") as string, _modName)) RefreshTabs(owner, "SectionTabController", true);
                Transform content = Read(owner, "Content") as Transform;
                if (content == null || _inputCell == null) return;
                foreach (Component control in content.GetComponentsInChildren(_inputCell, false)) RefreshControl(control);
            }
            catch (Exception ex) { Warn("menu-refresh", ex.Message); }
        }

        internal static bool SameMod(string category, string modName)
        {
            return !string.IsNullOrEmpty(category) && !string.IsNullOrEmpty(modName) &&
                string.Equals(category.Replace(" ", ""), modName.Replace(" ", ""), StringComparison.OrdinalIgnoreCase);
        }

        private static void RefreshTabs(MonoBehaviour menu, string controllerName, bool sections)
        {
            try
            {
                if (_tab == null || !(Read(Read(menu, controllerName), "Tabs") is IEnumerable tabs)) return;
                foreach (GameObject tab in tabs)
                {
                    if (tab == null) continue;
                    Component button = tab.GetComponent(_tab);
                    string category = Read(button, "category") as string;
                    if (!sections && !SameMod(category, _modName)) continue;
                    SetText(Read(button, "text"), _token(sections ? category : _modName));
                }
            }
            catch (Exception ex) { Warn("tabs", ex.Message); }
        }

        private static void ControlPostfix(Component __instance) { RefreshControl(__instance); }

        private static void RefreshControl(Component control)
        {
            try
            {
                if (control == null || !control.gameObject.activeInHierarchy) return;
                object setting = Read(control, "KeySetting") ?? Read(control, "_listeningSetting");
                ConfigEntryBase entry = Entry(setting);
                if (!Owns(entry, _fileName)) return;
                if (_row != null)
                {
                    Component row = control.GetComponentInParent(_row);
                    if (row != null)
                    {
                        string title = _name(entry);
                        if (!string.IsNullOrEmpty(title))
                        {
                            object localized = Read(row, "localizedText");
                            if (localized != null) Write(localized, "autoSet", false);
                            SetText(Read(row, "m_text"), title);
                        }
                    }
                }
                // bool On/Off is owned by the game. Only translate this plugin's enum/list labels.
                if (!entry.SettingType.IsEnum && !(entry.Description.AcceptableValues is AcceptableValueList<string>)) return;
                object dropdown = Read(control, "dropdown");
                if (dropdown == null || !(Read(dropdown, "options") is IList options)) return;
                MethodInfo choices = DeclaredImplementation(setting.GetType(), "GetUnlocalizedChoices", Type.EmptyTypes);
                if (!(choices?.Invoke(setting, null) is IEnumerable<string> values)) return;
                string[] raw = values.ToArray();
                if (raw.Length != options.Count) return;
                ApplyOptionText(options, raw, _token);
                DeclaredImplementation(dropdown.GetType(), "RefreshShownValue", Type.EmptyTypes)?.Invoke(dropdown, null);
                // TMP tracks only instantiated option rows here (not the inactive template).
                if (Read(dropdown, "m_Items") is IList items && items.Count == raw.Length)
                    for (int i = 0; i < items.Count; i++) SetText(Read(items[i], "text"), Read(options[i], "text") as string);
            }
            catch (Exception ex) { Warn("control", ex.Message); }
        }

        internal static void ApplyOptionText(IList options, string[] raw, Func<string, string> translate)
        {
            if (options == null || raw == null || options.Count != raw.Length) return;
            for (int i = 0; i < raw.Length; i++) SetText(options[i], translate(raw[i]) ?? raw[i]);
        }

        private static void SetText(object target, string text)
        {
            if (target != null && !string.IsNullOrEmpty(text) && !Equals(Read(target, "text"), text)) Write(target, "text", text);
        }

        private static MemberInfo Member(Type type, string name)
        {
            var key = Tuple.Create(type, name);
            if (MemberCache.TryGetValue(key, out MemberInfo result)) return result;
            for (Type current = type; current != null; current = current.BaseType)
            {
                result = (MemberInfo)current.GetProperty(name, Members) ?? current.GetField(name, Members);
                if (result != null) break;
            }
            MemberCache[key] = result;
            return result;
        }

        private static object Read(object target, string name)
        {
            if (target == null) return null;
            MemberInfo member = Member(target.GetType(), name);
            return member is PropertyInfo property ? property.GetValue(target, null) : (member as FieldInfo)?.GetValue(target);
        }

        private static void Write(object target, string name, object value)
        {
            MemberInfo member = Member(target.GetType(), name);
            if (member is PropertyInfo property) property.SetValue(target, value, null);
            else if (member is FieldInfo field) field.SetValue(target, value);
        }

        private static void StopRefresh()
        {
            if (!ReferenceEquals(_refresh, null) && !ReferenceEquals(_menu, null) && _menu != null)
                _menu.StopCoroutine(_refresh);
            _refresh = null;
        }

        internal static void Shutdown()
        {
            StopRefresh(); _menu = null;
            Patched.Clear(); Warnings.Clear(); MemberCache.Clear();
            _name = null; _token = null; _warn = null;
        }

        private static void Warn(string key, string message)
        {
            if (Warnings.Add(key)) _warn?.Invoke("ModConfig localization: " + message);
        }

        internal sealed class ActionFieldSubscription : IDisposable
        {
            private FieldInfo _field;
            private Action _handler;
            internal void Subscribe(FieldInfo field, Action handler)
            {
                if (field == null || field.FieldType != typeof(Action) || !field.IsStatic || handler == null) return;
                if (_field == field && _handler == handler) return;
                Dispose();
                field.SetValue(null, (Action)field.GetValue(null) + handler);
                _field = field; _handler = handler;
            }
            public void Dispose()
            {
                if (_field != null) _field.SetValue(null, (Action)_field.GetValue(null) - _handler);
                _field = null; _handler = null;
            }
        }
    }
}
