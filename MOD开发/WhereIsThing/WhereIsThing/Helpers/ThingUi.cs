using System.Globalization;

namespace WhereIsThing
{
    internal static class ThingUi
    {
        public static bool IsChinese
        {
            get
            {
                return LocalizedText.CURRENT_LANGUAGE == LocalizedText.Language.SimplifiedChinese ||
                    LocalizedText.CURRENT_LANGUAGE == LocalizedText.Language.TraditionalChinese;
            }
        }

        public static string Text(string english, string chinese)
        {
            return IsChinese ? chinese : english;
        }

        public static string PresetWindowTitle(bool allowEditing)
        {
            return allowEditing ? Text("WhereIsThing Presets", "WhereIsThing 预设") : Text("Shared Presets", "共享预设");
        }

        public static string PresetWindowSubtitle(bool allowEditing, bool usingFallbackPresets)
        {
            if (allowEditing)
            {
                return Text("Manage preset contents and sharing.", "管理预设内容与共享。");
            }

            if (usingFallbackPresets)
            {
                return Text("Host is not sharing presets. Using fallback presets.", "房主未共享预设，当前使用保底预设。");
            }

            return Text("Choose one preset for your own scan settings.", "为你自己的扫描设置选择一个预设。");
        }

        public static string SelectionWindowTitle()
        {
            return Text("Preset Targets", "预设目标");
        }

        public static string SelectionWindowSubtitle()
        {
            return Text("Choose which items and targets belong to this preset.", "选择哪些物品和目标属于这个预设。");
        }

        public static string SearchPlaceholder()
        {
            return Text("Search", "搜索");
        }

        public static string SelectedOnly()
        {
            return Text("Selected only", "仅显示已选");
        }

        public static string SelectVisible()
        {
            return Text("Select visible", "全选当前");
        }

        public static string ClearVisible()
        {
            return Text("Clear visible", "清除当前");
        }

        public static string Cancel()
        {
            return Text("Cancel", "取消");
        }

        public static string Apply()
        {
            return Text("Apply", "应用");
        }

        public static string Close()
        {
            return Text("Close", "关闭");
        }

        public static string LanguageLabel(ThingNameLanguage language)
        {
            return Text("Language: ", "语言：") + GetLanguageDisplay(language);
        }

        public static string CategoryLabel(string category, ThingNameLanguage language)
        {
            return Text("Category: ", "分类：") + ThingCatalog.GetCategoryDisplay(category, language);
        }

        public static string SelectedCount(int selectedCount, int totalCount)
        {
            return IsChinese
                ? string.Format(CultureInfo.InvariantCulture, "已选 {0} / {1}", selectedCount, totalCount)
                : string.Format(CultureInfo.InvariantCulture, "{0} selected / {1}", selectedCount, totalCount);
        }

        public static string NoMatchingTargets()
        {
            return Text("No matching targets", "没有匹配目标");
        }

        public static string ShareModeLabel(ThingPresetShareMode shareMode)
        {
            switch (shareMode)
            {
                case ThingPresetShareMode.BuiltInOnly:
                    return Text("Share: Built-in only", "共享：仅默认预设");
                case ThingPresetShareMode.PublishedPresets:
                    return Text("Share: Published presets", "共享：已发布预设");
                default:
                    return Text("Share: Off", "共享：关闭");
            }
        }

        public static string NewPreset()
        {
            return Text("New preset", "新增预设");
        }

        public static string Edit()
        {
            return Text("Edit", "编辑");
        }

        public static string Rename()
        {
            return Text("Rename", "重命名");
        }

        public static string RenamePresetTitle()
        {
            return Text("Rename custom preset", "重命名自定义预设");
        }

        public static string PresetNamePlaceholder()
        {
            return Text("Preset name", "预设名称");
        }

        public static string Save()
        {
            return Text("Save", "保存");
        }

        public static string PresetNameRequired()
        {
            return Text("Enter a preset name.", "请输入预设名称。");
        }

        public static string Hide()
        {
            return Text("Hide", "隐藏");
        }

        public static string Publish()
        {
            return Text("Publish", "公开");
        }

        public static string Delete()
        {
            return Text("Delete", "删除");
        }

        public static string Use(bool active)
        {
            return active ? Text("Using", "使用中") : Text("Use", "使用");
        }

        public static string LocalScanSettings()
        {
            return Text("Your scan settings", "你的扫描设置");
        }

        public static string ScanModeLabel(ThingScanMode scanMode, float durationSeconds)
        {
            if (scanMode == ThingScanMode.Persistent)
            {
                return Text("Mode: Persistent", "模式：常驻");
            }

            return IsChinese
                ? string.Format(CultureInfo.InvariantCulture, "模式：定时 {0:0}s", durationSeconds)
                : string.Format(CultureInfo.InvariantCulture, "Mode: Timed {0:0}s", durationSeconds);
        }

        public static string ScopeTitle()
        {
            return Text("Locations", "显示范围");
        }

        public static string PlayerNames()
        {
            return Text("Player names", "玩家名");
        }

        public static string Ground()
        {
            return Text("Ground", "地面");
        }

        public static string Held()
        {
            return Text("Held", "手持");
        }

        public static string Backpack()
        {
            return Text("Backpack", "背包");
        }

        public static string Luggage()
        {
            return Text("Luggage", "行李箱");
        }

        public static string Statue()
        {
            return Text("Statue", "雕像");
        }

        public static string TimeMinus()
        {
            return "-";
        }

        public static string TimePlus()
        {
            return "+";
        }

        public static string EmptyPresets()
        {
            return Text("No presets", "暂无预设");
        }

        public static string NoTargets()
        {
            return Text("No targets", "暂无目标");
        }

        public static string PublishedTag()
        {
            return Text("Published", "已公开");
        }

        public static string BuiltInTag()
        {
            return Text("Built-in", "保底");
        }

        public static string CustomPresetName(int index)
        {
            return IsChinese ? "自定义预设 " + index : "Custom Preset " + index;
        }

        public static string MigratedPresetName()
        {
            return Text("Migrated", "旧配置导入");
        }

        public static string AchievementPresetName()
        {
            return Text("Achievement", "成就探索");
        }

        public static string SurvivalPresetName()
        {
            return Text("Survival Medical", "生存急救");
        }

        public static string AscentEightPresetName()
        {
            return Text("Ascent 8", "天阶 8");
        }

        public static string GetLanguageDisplay(ThingNameLanguage language)
        {
            switch (language)
            {
                case ThingNameLanguage.English:
                    return "English";
                case ThingNameLanguage.SimplifiedChinese:
                    return "简体中文";
                default:
                    return Text("Follow Game", "跟随游戏");
            }
        }
    }
}
