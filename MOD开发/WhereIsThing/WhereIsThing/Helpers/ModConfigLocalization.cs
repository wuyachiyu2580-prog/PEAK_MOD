using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx.Configuration;
using HarmonyLib;

namespace WhereIsThing
{
    internal static class ModConfigLocalization
    {
        private const string ConfigFileName = "com.wuyachiyu.WhereIsThing.cfg";
        private static readonly FieldInfo DescriptionBackingField = typeof(ConfigDescription).GetField(
            "<Description>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);

        public static void PatchDisplayNames(Harmony harmony)
        {
            ModConfigUiAdapter.Install(harmony, ConfigFileName, "WhereIsThing",
                entry => GetLocalizedConfigText(entry.Definition.Section, entry.Definition.Key), GetLocalizedToken, message => UnityEngine.Debug.LogWarning("[WhereIsThing] " + message));
        }
        public static void RefreshVisibleUi() => ModConfigUiAdapter.RefreshVisibleUi();
        public static void Shutdown() => ModConfigUiAdapter.Shutdown();

        public static void ApplyLocalizedDescriptions(IEnumerable<ConfigEntryBase> entries)
        {
            if (entries == null)
            {
                return;
            }

            foreach (ConfigEntryBase entry in entries.Where(entry => entry != null))
            {
                if (IsKnownConfigKey(entry.Definition.Section, entry.Definition.Key))
                    SetDescription(entry, GetLocalizedDescription(entry.Definition.Key));
            }
        }

        private static string GetLocalizedConfigText(string section, string key)
        {
            string canonicalSection = GetCanonicalToken(section == null ? string.Empty : section.Replace(" ", string.Empty));
            string canonicalKey = GetCanonicalToken(key == null ? string.Empty : key.Replace(" ", string.Empty));
            if (string.IsNullOrEmpty(canonicalKey))
            {
                return null;
            }

            // Section is part of the stable identity. Keep the mapping scoped so a future
            // duplicate key in another section cannot be localized accidentally.
            if (!IsKnownConfigKey(canonicalSection, canonicalKey))
            {
                return null;
            }

            return GetLocalizedToken(canonicalKey);
        }

        private static bool IsKnownConfigKey(string section, string key)
        {
            switch (section)
            {
                case "Diagnostics":
                    return key == "PhaseTiming" || key == "PhaseTimingSeconds";
                case "General":
                    return key == "Enabled" || key == "ScanKey" || key == "WindowKey" ||
                        key == "ScanMode" || key == "DisplayDurationSeconds";
                case "Display":
                    return key == "NameLanguage" || key == "MaxDistance" || key == "MapScope" || key == "FontSize" ||
                        key == "LabelFont" || key == "ShowOffscreenDirection" || key == "ShowOwnerNames";
                case "Presets":
                    return key == "PresetSchemaVersion" || key == "PickupIdentityVersion" || key == "LocalPresets" ||
                        key == "ActiveLocalPresetId" || key == "ActiveLocalPresetIds" || key == "RestrictClientPresets" || key == "SelectedSharedPresetId" || key == "ShareMode";
                case "Selection":
                    return key == "SelectedItemIds" || key == "SelectedLuggage" ||
                        key == "SelectedLuggageTypes" || key == "SelectedSceneTargetTypes" ||
                        key == "LocationScopes";
                default:
                    return false;
            }
        }

        private static string GetCanonicalToken(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return null;
            }

            switch (value)
            {
                case "物品在哪": return "WhereIsThing";
                case "常规": return "General";
                case "诊断": return "Diagnostics";
                case "限时阶段计时": return "PhaseTiming";
                case "阶段计时秒数": return "PhaseTimingSeconds";
                case "显示": return "Display";
                case "选择": return "Selection";
                case "预设": return "Presets";
                case "启用MOD": return "Enabled";
                case "扫描快捷键": return "ScanKey";
                case "选择窗口快捷键": return "WindowKey";
                case "显示模式": return "ScanMode";
                case "定时显示秒数": return "DisplayDurationSeconds";
                case "名称语言": return "NameLanguage";
                case "最大距离": return "MaxDistance";
                case "地图范围": return "MapScope";
                case "当前关卡及入口营地": return "CurrentSegment";
                case "整张地图": return "WholeMap";
                case "拾取类型迁移版本": return "PickupIdentityVersion";
                case "标签字号": return "FontSize";
                case "标签字体": return "LabelFont";
                case "屏外方向提示": return "ShowOffscreenDirection";
                case "玩家名": return "ShowOwnerNames";
                case "已选物品ID": return "SelectedItemIds";
                case "兼容行李箱开关": return "SelectedLuggage";
                case "已选行李箱类型": return "SelectedLuggageTypes";
                case "已选场景目标": return "SelectedSceneTargetTypes";
                case "位置范围": return "LocationScopes";
                case "预设版本号": return "PresetSchemaVersion";
                case "本地预设数据": return "LocalPresets";
                case "当前本地预设": return "ActiveLocalPresetId";
                case "启用的本地预设": return "ActiveLocalPresetIds";
                case "客机预设限制": return "RestrictClientPresets";
                case "当前共享预设": return "SelectedSharedPresetId";
                case "房主共享模式": return "ShareMode";
                case "常驻": return "Persistent";
                case "定时": return "Timed";
                case "关闭": return "Off";
                case "仅默认预设": return "BuiltInOnly";
                case "已发布预设": return "PublishedPresets";
                case "跟随游戏": return "Game";
                case "简体中文": return "SimplifiedChinese";
                case "自动": return "Auto";
                case "游戏默认": return "GameDefault";
                case "TMP默认字体": return "TmpDefault";
                case "无": return "None";
                case "地面": return "Ground";
                case "手持": return "Held";
                case "背包": return "Backpack";
                case "行李箱": return "Luggage";
                default: return value;
            }
        }

        private static string GetLocalizedToken(string key)
        {
            bool chinese = IsChinese;
            switch (key)
            {
                case "WhereIsThing": return chinese ? "物品在哪" : "WhereIsThing";
                case "General": return chinese ? "常规" : "General";
                case "Diagnostics": return chinese ? "诊断" : "Diagnostics";
                case "PhaseTiming": return chinese ? "限时阶段计时" : "Phase Timing";
                case "PhaseTimingSeconds": return chinese ? "阶段计时秒数" : "Phase Timing Seconds";
                case "Display": return chinese ? "显示" : "Display";
                case "Selection": return chinese ? "选择" : "Selection";
                case "Presets": return chinese ? "预设" : "Presets";
                case "Enabled": return chinese ? "启用 MOD" : "Enabled";
                case "ScanKey": return chinese ? "扫描快捷键" : "Scan Key";
                case "WindowKey": return chinese ? "选择窗口快捷键" : "Window Key";
                case "ScanMode": return chinese ? "显示模式" : "Display Mode";
                case "DisplayDurationSeconds": return chinese ? "定时显示秒数" : "Display Duration";
                case "NameLanguage": return chinese ? "名称语言" : "Name Language";
                case "MaxDistance": return chinese ? "最大距离" : "Max Distance";
                case "FontSize": return chinese ? "标签字号" : "Label Font Size";
                case "LabelFont": return chinese ? "标签字体" : "Label Font";
                case "ShowOffscreenDirection": return chinese ? "屏外方向提示" : "Off-screen Direction";
                case "ShowOwnerNames": return chinese ? "玩家名" : "Player Names";
                case "SelectedItemIds": return chinese ? "已选物品 ID" : "Selected Item IDs";
                case "SelectedLuggage": return chinese ? "兼容行李箱开关" : "Legacy Luggage Toggle";
                case "SelectedLuggageTypes": return chinese ? "已选行李箱类型" : "Selected Luggage Types";
                case "SelectedSceneTargetTypes": return chinese ? "已选场景目标" : "Selected Scene Targets";
                case "LocationScopes": return chinese ? "位置范围" : "Location Scopes";
                case "PresetSchemaVersion": return chinese ? "预设版本号" : "Preset Schema Version";
                case "LocalPresets": return chinese ? "本地预设数据" : "Local Presets";
                case "ActiveLocalPresetId": return chinese ? "当前本地预设" : "Active Local Preset";
                case "ActiveLocalPresetIds": return chinese ? "启用的本地预设" : "Enabled Local Presets";
                case "RestrictClientPresets": return chinese ? "客机预设限制" : "Restrict Client Presets";
                case "SelectedSharedPresetId": return chinese ? "当前共享预设" : "Selected Shared Preset";
                case "ShareMode": return chinese ? "房主共享模式" : "Share Mode";
                case "MushroomZombie": return chinese ? "森蕈僵尸" : "Mushroom Zombie";
                case "Beetle": return chinese ? "甲虫" : "Beetle";
                case "Scorpion": return chinese ? "蝎子" : "Scorpion";
                case "Spider": return chinese ? "蜘蛛" : "Spider";
                case "BeeSwarm": return chinese ? "蜂群" : "Bee Swarm";
                case "Scoutmaster": return chinese ? "童军领队" : "Scoutmaster";
                case "TumbleWeed": return chinese ? "风滚草" : "Tumbleweed";
                case "GhostBall": return chinese ? "鬼球" : "Ghost Ball";
                case "SpikeTrap": return chinese ? "地刺" : "Spike Trap";
                case "Antlion": return chinese ? "蚁狮" : "Antlion";
                case "VenusFlyTrap": return chinese ? "捕蝇草" : "Venus Flytrap";
                case "Tornado": return chinese ? "龙卷风" : "Tornado";
                case "NapberryHypnoOrb": return chinese ? "晚安莓" : "Napberry";
                case "ArrowShooter": return chinese ? "箭矢发射器" : "Arrow Shooter";
                case "MovingSawBlade": return chinese ? "移动锯刃" : "Moving Sawblade";
                case "SpikeRoller": return chinese ? "滚刺机关" : "Spike Roller";
                case "SwingingAxe": return chinese ? "摆斧机关" : "Swinging Axe";
                case "GloomBellTower": return chinese ? "雾沼钟塔" : "Gloom Bell Tower";
                case "Persistent": return chinese ? "常驻" : "Persistent";
                case "Timed": return chinese ? "定时" : "Timed";
                case "Off": return chinese ? "关闭" : "Off";
                case "BuiltInOnly": return chinese ? "仅默认预设" : "Built-in Only";
                case "PublishedPresets": return chinese ? "已发布预设" : "Published Presets";
                case "Game": return chinese ? "跟随游戏" : "Follow Game";
                case "MapScope": return chinese ? "地图范围" : "Map Scope";
                case "CurrentSegment": return chinese ? "当前关卡及入口营地" : "Current Segment and Entry Camp";
                case "WholeMap": return chinese ? "整张地图" : "Whole Map";
                case "PickupIdentityVersion": return chinese ? "拾取类型迁移版本" : "Pickup Identity Version";
                case "English": return "English";
                case "SimplifiedChinese": return chinese ? "简体中文" : "Simplified Chinese";
                case "Auto": return chinese ? "自动" : "Auto";
                case "GameDefault": return chinese ? "游戏默认" : "Game Default";
                case "TmpDefault": return chinese ? "TMP 默认字体" : "TMP Default";
                case "KoreanBinggrae": return "KoreanBinggrae";
                case "Crazk": return "Crazk";
                case "None": return chinese ? "无" : "None";
                case "Ground": return chinese ? "地面" : "Ground";
                case "Held": return chinese ? "手持" : "Held";
                case "Backpack": return chinese ? "背包" : "Backpack";
                case "Luggage": return chinese ? "行李箱" : "Luggage";
                case "Statue": return chinese ? "雕像" : "Statue";
                default: return null;
            }
        }

        private static string GetLocalizedDescription(string key)
        {
            bool chinese = IsChinese;
            switch (key)
            {
                case "PhaseTiming": return chinese ? "采集一次限时阶段耗时，结束后自动关闭。嵌套阶段不可重复相加。" : "Run one bounded diagnostic window, then automatically switch off. Nested phases must not be summed.";
                case "PhaseTimingSeconds": return chinese ? "诊断窗口持续 30 至 60 秒。P95 为直方图上界；GC 增量是次数，不是暂停耗时。" : "Diagnostic window duration, 30 to 60 seconds. P95 is a histogram upper bound; GC deltas are counts, not pause durations.";
                case "Enabled": return chinese ? "启用或关闭 WhereIsThing。" : "Enable or disable WhereIsThing.";
                case "ScanKey": return chinese ? "显示当前已选目标位置的快捷键。" : "Key used to show the locations of selected targets.";
                case "WindowKey": return chinese ? "按住 Alt 后使用此键打开物品选择窗口。" : "Hold Alt and press this key to open the target selection window.";
                case "ScanMode": return chinese ? "选择位置标签常驻显示，或在数秒后自动隐藏。" : "Keep location labels visible or hide them automatically after a few seconds.";
                case "DisplayDurationSeconds": return chinese ? "定时模式下标签持续显示的秒数。" : "Number of seconds labels remain visible in Timed mode.";
                case "NameLanguage": return chinese ? "物品名称跟随游戏语言，或强制使用英文/简体中文。" : "Follow the game language for item names, or force English/Simplified Chinese.";
                case "MaxDistance": return chinese ? "所选地图范围内的最大距离，单位米。0 表示不限距离，仍遵守地图范围。" : "Maximum distance within the selected map scope. 0 is unlimited distance, not unlimited map scope.";
                case "MapScope": return chinese ? "默认只显示当前关卡及入口营地，可切换到整张地图。自定义地图缺少有效关卡边界时，按场景归属和附近 40 米回退。" : "Show the current segment and its entry camp, or the whole map. Maps without valid boundaries use scene hierarchy and a nearby 40m fallback.";
                case "PickupIdentityVersion": return chinese ? "内部拾取类型迁移版本，请勿手动编辑。" : "Internal pickup selection migration version. Do not edit manually.";
                case "FontSize": return chinese ? "位置与距离标签的字号。" : "Font size used by location and distance labels.";
                case "LabelFont": return chinese ? "选择位置标签字体；显示中文时会优先使用已有的中文字体补足缺字。" : "Choose the label font. Chinese names use an available Chinese-capable game font when needed.";
                case "ShowOffscreenDirection": return chinese ? "目标在屏幕外时显示方向提示。" : "Show a direction indicator when a target is off-screen.";
                case "ShowOwnerNames": return chinese ? "在支持的目标后显示放置者玩家名。" : "Show the placer name after supported labels.";
                case "SelectedItemIds": return chinese ? "由 Alt+C 选择窗口维护的物品 ID，请勿手动编辑。" : "Item IDs managed by the Alt+C selection window. Do not edit manually.";
                case "SelectedLuggage": return chinese ? "旧版配置兼容项，请使用选择窗口设置行李箱类型。" : "Legacy compatibility setting. Choose luggage types in the selection window.";
                case "SelectedLuggageTypes": return chinese ? "由 Alt+C 选择窗口维护的行李箱类型，请勿手动编辑。" : "Luggage types managed by the Alt+C selection window. Do not edit manually.";
                case "SelectedSceneTargetTypes": return chinese ? "由 Alt+C 选择窗口维护的动态危险和钟塔类型，请勿手动编辑。" : "Scene target types managed by the Alt+C selection window. Do not edit manually.";
                case "LocationScopes": return chinese ? "目标位置范围。雕像范围会显示已选护符在场景雕像手中的碎片；行李箱是否扫描由 Alt+C 窗口中已选的行李箱类型自动控制。" : "Target location scopes. Statue scope shows selected amulet fragments held by scene statues. Luggage scanning is controlled automatically by the luggage types selected in the Alt+C window.";
                case "PresetSchemaVersion": return chinese ? "WhereIsThing 内部使用的预设数据版本，请勿手动编辑。" : "Internal preset schema version used by WhereIsThing. Do not edit manually.";
                case "LocalPresets": return chinese ? "WhereIsThing 保存的本地预设内容，请勿手动编辑。" : "Serialized local presets managed by WhereIsThing. Do not edit manually.";
                case "ActiveLocalPresetId": return chinese ? "当前本地玩家使用的预设 ID，请勿手动编辑。" : "Preset ID currently selected for local use. Do not edit manually.";
                case "ActiveLocalPresetIds": return chinese ? "启用的本地预设 ID，目标合并显示，请勿手动编辑。" : "Enabled local preset IDs, whose targets are combined. Do not edit manually.";
                case "RestrictClientPresets": return chinese ? "默认开启。作为客机时使用房主共享预设（未共享时使用保底预设）；关闭后可使用和编辑本地预设。这是本地偏好，不是房主强制规则。" : "Enabled by default. As a client, use host-shared presets (or fallback presets). Disable to use and edit local presets. This is a local preference, not a host-enforced rule.";
                case "SelectedSharedPresetId": return chinese ? "当前客户端选择的共享预设 ID，请勿手动编辑。" : "Shared preset ID currently selected by this client. Do not edit manually.";
                case "ShareMode": return chinese ? "房主是否向客机共享默认预设或已发布预设。" : "Whether the host shares built-in presets or published presets with clients.";
                default: return string.Empty;
            }
        }

        private static void SetDescription(ConfigEntryBase entry, string text)
        {
            if (!ModConfigUiAdapter.Owns(entry, ConfigFileName) || entry.Description == null ||
                DescriptionBackingField == null || string.IsNullOrEmpty(text)) return;
            try { DescriptionBackingField.SetValue(entry.Description, text); }
            catch (Exception ex) { UnityEngine.Debug.LogWarning("[WhereIsThing] Description localization skipped: " + ex.Message); }
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
