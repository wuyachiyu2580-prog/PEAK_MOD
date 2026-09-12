using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BepInEx.Logging;
using UnityEngine;

namespace ModConfigDiagnostics
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class ModConfigDiagnosticsPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.wuyachiyu.ModConfigDiagnostics";
        public const string PluginName = "ModConfig Diagnostics";
        public const string PluginVersion = "0.1.0";

        private ConfigEntry<KeyCode> _dumpKey;
        private ConfigEntry<bool> _scanUi;
        private ConfigEntry<int> _maxUiLines;
        private float _nextDumpAllowed;

        private void Awake()
        {
            _dumpKey = Config.Bind("Diagnostics", "DumpKey", KeyCode.F9,
                "Key that writes a read-only ModConfig diagnostics report.");
            _scanUi = Config.Bind("Diagnostics", "ScanUi", true,
                "Include active TextMeshPro UI objects and suspicious text in the report.");
            _maxUiLines = Config.Bind("Diagnostics", "MaxUiLines", 400,
                new ConfigDescription("Maximum UI lines written to one report.",
                    new AcceptableValueRange<int>(50, 2000)));

            Logger.LogInfo("ModConfig Diagnostics loaded. Press " + _dumpKey.Value + " to write a report.");
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextDumpAllowed || !Input.GetKeyDown(_dumpKey.Value))
            {
                return;
            }

            _nextDumpAllowed = Time.unscaledTime + 1f;
            try
            {
                string path = WriteReport();
                Logger.LogInfo("[MCD] Report written: " + path);
            }
            catch (Exception ex)
            {
                Logger.LogError("[MCD] Report failed: " + ex);
            }
        }

        private string WriteReport()
        {
            StringBuilder report = new StringBuilder(32768);
            Action<string> line = value => report.AppendLine(value ?? "");

            line("ModConfig Diagnostics");
            line("GeneratedUtc=" + DateTime.UtcNow.ToString("O"));
            line("GameVersion=" + Application.version);
            line("UnityVersion=" + Application.unityVersion);
            line("PluginPath=" + Paths.PluginPath);
            line("");

            DumpRuntimeTypes(line);
            DumpLoadedPlugins(line);
            DumpDiskAssemblies(line);
            DumpConfigEntries(line);
            DumpSettingsHandler(line);
            DumpSectionTracker(line);
            if (_scanUi.Value)
            {
                DumpUi(line);
            }

            string path = Path.Combine(Paths.BepInExRootPath, "ModConfigDiagnostics-latest.txt");
            File.WriteAllText(path, report.ToString(), new UTF8Encoding(false));
            return path;
        }

        private static void DumpRuntimeTypes(Action<string> line)
        {
            line("[Runtime]");
            line("ModConfig=" + PluginVersionOf("com.github.PEAKModding.PEAKLib.ModConfig"));
            line("PEAKLib.UI=" + PluginVersionOf("com.github.PEAKModding.PEAKLib.UI"));
            line("PEAKLib.Core=" + PluginVersionOf("com.github.PEAKModding.PEAKLib.Core"));
            line("Type.ModSettingsMenu=" + FindType("PEAKLib.ModConfig.Components.ModSettingsMenu"));
            line("Type.ModdedSettingsMenu=" + FindType("PEAKLib.ModConfig.Components.ModdedSettingsMenu"));
            line("Type.PeakHorizontalTabs=" + FindType("PEAKLib.UI.Elements.PeakHorizontalTabs"));
            line("Type.PeakDropdown=" + FindType("PEAKLib.UI.Elements.PeakDropdown"));
            line("");
        }

        private static void DumpLoadedPlugins(Action<string> line)
        {
            line("[LoadedPlugins]");
            foreach (PluginInfo plugin in Chainloader.PluginInfos.Values.OrderBy(p => p.Metadata.GUID, StringComparer.OrdinalIgnoreCase))
            {
                string location = GetPluginLocation(plugin);
                line(string.Format("GUID={0} | Name={1} | Version={2} | Location={3}",
                    plugin.Metadata.GUID, plugin.Metadata.Name, plugin.Metadata.Version, location));
            }
            line("");
        }

        private static void DumpDiskAssemblies(Action<string> line)
        {
            line("[PluginFiles]");
            try
            {
                string[] dlls = Directory.GetFiles(Paths.PluginPath, "*.dll", SearchOption.AllDirectories);
                foreach (IGrouping<string, string> group in dlls
                    .GroupBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
                    .Where(g => g.Count() > 1)
                    .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase))
                {
                    line("DuplicateFileName=" + group.Key);
                    foreach (string file in group.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
                    {
                        line("  " + file);
                    }
                }
            }
            catch (Exception ex)
            {
                line("ScanError=" + ex.GetType().Name + ": " + ex.Message);
            }
            line("");
        }

        private static void DumpConfigEntries(Action<string> line)
        {
            line("[ConfigEntries]");
            List<string> identities = new List<string>();
            foreach (PluginInfo plugin in Chainloader.PluginInfos.Values.OrderBy(p => p.Metadata.GUID, StringComparer.OrdinalIgnoreCase))
            {
                try
                {
                    foreach (KeyValuePair<ConfigDefinition, ConfigEntryBase> pair in plugin.Instance.Config)
                    {
                        ConfigEntryBase entry = pair.Value;
                        if (entry == null || IsHidden(entry))
                        {
                            continue;
                        }

                        string file = entry.ConfigFile == null ? "<null>" : entry.ConfigFile.ConfigFilePath;
                        string identity = file + "|" + entry.Definition.Section + "|" + entry.Definition.Key;
                        identities.Add(identity);
                        line(string.Format("{0} | {1} | {2} | Type={3} | Value={4} | File={5}",
                            plugin.Metadata.GUID, entry.Definition.Section, entry.Definition.Key,
                            entry.SettingType == null ? "<null>" : entry.SettingType.FullName,
                            SafeValue(entry.BoxedValue), file));
                    }
                }
                catch (Exception ex)
                {
                    line("PluginConfigError=" + plugin.Metadata.GUID + " | " + ex.GetType().Name + ": " + ex.Message);
                }
            }

            foreach (IGrouping<string, string> duplicate in identities.GroupBy(x => x, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1))
            {
                line("DuplicateConfigIdentity=" + duplicate.Key + " | Count=" + duplicate.Count());
            }
            line("VisibleConfigEntryCount=" + identities.Count);
            line("");
        }

        private static void DumpSettingsHandler(Action<string> line)
        {
            line("[SettingsHandler]");
            try
            {
                Type handlerType = FindType("SettingsHandler");
                FieldInfo instanceField = handlerType == null ? null : handlerType.GetField("Instance",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                object handler = instanceField == null ? null : instanceField.GetValue(null);
                MethodInfo getAllSettings = handlerType == null ? null : handlerType.GetMethod("GetAllSettings",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                IEnumerable settings = getAllSettings == null || handler == null
                    ? null : getAllSettings.Invoke(handler, null) as IEnumerable;
                if (settings == null)
                {
                    line("Unavailable");
                    line("");
                    return;
                }

                List<string> wrappedIdentities = new List<string>();
                int total = 0;
                foreach (object setting in settings)
                {
                    total++;
                    ConfigEntryBase entry = FindConfigEntry(setting);
                    if (entry == null)
                    {
                        continue;
                    }

                    string category = InvokeString(setting, "GetCategory") ?? "<none>";
                    string file = entry.ConfigFile == null ? "<null>" : entry.ConfigFile.ConfigFilePath;
                    string identity = file + "|" + entry.Definition.Section + "|" + entry.Definition.Key;
                    wrappedIdentities.Add(identity);
                    line(string.Format("Wrapped={0} | Category={1} | Section={2} | Key={3} | File={4}",
                        setting.GetType().FullName, category, entry.Definition.Section, entry.Definition.Key, file));
                }

                line("TotalSettingCount=" + total);
                line("BepInExWrappedSettingCount=" + wrappedIdentities.Count);
                foreach (IGrouping<string, string> duplicate in wrappedIdentities
                    .GroupBy(x => x, StringComparer.OrdinalIgnoreCase)
                    .Where(g => g.Count() > 1))
                {
                    line("DuplicateWrappedSetting=" + duplicate.Key + " | Count=" + duplicate.Count());
                }
            }
            catch (Exception ex)
            {
                line("Error=" + ex.GetType().Name + ": " + ex.Message);
            }
            line("");
        }

        private static void DumpSectionTracker(Action<string> line)
        {
            line("[ModConfigSections]");
            try
            {
                Type trackerType = FindType("PEAKLib.ModConfig.Components.ModSectionNames");
                PropertyInfo sectionNames = trackerType == null ? null : trackerType.GetProperty("SectionNames",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                IEnumerable trackers = sectionNames == null ? null : sectionNames.GetValue(null, null) as IEnumerable;
                if (trackers == null)
                {
                    line("Unavailable");
                    line("");
                    return;
                }

                foreach (object tracker in trackers)
                {
                    string mod = ReadStringMember(tracker, "ModName") ?? "<null>";
                    IEnumerable sections = ReadEnumerableMember(tracker, "Sections");
                    string values = sections == null ? "<null>" : string.Join(", ", sections.Cast<object>().Select(x => x == null ? "<null>" : x.ToString()));
                    line("Mod=" + mod + " | Sections=" + values);
                }
            }
            catch (Exception ex)
            {
                line("Error=" + ex.GetType().Name + ": " + ex.Message);
            }
            line("");
        }

        private void DumpUi(Action<string> line)
        {
            line("[UI]");
            try
            {
                Type textType = FindType("TMPro.TMP_Text");
                if (textType == null)
                {
                    line("TMP_Text unavailable");
                    return;
                }

                PropertyInfo textProperty = textType.GetProperty("text", BindingFlags.Instance | BindingFlags.Public);
                Array objects = Resources.FindObjectsOfTypeAll(textType);
                int suspicious = 0;
                int written = 0;
                foreach (object value in objects)
                {
                    Component component = value as Component;
                    if (component == null || textProperty == null)
                    {
                        continue;
                    }

                    string text = textProperty.GetValue(value, null) as string;
                    string path = GetTransformPath(component.transform);
                    bool interesting = ContainsProblemToken(text) || IsModConfigObject(path);
                    if (!interesting)
                    {
                        continue;
                    }

                    if (ContainsProblemToken(text))
                    {
                        suspicious++;
                    }

                    if (written < _maxUiLines.Value)
                    {
                        line(string.Format("Text={0} | GameObject={1} | Active={2}",
                            SafeValue(text), path, component.gameObject.activeInHierarchy));
                        written++;
                    }
                }
                line("SuspiciousTextCount=" + suspicious);
                line("UiLinesWritten=" + written);
            }
            catch (Exception ex)
            {
                line("Error=" + ex.GetType().Name + ": " + ex.Message);
            }
            line("");
        }

        private static bool ContainsProblemToken(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }
            return value.IndexOf("log:0", StringComparison.OrdinalIgnoreCase) >= 0 ||
                value.IndexOf("log：0", StringComparison.OrdinalIgnoreCase) >= 0 ||
                value.IndexOf("loc:", StringComparison.OrdinalIgnoreCase) >= 0 ||
                value.IndexOf("unknown", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsModConfigObject(string path)
        {
            return path.IndexOf("ModSettings", StringComparison.OrdinalIgnoreCase) >= 0 ||
                path.IndexOf("ModConfig", StringComparison.OrdinalIgnoreCase) >= 0 ||
                path.IndexOf("SectionTabs", StringComparison.OrdinalIgnoreCase) >= 0 ||
                path.IndexOf("TABS", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static ConfigEntryBase FindConfigEntry(object setting)
        {
            for (Type type = setting == null ? null : setting.GetType(); type != null; type = type.BaseType)
            {
                foreach (FieldInfo field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    if (typeof(ConfigEntryBase).IsAssignableFrom(field.FieldType))
                    {
                        return field.GetValue(setting) as ConfigEntryBase;
                    }
                }
            }
            return null;
        }

        private static bool IsHidden(ConfigEntryBase entry)
        {
            object[] tags = entry.Description == null ? null : entry.Description.Tags;
            return tags != null && tags.Any(tag => string.Equals(Convert.ToString(tag), "Hidden", StringComparison.OrdinalIgnoreCase));
        }

        private static string SafeValue(object value)
        {
            if (value == null)
            {
                return "<null>";
            }
            return value.ToString().Replace("\r", "\\r").Replace("\n", "\\n");
        }

        private static string PluginVersionOf(string guid)
        {
            PluginInfo plugin;
            return Chainloader.PluginInfos.TryGetValue(guid, out plugin) && plugin != null
                ? plugin.Metadata.Version.ToString()
                : "<not loaded>";
        }

        private static Type FindType(string fullName)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType(fullName, false);
                if (type != null)
                {
                    return type;
                }
            }
            return null;
        }

        private static string GetPluginLocation(PluginInfo plugin)
        {
            try
            {
                PropertyInfo location = typeof(PluginInfo).GetProperty("Location",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                return location == null ? "<unknown>" : Convert.ToString(location.GetValue(plugin, null));
            }
            catch
            {
                return "<unknown>";
            }
        }

        private static string InvokeString(object instance, string methodName)
        {
            if (instance == null)
            {
                return null;
            }
            MethodInfo method = instance.GetType().GetMethod(methodName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return method == null ? null : method.Invoke(instance, null) as string;
        }

        private static string ReadStringMember(object instance, string name)
        {
            if (instance == null)
            {
                return null;
            }
            Type type = instance.GetType();
            PropertyInfo property = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property != null)
            {
                return property.GetValue(instance, null) as string;
            }
            FieldInfo field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return field == null ? null : field.GetValue(instance) as string;
        }

        private static IEnumerable ReadEnumerableMember(object instance, string name)
        {
            if (instance == null)
            {
                return null;
            }
            Type type = instance.GetType();
            PropertyInfo property = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property != null)
            {
                return property.GetValue(instance, null) as IEnumerable;
            }
            FieldInfo field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return field == null ? null : field.GetValue(instance) as IEnumerable;
        }

        private static string GetTransformPath(Transform transform)
        {
            List<string> names = new List<string>();
            for (Transform current = transform; current != null; current = current.parent)
            {
                names.Add(current.name);
            }
            names.Reverse();
            return string.Join("/", names.ToArray());
        }
    }
}
