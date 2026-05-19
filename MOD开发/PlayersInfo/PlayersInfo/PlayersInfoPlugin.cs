using System;
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
        public const string PluginVersion = "0.1.1";

        public enum HudAnchor { TopLeft, TopRight, BottomLeft, BottomRight }

        // ========== Config Entries（全局共享） ==========
        public static ConfigEntry<bool> CfgModEnabled;

        public static ConfigEntry<bool> CfgEnableStaminaBar;
        public static ConfigEntry<bool> CfgShowStaminaValue;

        public static ConfigEntry<bool> CfgEnableInventoryRow;

        public static ConfigEntry<HudAnchor> CfgAnchor;
        public static ConfigEntry<float> CfgOffsetX;
        public static ConfigEntry<float> CfgOffsetY;

        // 附近玩家过滤
        public static ConfigEntry<float> CfgNearbyRange;
        public static ConfigEntry<int> CfgMaxNearbyCount;
        public static ConfigEntry<bool> CfgRoundStamina;
        public static ConfigEntry<bool> CfgDebugLogging;

        private Harmony _harmony;
        private float _nextSafeTick;
        private const float SafeTickInterval = 1f;

        private void Awake()
        {
            try
            {
                PluginLogger.Log = Logger;
                LanguageHelper.IsChinese = LanguageHelper.DetectChineseLanguage();

                BindConfig();

                _harmony = new Harmony(PluginGuid);
                _harmony.PatchAll();

                SceneManager.sceneLoaded += OnSceneLoaded;

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
            CfgModEnabled = Config.Bind("General",
                "Enabled", true,
                LanguageHelper.L("Master switch. Turn off to disable all features.",
                                 "总开关，关闭后全部功能失效。"));

            CfgEnableStaminaBar = Config.Bind("Features",
                "EnableStaminaBar", true,
                LanguageHelper.L("Show teammate stamina bars (with extra stamina).",
                                 "显示队友体力条（含临时体力）。"));

            CfgShowStaminaValue = Config.Bind("Features",
                "ShowStaminaValue", true,
                LanguageHelper.L("Show numeric stamina value on the bar.",
                                 "在体力条右侧显示数值。"));

            CfgEnableInventoryRow = Config.Bind("Features",
                "EnableInventoryRow", true,
                LanguageHelper.L("Show teammate inventory (main 3 + temp + backpack + backpack inner 4).",
                                 "显示队友物品栏（主 3 + 临时 + 背包槽 + 背包内部 4 格）。"));

            CfgAnchor = Config.Bind("Layout",
                "Anchor", HudAnchor.TopLeft,
                LanguageHelper.L("HUD anchor corner on screen.",
                                 "HUD 屏幕锚点位置。"));

            CfgOffsetX = Config.Bind("Layout",
                "OffsetX", 0f,
                new ConfigDescription(
                    LanguageHelper.L("Additional X offset in pixels.", "X 方向像素偏移。"),
                    new AcceptableValueRange<float>(-800f, 800f)));

            CfgOffsetY = Config.Bind("Layout",
                "OffsetY", 0f,
                new ConfigDescription(
                    LanguageHelper.L("Additional Y offset in pixels.", "Y 方向像素偏移。"),
                    new AcceptableValueRange<float>(-800f, 800f)));

            CfgNearbyRange = Config.Bind("Nearby",
                "NearbyRange", 30f,
                new ConfigDescription(
                    LanguageHelper.L("Max distance (meters) to show a teammate bar. 0 = unlimited.",
                                     "显示队友体力条的最大距离（米）。0 表示不限。"),
                    new AcceptableValueRange<float>(0f, 500f)));

            CfgMaxNearbyCount = Config.Bind("Nearby",
                "MaxNearbyCount", 3,
                new ConfigDescription(
                    LanguageHelper.L("Max number of nearest teammates to show bars for.",
                                     "最多显示几个最近的队友体力条。"),
                    new AcceptableValueRange<int>(0, 8)));

            CfgRoundStamina = Config.Bind("Features",
                "RoundStaminaValue", true,
                LanguageHelper.L("If true, round stamina numeric value to nearest integer; else 1 decimal.",
                                 "为 true 时体力数值四舍五入到整数，否则显示 1 位小数。"));

            CfgDebugLogging = Config.Bind("Diagnostics",
                "DebugLogging", false,
                LanguageHelper.L("Enable verbose diagnostic logs for distance, stamina, inventory, and cloned UI internals.",
                                 "Enable verbose diagnostic logs for distance, stamina, inventory, and cloned UI internals."));

            // 只订阅真正影响"克隆体结构"的配置项变化，避免任意配置改动（OffsetX 拖滑块、
            // BepInEx 启动回写、ConfigurationManager 实时事件）触发 ClearAll → 全部体力条一起跳。
            // 运行时数值类（NearbyRange/MaxNearbyCount/RoundStamina/DebugLogging/Anchor/Offset）
            // 由 Update 直接读 Cfg.Value 生效，无需事件。
            CfgModEnabled.SettingChanged       += OnStructuralConfigChanged;
            CfgShowStaminaValue.SettingChanged += OnStructuralConfigChanged;
            CfgEnableInventoryRow.SettingChanged += OnStructuralConfigChanged;
        }

        private void OnStructuralConfigChanged(object sender, EventArgs e)
        {
            OnAnyConfigChanged();
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
                _harmony?.UnpatchSelf();
            }
            catch { }
        }
    }
}
