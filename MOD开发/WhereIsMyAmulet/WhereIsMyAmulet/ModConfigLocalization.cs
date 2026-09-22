using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx.Configuration;
using HarmonyLib;

namespace WhereIsMyAmulet
{
    internal static class ModConfigLocalization
    {
        private const string ConfigFileName = "com.wuyachiyu.WhereIsMyAmulet.cfg";
        private static readonly FieldInfo DescriptionBackingField = typeof(ConfigDescription).GetField(
            "<Description>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);

        public static void PatchDisplayNames(Harmony harmony)
        {
            ModConfigUiAdapter.Install(harmony, ConfigFileName, "WhereIsMyAmulet",
                entry => GetLocalizedConfigText(entry.Definition.Section, entry.Definition.Key), GetLocalizedToken, message => UnityEngine.Debug.LogWarning("[WhereIsMyAmulet] " + message));
        }
        public static void RefreshVisibleUi() => ModConfigUiAdapter.RefreshVisibleUi();
        public static void Shutdown() => ModConfigUiAdapter.Shutdown();

        public static void ApplyLocalizedDescriptions(IEnumerable<ConfigEntryBase> entries)
        {
            if (entries == null)
            {
                return;
            }

            foreach (ConfigEntryBase entry in entries.Where(value => value != null))
            {
                SetDescription(entry, GetLocalizedDescription(entry.Definition.Section, entry.Definition.Key));
            }
        }

        private static string GetLocalizedDescription(string section, string key)
        {
            return GetDescription(section, key, IsChinese);
        }

        private static string GetLocalizedConfigText(string section, string key)
        {
            if (!IsKnownConfigKey(section, key))
            {
                return null;
            }
            return GetLocalizedToken(key);
        }

        private static bool IsKnownConfigKey(string section, string key)
        {
            if (section == "General") return key == "Enabled" || key == "ScanKey" || key == "ScanMode" || key == "DisplayDurationSeconds";
            if (section == "Display") return key == "MaxDistance" || key == "FontSize" || key == "LabelFont" || key == "ShowStatueFragments" || key == "ShowOffscreenDirection";
            return false;
        }

        private static string GetDescription(string section, string key, bool chinese)
        {
            if (section == "General")
            {
                switch (key)
                {
                    case "Enabled": return chinese ? "启用或关闭 WhereIsMyAmulet。" : "Enable or disable WhereIsMyAmulet.";
                    case "ScanKey": return chinese ? "扫描掉落护符和雕像碎片的快捷键。" : "Key used to scan dropped amulets and statue fragments.";
                    case "ScanMode": return chinese ? "选择标签常驻显示，或在扫描后按设定时间自动隐藏。" : "Keep labels visible, or hide them automatically after the configured duration.";
                    case "DisplayDurationSeconds": return chinese ? "定时模式下标签持续显示的秒数。" : "Number of seconds labels remain visible in Timed mode.";
                }
            }
            if (section == "Display")
            {
                switch (key)
                {
                    case "MaxDistance": return chinese ? "标签显示的最大距离，单位米。0 表示不限制。" : "Maximum label distance in metres. Set to 0 for unlimited.";
                    case "FontSize": return chinese ? "位置标签的字号。" : "Font size used by location labels.";
                    case "LabelFont": return chinese ? "选择仅用于英文位置标签的字体；中文可能显示为口口口。" : "Choose a font for English location labels only. Chinese text may display as tofu boxes.";
                    case "ShowStatueFragments": return chinese ? "显示场景雕像手中的护符碎片，以及祭坛上已经镶嵌的护符碎片。" : "Show amulet fragments held by amulet statues and fragments mounted on Scout Statues.";
                    case "ShowOffscreenDirection": return chinese ? "目标在屏幕外时显示方向提示。" : "Show a direction indicator when a target is off-screen.";
                }
            }
            return string.Empty;
        }

        private static string GetLocalizedToken(string key)
        {
            bool chinese = IsChinese;
            switch (key)
            {
                case "WhereIsMyAmulet": return chinese ? "护符在哪里" : "WhereIsMyAmulet";
                case "General": return chinese ? "常规" : "General";
                case "Display": return chinese ? "显示" : "Display";
                case "Enabled": return chinese ? "启用 MOD" : "Enabled";
                case "ScanKey": return chinese ? "扫描快捷键" : "Scan Key";
                case "ScanMode": return chinese ? "显示模式" : "Scan Mode";
                case "DisplayDurationSeconds": return chinese ? "定时显示秒数" : "Display Duration";
                case "MaxDistance": return chinese ? "最大距离" : "Max Distance";
                case "FontSize": return chinese ? "标签字号" : "Label Font Size";
                case "LabelFont": return chinese ? "标签字体" : "Label Font";
                case "ShowStatueFragments": return chinese ? "显示雕像碎片" : "Show Statue Fragments";
                case "ShowOffscreenDirection": return chinese ? "屏外方向提示" : "Off-screen Direction";
                case "Auto": return chinese ? "自动" : "Auto";
                case "GameDefault": return chinese ? "游戏默认" : "Game Default";
                case "TmpDefault": return chinese ? "TMP 默认字体" : "TMP Default";
                case "Persistent": return chinese ? "常驻" : "Persistent";
                case "Timed": return chinese ? "定时" : "Timed";
                case "KoreanBinggrae": return chinese ? "KoreanBinggrae" : "KoreanBinggrae";
                case "Crazk": return chinese ? "Crazk" : "Crazk";
                default: return null;
            }
        }

        private static void SetDescription(ConfigEntryBase entry, string text)
        {
            if (!ModConfigUiAdapter.Owns(entry, ConfigFileName) || entry.Description == null ||
                DescriptionBackingField == null || string.IsNullOrEmpty(text)) return;
            try { DescriptionBackingField.SetValue(entry.Description, text); }
            catch (Exception ex) { UnityEngine.Debug.LogWarning("[WhereIsMyAmulet] Description localization skipped: " + ex.Message); }
        }

        private static bool IsChinese
        {
            get
            {
                return LocalizedText.CURRENT_LANGUAGE == LocalizedText.Language.SimplifiedChinese ||
                    LocalizedText.CURRENT_LANGUAGE == LocalizedText.Language.TraditionalChinese;
            }
        }

    }
}
