using System;
using System.Collections;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using PlayersInfo.Helpers;
using PlayersInfo.MonoBehaviours;
using PlayersInfo.Patches;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PlayersInfo
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class PlayersInfoPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.players.info";
        public const string PluginName = "PlayersInfo";
        public const string PluginVersion = "0.2.2";

        public enum HudAnchor { TopLeft, TopRight, BottomLeft, BottomRight }
        public enum TeammateSortMode { Stable, Distance }
        public enum SpectatorNearbyCenterMode { LocalCharacter, ObservedCharacter }
        public enum TeammateInventoryDisplayMode { Disabled, ContentsOnly, ContentsAndJetpackFuel }

        // ========== Config Entries（全局共享） ==========
        public static ConfigEntry<bool> CfgModEnabled;

        public static ConfigEntry<bool> CfgEnableStaminaBar;
        public static ConfigEntry<bool> CfgShowStaminaValue;
        public static ConfigEntry<bool> CfgShowExtraStaminaCap;

        public static ConfigEntry<TeammateInventoryDisplayMode> CfgInventoryDisplayMode;

        public static ConfigEntry<HudAnchor> CfgAnchor;
        public static ConfigEntry<float> CfgOffsetX;
        public static ConfigEntry<float> CfgOffsetY;

        // 附近玩家过滤
        public static ConfigEntry<float> CfgNearbyRange;
        public static ConfigEntry<int> CfgMaxNearbyCount;
        public static ConfigEntry<TeammateSortMode> CfgTeammateSortMode;
        public static ConfigEntry<SpectatorNearbyCenterMode> CfgSpectatorNearbyCenter;
        public static ConfigEntry<bool> CfgRoundStamina;
        public static ConfigEntry<bool> CfgDebugLogging;

        private Harmony _harmony;
        private float _nextSafeTick;
        private const float SafeTickInterval = 1f;
        private const string DisplaySection = "Display";
        private const string AdvancedSection = "Advanced";
        private static readonly FieldInfo OrphanedEntriesField =
            typeof(ConfigFile).GetField("<OrphanedEntries>k__BackingField",
                BindingFlags.Instance | BindingFlags.NonPublic);

        private void Awake()
        {
            try
            {
                PluginLogger.Log = Logger;
                LanguageHelper.IsChinese = LanguageHelper.DetectChineseLanguage();

                BindConfig();

                _harmony = new Harmony(PluginGuid);
                _harmony.PatchAll();
                try { ModConfigLocalization.PatchDisplayNames(_harmony); }
                catch (Exception ex) { PluginLogger.ThrottleWarn("modconfig_patch", "ModConfig localization patch skipped: " + ex.Message); }

                SceneManager.sceneLoaded += OnSceneLoaded;
                StartCoroutine(DeferredLanguageRefresh());

                // 尝试先建 Tracker（HUD 等 GUIManager.Start 后才建）
                if (CfgModEnabled.Value)
                {
                    TeamRosterTracker.EnsureExists();
                }

                PluginLogger.Info($"{PluginName} v{PluginVersion} loaded. Lang={(LanguageHelper.IsChinese ? "zh" : "en")}");
            }
            catch (Exception ex)
            {
                Logger.LogError($"[{PluginName}] Awake failed: {ex}");
            }
        }

        private void BindConfig()
        {
            CfgModEnabled = Config.Bind(DisplaySection,
                "Enabled", ReadLegacyValue("General", "Enabled", true),
                LanguageHelper.L("Master switch. Turn off to disable all features.",
                                 "总开关，关闭后全部功能失效。"));

            CfgEnableStaminaBar = Config.Bind(DisplaySection,
                "EnableStaminaBar", ReadLegacyValue("Features", "EnableStaminaBar", true),
                LanguageHelper.L("Show teammate stamina bars (with extra stamina).",
                                 "显示队友体力条（含临时体力）。"));

            CfgShowStaminaValue = Config.Bind(DisplaySection,
                "ShowStaminaValue", ReadLegacyValue("Features", "ShowStaminaValue", true),
                LanguageHelper.L("Show numeric stamina value on the bar.",
                                 "在体力条右侧显示数值。"));

            CfgShowExtraStaminaCap = Config.Bind(DisplaySection,
                "ShowExtraStaminaCap", true,
                LanguageHelper.L("Show teammate extra stamina as current/cap. Turn off to show current only.",
                                 "队友额外体力显示为 当前/上限。关闭后只显示当前值。"));

            TeammateInventoryDisplayMode inventoryMode = ReadInventoryDisplayMode();
            RemoveConfigEntry(DisplaySection, "EnableInventoryRow");
            RemoveConfigEntry("Features", "EnableInventoryRow");
            CfgInventoryDisplayMode = Config.Bind(DisplaySection,
                "EnableInventoryRow", inventoryMode,
                LanguageHelper.L("Choose whether teammate inventory is hidden, shows contents only, or also shows jetpack fuel.",
                                 "选择隐藏队友物品栏、仅显示物品内容，或同时显示喷气背包燃料。"));

            CfgAnchor = Config.Bind(DisplaySection,
                "Anchor", ReadLegacyValue("Layout", "Anchor", HudAnchor.BottomLeft),
                LanguageHelper.L("HUD anchor corner on screen.",
                                 "HUD 屏幕锚点位置。"));

            CfgOffsetX = Config.Bind(DisplaySection,
                "OffsetX", ReadLegacyValue("Layout", "OffsetX", 0f),
                new ConfigDescription(
                    LanguageHelper.L("Additional X offset in pixels.", "X 方向像素偏移。"),
                    new AcceptableValueRange<float>(-800f, 800f)));

            CfgOffsetY = Config.Bind(DisplaySection,
                "OffsetY", ReadLegacyValue("Layout", "OffsetY", 0f),
                new ConfigDescription(
                    LanguageHelper.L("Additional Y offset in pixels.", "Y 方向像素偏移。"),
                    new AcceptableValueRange<float>(-800f, 800f)));

            CfgNearbyRange = Config.Bind(DisplaySection,
                "NearbyRange", ReadLegacyValue("Nearby", "NearbyRange", 30f),
                new ConfigDescription(
                    LanguageHelper.L("Max distance (meters) to show a teammate bar. 0 = unlimited.",
                                     "显示队友体力条的最大距离（米）。0 表示不限。"),
                    new AcceptableValueRange<float>(0f, 500f)));

            CfgMaxNearbyCount = Config.Bind(DisplaySection,
                "MaxNearbyCount", ReadLegacyValue("Nearby", "MaxNearbyCount", 3),
                new ConfigDescription(
                    LanguageHelper.L("Max number of nearest teammates to show bars for.",
                                     "最多显示几个最近的队友体力条。"),
                new AcceptableValueRange<int>(0, 8)));

            CfgTeammateSortMode = Config.Bind(DisplaySection,
                "TeammateSortMode", TeammateSortMode.Stable,
                LanguageHelper.L("Order teammate bars by first appearance, or by current distance.",
                                 "队友条按首次出现顺序或当前距离排序。"));

            CfgSpectatorNearbyCenter = Config.Bind(DisplaySection,
                "SpectatorNearbyCenter",
                SpectatorNearbyCenterMode.ObservedCharacter,
                LanguageHelper.L("While spectating, use the local character or the observed character as the nearby-player center.",
                                 "观战时，选择以本机角色或被观看角色作为附近玩家中心。"));

            CfgRoundStamina = Config.Bind(DisplaySection,
                "RoundStaminaValue", ReadLegacyValue("Features", "RoundStaminaValue", true),
                LanguageHelper.L("If true, round stamina numeric value to nearest integer; else 1 decimal.",
                                 "为 true 时体力数值四舍五入到整数，否则显示 1 位小数。"));

            CfgDebugLogging = Config.Bind(AdvancedSection,
                "DebugLogging", ReadLegacyValue("Diagnostics", "DebugLogging", false),
                LanguageHelper.L("Enable verbose diagnostic logs for distance, stamina, inventory, and cloned UI internals.",
                                 "启用详细诊断日志（距离、体力、物品栏和克隆 UI 内部状态）。"));

            ModConfigLocalization.ApplyLocalizedDescriptions();
            RemoveLegacyConfigEntries();
            MigrateDefaultHudAnchor();

            // 只订阅真正影响"克隆体结构"的配置项变化，避免任意配置改动（OffsetX 拖滑块、
            // BepInEx 启动回写、ConfigurationManager 实时事件）触发 ClearAll → 全部体力条一起跳。
            // 运行时数值类（NearbyRange/MaxNearbyCount/TeammateSortMode/SpectatorNearbyCenter/
            // RoundStamina/ShowExtraStaminaCap/DebugLogging/Anchor/Offset）
            // 由 Update 直接读 Cfg.Value 生效，无需事件。
            CfgModEnabled.SettingChanged += OnStructuralConfigChanged;
            CfgEnableStaminaBar.SettingChanged += OnStructuralConfigChanged;
            CfgShowStaminaValue.SettingChanged += OnStructuralConfigChanged;
            CfgInventoryDisplayMode.SettingChanged += OnStructuralConfigChanged;
            CfgAnchor.SettingChanged += OnLayoutConfigChanged;
            CfgOffsetX.SettingChanged += OnLayoutConfigChanged;
            CfgOffsetY.SettingChanged += OnLayoutConfigChanged;

            try { Config.Save(); } catch { }
        }

        private TeammateInventoryDisplayMode ReadInventoryDisplayMode()
        {
            try
            {
                var definition = new ConfigDefinition(DisplaySection, "EnableInventoryRow");
                if (Config.TryGetEntry<TeammateInventoryDisplayMode>(definition, out var modeEntry))
                    return modeEntry.Value;
                if (Config.TryGetEntry<bool>(definition, out var currentBool))
                    return currentBool.Value
                        ? TeammateInventoryDisplayMode.ContentsOnly
                        : TeammateInventoryDisplayMode.Disabled;

                if (OrphanedEntriesField != null)
                {
                    var orphans = OrphanedEntriesField.GetValue(Config)
                        as System.Collections.Generic.Dictionary<ConfigDefinition, string>;
                    if (orphans != null && orphans.TryGetValue(definition, out string raw))
                    {
                        if (Enum.TryParse(raw, true, out TeammateInventoryDisplayMode parsedMode))
                            return parsedMode;
                        if (bool.TryParse(raw, out bool oldEnabled))
                            return oldEnabled
                                ? TeammateInventoryDisplayMode.ContentsOnly
                                : TeammateInventoryDisplayMode.Disabled;
                    }
                }

                return ReadLegacyValue("Features", "EnableInventoryRow", true)
                    ? TeammateInventoryDisplayMode.ContentsOnly
                    : TeammateInventoryDisplayMode.Disabled;
            }
            catch
            {
                return TeammateInventoryDisplayMode.ContentsOnly;
            }
        }

        private void MigrateDefaultHudAnchor()
        {
            // 0.1.1 and earlier used TopLeft as the default. Convert that old default
            // on upgrade while preserving users who explicitly chose another corner.
            if (CfgAnchor == null || CfgAnchor.Value != HudAnchor.TopLeft) return;

            CfgAnchor.Value = HudAnchor.BottomLeft;
            try
            {
                Config.Save();
                PluginLogger.Info("Migrated the default HUD anchor from TopLeft to BottomLeft.");
            }
            catch (Exception ex)
            {
                PluginLogger.ThrottleWarn("anchor_migrate_save", "HUD anchor migration save failed: " + ex.Message);
            }
        }

        private T ReadLegacyValue<T>(string section, string key, T fallback)
        {
            try
            {
                if (Config.TryGetEntry<T>(section, key, out var oldEntry))
                    return oldEntry.Value;

                if (OrphanedEntriesField != null)
                {
                    var orphans = OrphanedEntriesField.GetValue(Config) as System.Collections.Generic.Dictionary<ConfigDefinition, string>;
                    if (orphans != null && orphans.TryGetValue(new ConfigDefinition(section, key), out string raw))
                    {
                        if (typeof(T).IsEnum)
                            return (T)Enum.Parse(typeof(T), raw, true);
                        return (T)Convert.ChangeType(raw, typeof(T), System.Globalization.CultureInfo.InvariantCulture);
                    }
                }
            }
            catch { }
            return fallback;
        }

        private void RemoveLegacyConfigEntries()
        {
            bool removed = false;
            removed |= RemoveConfigEntry("General", "Enabled");
            removed |= RemoveConfigEntry("Features", "EnableStaminaBar");
            removed |= RemoveConfigEntry("Features", "ShowStaminaValue");
            removed |= RemoveConfigEntry("Features", "EnableInventoryRow");
            removed |= RemoveConfigEntry("Features", "RoundStaminaValue");
            removed |= RemoveConfigEntry("Layout", "Anchor");
            removed |= RemoveConfigEntry("Layout", "OffsetX");
            removed |= RemoveConfigEntry("Layout", "OffsetY");
            removed |= RemoveConfigEntry("Nearby", "NearbyRange");
            removed |= RemoveConfigEntry("Nearby", "MaxNearbyCount");
            removed |= RemoveConfigEntry("Diagnostics", "DebugLogging");

            if (!removed) return;
            try
            {
                Config.Save();
                PluginLogger.Info("Migrated config sections to Display/Advanced.");
            }
            catch (Exception ex)
            {
                PluginLogger.ThrottleWarn("cfg_migrate_save", "Config migration save failed: " + ex.Message);
            }
        }

        private bool RemoveConfigEntry(string section, string key)
        {
            try
            {
                var definition = new ConfigDefinition(section, key);
                bool removed = false;
                if (Config.ContainsKey(definition))
                    removed = Config.Remove(definition);

                if (OrphanedEntriesField != null)
                {
                    var orphans = OrphanedEntriesField.GetValue(Config) as System.Collections.Generic.Dictionary<ConfigDefinition, string>;
                    if (orphans != null && orphans.Remove(definition))
                        removed = true;
                }

                return removed;
            }
            catch { return false; }
        }

        private void OnStructuralConfigChanged(object sender, EventArgs e)
        {
            OnAnyConfigChanged();
        }

        private void OnLayoutConfigChanged(object sender, EventArgs e)
        {
            try
            {
                if (TeammateBarsCoordinator.Instance != null)
                    TeammateBarsCoordinator.Instance.RefreshLayout();
            }
            catch (Exception ex)
            {
                PluginLogger.ThrottleError("layout_changed", "HUD layout refresh failed: " + ex.Message);
            }
        }

        private void OnAnyConfigChanged()
        {
            try
            {
                if (!CfgModEnabled.Value)
                {
                    if (TeammateBarsCoordinator.Instance != null)
                        TeammateBarsCoordinator.Instance.ClearAll();
                    IconSpriteCache.Clear();
                    return;
                }
                var tracker = TeamRosterTracker.EnsureExists();
                var coord = TeammateBarsCoordinator.EnsureExists();
                coord.Init();
                coord.AttachToTracker(tracker);
                coord.OnConfigChanged();
            }
            catch (Exception ex)
            {
                PluginLogger.ThrottleError("cfg_changed", "OnAnyConfigChanged failed: " + ex.Message);
            }
        }

        private IEnumerator DeferredLanguageRefresh()
        {
            yield return new WaitForSeconds(8f);

            bool newIsChinese = LanguageHelper.DetectChineseLanguage();
            if (newIsChinese == LanguageHelper.IsChinese) yield break;

            LanguageHelper.IsChinese = newIsChinese;
            ModConfigLocalization.ApplyLocalizedDescriptions();
            try { Config.Save(); } catch { }
            ModConfigLocalization.RefreshCache();
            PluginLogger.Info("Config descriptions updated to " + (LanguageHelper.IsChinese ? "Chinese" : "English") + ".");
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            try
            {
                IconSpriteCache.Clear();
                if (TeamRosterTracker.Instance != null)
                    TeamRosterTracker.Instance.ClearForSceneReload();
                if (TeammateBarsCoordinator.Instance != null)
                    TeammateBarsCoordinator.Instance.ClearAll();
                // 本地 StaminaBar 叠加文本的引用会随 HUD 销毁，重置后下次 Postfix 重建
                LocalStaminaBarPatch.ResetForSceneReload();
                PluginLogger.ClearThrottle();
            }
            catch (Exception ex)
            {
                PluginLogger.Error("OnSceneLoaded cleanup failed: " + ex.Message);
            }
        }

        private void Update()
        {
            // SafeTick：1 秒一次做兜底检查
            if (Time.unscaledTime < _nextSafeTick) return;
            _nextSafeTick = Time.unscaledTime + SafeTickInterval;
            SafeTick();
        }

        private void SafeTick()
        {
            try
            {
                if (!CfgModEnabled.Value) return;
                if (TeamRosterTracker.Instance == null) return;
        
                // 兜底：若协调器未建但进入关卡了，补建
                if (TeammateBarsCoordinator.Instance == null && Character.localCharacter != null)
                {
                    var tracker = TeamRosterTracker.Instance;
                    var coord = TeammateBarsCoordinator.EnsureExists();
                    coord.Init();
                    coord.AttachToTracker(tracker);
                    tracker.RequestRescan();
                    PluginLogger.ThrottleInfo("coord_fallback", "BarsCoordinator fallback-initialized in SafeTick.");
                }
            }
            catch (Exception ex)
            {
                PluginLogger.ThrottleError("safetick", "SafeTick failed: " + ex.Message);
            }
        }

        private void OnDestroy()
        {
            try
            {
                SceneManager.sceneLoaded -= OnSceneLoaded;
                if (CfgModEnabled != null) CfgModEnabled.SettingChanged -= OnStructuralConfigChanged;
                if (CfgEnableStaminaBar != null) CfgEnableStaminaBar.SettingChanged -= OnStructuralConfigChanged;
                if (CfgShowStaminaValue != null) CfgShowStaminaValue.SettingChanged -= OnStructuralConfigChanged;
                if (CfgInventoryDisplayMode != null) CfgInventoryDisplayMode.SettingChanged -= OnStructuralConfigChanged;
                if (CfgAnchor != null) CfgAnchor.SettingChanged -= OnLayoutConfigChanged;
                if (CfgOffsetX != null) CfgOffsetX.SettingChanged -= OnLayoutConfigChanged;
                if (CfgOffsetY != null) CfgOffsetY.SettingChanged -= OnLayoutConfigChanged;
                _harmony?.UnpatchSelf();
            }
            catch { }
        }
    }
}
