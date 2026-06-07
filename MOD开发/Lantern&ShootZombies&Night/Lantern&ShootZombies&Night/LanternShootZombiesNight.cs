using System;
using System.Collections;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using ExitGames.Client.Photon;
using HarmonyLib;
using Lantern_ShootZombies_Night.Patches;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Lantern_ShootZombies_Night
{
    [BepInPlugin("com.wuyachiyu.LanternShootZombiesNight", "LanternShootZombiesNight", ModVersion)]
    [BepInDependency("HnskNoah.BlackPeakRemix")]
    [BepInDependency("com.github.Thanks.ShootZombies")]
    public class Plugin : BaseUnityPlugin, IOnEventCallback
    {
        public const string ModVersion = "0.2.1";

        public static Plugin Instance;

        // ── Config entries ──────────────────────────────────────────
        // Section: Preset (配置预设)
        public static ConfigEntry<ConfigPreset> ActivePreset;
        // Section: LanternCold (灯笼御寒)
        public static ConfigEntry<LanternFuelOption> LanternMaxFuel;
        public static ConfigEntry<bool> EnableWarmthReduction;
        public static ConfigEntry<float> LanternWarmthMultiplier;
        public static ConfigEntry<bool> EnableCampfireRefuel;
        // Section: Restore (回暖) — 仅保留打僵尸回暖
        public static ConfigEntry<bool> EnableWarmthRestore;
        public static ConfigEntry<int> HitRestoreWarmth;
        public static ConfigEntry<int> RestoreRadius;
        public static ConfigEntry<float> HitRestoreCooldown;
        public static ConfigEntry<ReserveWarmthRatio> ReserveWarmthMax;
        // Section: AutoRefill (灯熄灭自动回燃料)
        public static ConfigEntry<bool> AutoRefillEnabled;
        public static ConfigEntry<float> AutoRefillCapPercent;
        public static ConfigEntry<float> AutoRefillRate;
        public static ConfigEntry<bool> AutoRefillDaytimeOnly;
        public static ConfigEntry<bool> AutoRefillRequireHold;
        // Section: BugleUltimate (号角大招)
        public static ConfigEntry<bool> BugleUltimateEnabled;
        public static ConfigEntry<float> BugleUltimateCooldown;
        public static ConfigEntry<float> BugleUltimateRadius;
        public static ConfigEntry<float> BugleUltimateRestore;
        // Section: ItemSpawn (F8 生成)
        public static ConfigEntry<bool> PurgeExtraLanterns;
        public static ConfigEntry<bool> StrayBugleCleanupEnabled;
        public static ConfigEntry<float> StrayBugleDistance;
        public static ConfigEntry<float> StrayBugleGracePeriod;
        // Section: HUD (信息面板) — 归入 Advanced 分区
        public static ConfigEntry<bool> EnableHud;
        public static ConfigEntry<HudPosition> HudPos;
        public static ConfigEntry<HudSizePreset> HudSize;
        public static ConfigEntry<bool> ShowDayNightOnHud;
        // Section: Hotkey (快捷键)
        public static ConfigEntry<KeyCode> SpawnItemsKey;
        public static ConfigEntry<KeyCode> BugleRecallKey;
        // Section: DrainMultiplier (消耗倍率)
        public static ConfigEntry<float> FlashlightDrainMultiplier;
        public static ConfigEntry<float> CompanionDrainMultiplier;
        public static ConfigEntry<float> SoloDrainMultiplier;
        public static ConfigEntry<int> ProximityGracePeriod;
        // Section: Advanced (进阶) — 静音
        public static ConfigEntry<bool> MuteZombieTornado;
        public static ConfigEntry<bool> EnableFuelBroadcast;
        public static ConfigEntry<float> FuelBroadcastInterval;
        // Section: Upgrade (灯笼升级) — 自动升级，无需菜单热键
        public static ConfigEntry<bool> EnableUpgradeSystem;
        public static ConfigEntry<string> UpgradeLevelCostsCsv;
        public static ConfigEntry<string> UpgradeCapacityBonusCsv;
        public static ConfigEntry<string> UpgradeEfficiencyBonusCsv;
        public static ConfigEntry<float> UpgradePassiveTickInterval;
        public static ConfigEntry<int> UpgradePassivePointsPerTick;
        public static ConfigEntry<int> UpgradeHitPoints;
        public static ConfigEntry<int> UpgradeCampfirePoints;
        public static ConfigEntry<int> UpgradeBuglePoints;

        internal static ManualLogSource Log;

        void Awake()
        {
            Log = Logger;
            Log.LogInfo("[DEBUG] Plugin Awake start");

            // 0. Early game reflection (for language detection)
            ReflectionCache.InitGameReflectionEarly();

            // 0.5 Language detection
            LanguageHelper.IsChinese = LanguageHelper.DetectChineseLanguage();
            Log.LogInfo($"[DEBUG] Language detected: {(LanguageHelper.IsChinese ? "Chinese" : "English")}");

            // 1. Config bindings ── Lantern (灯笼：保暖/燃料/消耗/管理)
            LanternMaxFuel = Config.Bind("Lantern", "LanternMaxFuel", LanternFuelOption.Seconds120,
                LanguageHelper.L(
                    "Fuel capacity. GameDefault=unchanged, others=seconds, Infinite=never burns out.",
                    "燃料上限。默认=不改，其他=秒数，无限=永不熄灭。"));
            EnableWarmthReduction = Config.Bind("Lantern", "EnableWarmthReduction", true,
                LanguageHelper.L(
                    "Enable lantern cold-resistance multiplier below.",
                    "启用下方的灯笼抗寒倍率。"));
            LanternWarmthMultiplier = Config.Bind("Lantern", "LanternWarmthMultiplier", 0.75f,
                new ConfigDescription(
                    LanguageHelper.L(
                        "Cold resistance when holding lit lantern. 1=full block, 0.5=half cold, 0=no protection. Step=0.05",
                        "持灯抗寒能力。1=完全抵御，0.5=减半受冷，0=无保护。步进=0.05"),
                    new AcceptableValueRange<float>(0f, 1f)))
                .WithStep(0.05f);
            EnableCampfireRefuel = Config.Bind("Lantern", "EnableCampfireRefuel", true,
                LanguageHelper.L(
                    "Light campfire = refill lantern; near campfire = no fuel drain.",
                    "篅火补满&暂停消耗。"));
            ReserveWarmthMax = Config.Bind("Lantern", "ReserveWarmthMax", ReserveWarmthRatio.Half,
                LanguageHelper.L(
                    "Reserve = max × ratio. Overflow fills reserve; burning drains it first.",
                    "备用池 = 上限×比例。溢出存备用，燃烧优先消耗。"));
            // ── Restore: 仅保留打僵尸回暖 ──
            EnableWarmthRestore = Config.Bind("Restore", "EnableWarmthRestore", true,
                LanguageHelper.L(
                    "Master switch for warmth restore (zombie hit).",
                    "回暖总开关（打僵尸回暖）。"));
            HitRestoreWarmth = Config.Bind("Restore", "HitRestoreWarmth", 8,
                new ConfigDescription(
                    LanguageHelper.L(
                        "Warmth per zombie hit (seconds).",
                        "每次击杀僵尸回暖（秒）。"),
                    new AcceptableValueRange<int>(0, 30)));
            RestoreRadius = Config.Bind("Restore", "RestoreRadius", 50,
                new ConfigDescription(
                    LanguageHelper.L(
                        "Range for hit restore (meters).",
                        "击杀回暖范围（米）。"),
                    new AcceptableValueRange<int>(1, 200)));
            HitRestoreCooldown = Config.Bind("Restore", "HitRestoreCooldown", 0.3f,
                new ConfigDescription(
                    LanguageHelper.L(
                        "Min interval between hit restores (seconds). Step=0.1",
                        "击杀回暖最小间隔（秒）。步进=0.1"),
                    new AcceptableValueRange<float>(0.1f, 5f)))
                .WithStep(0.1f);

            // ── Lantern (灯熄后自动回燃料) ──
            AutoRefillEnabled = Config.Bind("Lantern", "AutoRefillEnabled", true,
                LanguageHelper.L(
                    "When lantern is out of fuel, slowly refill up to a cap.",
                    "灯熄灭后自动缓慢回燃料至上限。"));
            AutoRefillCapPercent = Config.Bind("Lantern", "AutoRefillCapPercent", 0.5f,
                new ConfigDescription(
                    LanguageHelper.L(
                        "Refill upper cap (ratio of max fuel). Step=0.05",
                        "自动回料上限（占满燃料比例）。步进=0.05"),
                    new AcceptableValueRange<float>(0.1f, 1f)))
                .WithStep(0.05f);
            AutoRefillRate = Config.Bind("Lantern", "AutoRefillRate", 1.0f,
                new ConfigDescription(
                    LanguageHelper.L(
                        "Seconds of fuel gained per real second. Step=0.1",
                        "每现实秒回复的燃料秒数。步进=0.1"),
                    new AcceptableValueRange<float>(0.1f, 5f)))
                .WithStep(0.1f);
            AutoRefillDaytimeOnly = Config.Bind("Lantern", "AutoRefillDaytimeOnly", false,
                LanguageHelper.L(
                    "Only refill during daytime.",
                    "仅在白天自动回料。"));
            AutoRefillRequireHold = Config.Bind("Lantern", "AutoRefillRequireHold", false,
                LanguageHelper.L(
                    "Only refill while a player is holding the lantern.",
                    "仅在玩家持有灯时自动回料。"));

            // ── Bugle (号角大招 + 号角清理) ──
            BugleUltimateEnabled = Config.Bind("Bugle", "BugleUltimateEnabled", true,
                LanguageHelper.L(
                    "Enable bugle ultimate: nearby players' lanterns refill, long global cooldown.",
                    "启用号角大招：附近玩家灯笼回满，全局长CD。"));
            BugleUltimateCooldown = Config.Bind("Bugle", "BugleUltimateCooldown", 180f,
                new ConfigDescription(
                    LanguageHelper.L(
                        "Global cooldown between bugle ultimates (seconds).",
                        "号角大招CD（秒），全局共享。"),
                    new AcceptableValueRange<float>(30f, 600f)));
            BugleUltimateRadius = Config.Bind("Bugle", "BugleUltimateRadius", 20f,
                new ConfigDescription(
                    LanguageHelper.L(
                        "Ultimate effect radius (meters).",
                        "大招生效范围（米）。"),
                    new AcceptableValueRange<float>(5f, 100f)));
            BugleUltimateRestore = Config.Bind("Bugle", "BugleUltimateRestore", -1f,
                new ConfigDescription(
                    LanguageHelper.L(
                        "Warmth gained per ultimate. -1 means refill to max.",
                        "单次大招回暖秒数。-1 表示回满。"),
                    new AcceptableValueRange<float>(-1f, 300f)));

            // ── Lantern (多余灯笼清理) / Bugle (流浪号角清理) ──
            PurgeExtraLanterns = Config.Bind("Lantern", "PurgeExtraLanterns", true,
                LanguageHelper.L(
                    "Auto destroy duplicate lanterns on a player (Faerie lantern exempt).",
                    "自动销毁玩家身上的多余灯笼（仙子提灯豁免）。"));
            StrayBugleCleanupEnabled = Config.Bind("Bugle", "StrayBugleCleanupEnabled", true,
                LanguageHelper.L(
                    "Host cleans stray (unheld, far away) bugles after grace period.",
                    "房主自动清理无人持有且偏远的号角。"));
            StrayBugleDistance = Config.Bind("Bugle", "StrayBugleDistance", 100f,
                new ConfigDescription(
                    LanguageHelper.L(
                        "Distance from nearest player to count as stray (meters).",
                        "距最近玩家大于此距离才算偏远（米）。"),
                    new AcceptableValueRange<float>(30f, 500f)));
            StrayBugleGracePeriod = Config.Bind("Bugle", "StrayBugleGracePeriod", 60f,
                new ConfigDescription(
                    LanguageHelper.L(
                        "Must stay stray for N seconds before cleanup.",
                        "持续N秒满足条件才清理。"),
                    new AcceptableValueRange<float>(10f, 600f)));
            // ── Display (显示与操作) — 本地设置，不同步
            ActivePreset = Config.Bind("Display", "ActivePreset", ConfigPreset.Custom,
                LanguageHelper.L(
                    "Config preset. Custom=manual, Casual=easy, Balanced=default, Hardcore=challenging. Switching applies all values at once.",
                    "配置预设。自定义=手动调节，休闲=轻松，平衡=默认值，硬核=高挑战。切换时一键覆盖所有平衡相关配置。"));
            EnableHud = Config.Bind("Display", "EnableHud", true,
                LanguageHelper.L(
                    "Show visual HUD panel (fuel bar, multipliers, status).",
                    "显示可视化信息面板（燃料条、倍率、状态）。"));
            HudPos = Config.Bind("Display", "HudPos", HudPosition.Bottom,
                LanguageHelper.L(
                    "HUD panel screen position (8 positions).",
                    "HUD面板屏幕位置（8方位）。"));
            HudSize = Config.Bind("Display", "HudSize", HudSizePreset.ExtraLarge,
                LanguageHelper.L(
                    "HUD panel size preset (Small/Medium/Large/ExtraLarge).",
                    "HUD面板尺寸预设（小/中/大/超大）。"));
            ShowDayNightOnHud = Config.Bind("Display", "ShowDayNightOnHud", true,
                LanguageHelper.L(
                    "Show day/night info on HUD (day count, time, BPR darkness).",
                    "在HUD上显示日夜信息（天数、时间、BPR黑暗状态）。"));
            SpawnItemsKey = Config.Bind("Display", "SpawnItemsKey", KeyCode.F8,
                LanguageHelper.L(
                    "Hotkey to spawn Bugle & Backpack if missing. Set to None to disable.",
                    "快捷键生成号角和背包（身上没有时）。设为None可禁用。"));
            BugleRecallKey = Config.Bind("Display", "BugleRecallKey", KeyCode.F7,
                LanguageHelper.L(
                    "Host-only hotkey: destroy ALL bugles in the scene (held or dropped). Set to None to disable.",
                    "房主专属快捷键：销毁场景里所有号角（含被持有/掉落的）。设为None可禁用。"));

            // ── Lantern (消耗倍率) ──
            FlashlightDrainMultiplier = Config.Bind("Lantern", "FlashlightDrainMultiplier", 1.2f,
                new ConfigDescription(
                    LanguageHelper.L(
                        "Fuel drain multiplier when BPR flashlight mode is active. 1=normal, 2=double. Step=0.05",
                        "BPR手电筒模式激活时的燃料消耗倍率。1=正常，2=双倍消耗。步进=0.05"),
                    new AcceptableValueRange<float>(1f, 2f)))
                .WithStep(0.05f);
            CompanionDrainMultiplier = Config.Bind("Lantern", "CompanionDrainMultiplier", 0.8f,
                new ConfigDescription(
                    LanguageHelper.L(
                        "Drain multiplier when companions are nearby. More players = stronger effect (scales to 3). Step=0.05",
                        "附近有同伴时的消耗倍率。人越多效果越强（最多按3人计算）。步进=0.05"),
                    new AcceptableValueRange<float>(0.5f, 1f)))
                .WithStep(0.05f);
            SoloDrainMultiplier = Config.Bind("Lantern", "SoloDrainMultiplier", 1.2f,
                new ConfigDescription(
                    LanguageHelper.L(
                        "Drain multiplier when alone. Step=0.05",
                        "独自行动时的消耗倍率。步进=0.05"),
                    new AcceptableValueRange<float>(1f, 1.5f)))
                .WithStep(0.05f);
            ProximityGracePeriod = Config.Bind("Lantern", "ProximityGracePeriod", 15,
                new ConfigDescription(
                    LanguageHelper.L(
                        "Seconds before companion/solo multiplier activates after state change.",
                        "状态切换后，结伴/独行倍率生效前的等待秒数。"),
                    new AcceptableValueRange<int>(10, 120)));

            MuteZombieTornado = Config.Bind("Display", "MuteZombieTornado", false,
                LanguageHelper.L(
                    "Mute all zombie and tornado sounds.",
                    "静音所有僵尸和龙卷风的声音。"));

            EnableFuelBroadcast = Config.Bind("Lantern", "EnableFuelBroadcast", true,
                LanguageHelper.L(
                    "Broadcast owned lantern fuel to keep the local LSN HUD correct when Photon ownership changes.",
                    "广播自己拥有的灯笼燃料，确保 Photon 主权变化时本地 LSN HUD 仍能显示正确耐久。"));
            FuelBroadcastInterval = Config.Bind("Lantern", "FuelBroadcastInterval", 2f,
                new ConfigDescription(
                    LanguageHelper.L(
                        "Fuel sync interval in seconds. 1-2 seconds is recommended for multiplayer.",
                        "燃料同步间隔（秒）。多人建议 1-2 秒。"),
                    new AcceptableValueRange<float>(1f, 10f)));

            EnableUpgradeSystem = Config.Bind("Upgrade", "EnableUpgradeSystem", true,
                LanguageHelper.L(
                    "Enable lantern upgrade system. Points auto-spend to upgrade (capacity first, then efficiency).",
                    "启用灯笼升级系统。点数足够时自动升级（先容量再效率）。"));

            // ── Upgrade numeric tuning ──
            UpgradeLevelCostsCsv = Config.Bind("Upgrade", "LevelCostsCsv", "30,60,90,120,150",
                LanguageHelper.L(
                    "Point cost per level (comma separated, 5 values).",
                    "每级升级所需点数（逗号分隔，5 个值）。"));
            UpgradeCapacityBonusCsv = Config.Bind("Upgrade", "CapacityBonusCsv", "0.15,0.3,0.45,0.6,0.75",
                LanguageHelper.L(
                    "Capacity bonus per level (comma separated, 5 values). Final multiplier = 1 + value.",
                    "每级容量加成（5 个值）。最终倍率 = 1 + 值。"));
            UpgradeEfficiencyBonusCsv = Config.Bind("Upgrade", "EfficiencyBonusCsv", "0.1,0.2,0.3,0.4,0.5",
                LanguageHelper.L(
                    "Efficiency bonus per level (comma separated, 5 values). Final drain multiplier = 1 - value.",
                    "每级效率加成（5 个值）。最终消耗倍率 = 1 - 值。"));
            UpgradePassiveTickInterval = Config.Bind("Upgrade", "PassiveTickInterval", 30f,
                LanguageHelper.L(
                    "Passive points accrual interval in seconds.",
                    "被动累积点数的间隔秒数。"));
            UpgradePassivePointsPerTick = Config.Bind("Upgrade", "PassivePointsPerTick", 1,
                LanguageHelper.L(
                    "Points gained per passive tick.",
                    "每次被动累积获得的点数。"));
            UpgradeHitPoints = Config.Bind("Upgrade", "HitPoints", 1,
                LanguageHelper.L(
                    "Points gained per zombie hit.",
                    "每次打僵尸获得的点数。"));
            UpgradeCampfirePoints = Config.Bind("Upgrade", "CampfirePoints", 5,
                LanguageHelper.L(
                    "Points gained per campfire light-up.",
                    "每次点燃篝火获得的点数。"));
            UpgradeBuglePoints = Config.Bind("Upgrade", "BuglePoints", 3,
                LanguageHelper.L(
                    "Points gained per bugle ultimate trigger.",
                    "每次号角大招触发获得的点数。"));

            Log.LogInfo("[DEBUG] Config bound OK");

            // 1.5 Room config sync
            Instance = this;
            RoomConfigSync.Initialize(this);

            // 2. Harmony patches
            Harmony harmony = new Harmony("com.wuyachiyu.LanternShootZombiesNight");

            try { harmony.PatchAll(typeof(ReduceLanternWarmthPatch)); Log.LogInfo("[DEBUG] Patch OK: ReduceLanternWarmthPatch"); }
            catch (Exception ex) { Log.LogError($"[DEBUG] Patch FAILED: ReduceLanternWarmthPatch - {ex.Message}"); }
            
            try { harmony.PatchAll(typeof(HeatEmissionMultiplierPatch)); Log.LogInfo("[DEBUG] Patch OK: HeatEmissionMultiplierPatch"); }
            catch (Exception ex) { Log.LogError($"[DEBUG] Patch FAILED: HeatEmissionMultiplierPatch - {ex.Message}"); }

            try { harmony.PatchAll(typeof(SubtractColdMonitorPatch)); Log.LogInfo("[DEBUG] Patch OK: SubtractColdMonitorPatch"); }
            catch (Exception ex) { Log.LogError($"[DEBUG] Patch FAILED: SubtractColdMonitorPatch - {ex.Message}"); }

            try { harmony.PatchAll(typeof(StatusFieldColdPatch)); Log.LogInfo("[DEBUG] Patch OK: StatusFieldColdPatch"); }
            catch (Exception ex) { Log.LogError($"[DEBUG] Patch FAILED: StatusFieldColdPatch - {ex.Message}"); }
            
            try { harmony.PatchAll(typeof(LocalDartFuelPatch)); Log.LogInfo("[DEBUG] Patch OK: LocalDartFuelPatch (DartImpact)"); }
            catch (Exception ex) { Log.LogError($"[DEBUG] Patch FAILED: LocalDartFuelPatch - {ex.Message}"); }

            try { harmony.PatchAll(typeof(RpcDartFuelPatch)); Log.LogInfo("[DEBUG] Patch OK: RpcDartFuelPatch (RPC_DartImpact)"); }
            catch (Exception ex) { Log.LogError($"[DEBUG] Patch FAILED: RpcDartFuelPatch - {ex.Message}"); }

            try { harmony.PatchAll(typeof(BugleUltimatePatch)); Log.LogInfo("[DEBUG] Patch OK: BugleUltimatePatch"); }
            catch (Exception ex) { Log.LogError($"[DEBUG] Patch FAILED: BugleUltimatePatch - {ex.Message}"); }

            try { harmony.PatchAll(typeof(CampfireLightRefuelPatch)); Log.LogInfo("[DEBUG] Patch OK: CampfireLightRefuelPatch"); }
            catch (Exception ex) { Log.LogError($"[DEBUG] Patch FAILED: CampfireLightRefuelPatch - {ex.Message}"); }

            try { harmony.PatchAll(typeof(CampfireAwakePatch)); Log.LogInfo("[DEBUG] Patch OK: CampfireAwakePatch"); }
            catch (Exception ex) { Log.LogError($"[DEBUG] Patch FAILED: CampfireAwakePatch - {ex.Message}"); }

            try { CampfireDestroyPatch.TryApply(harmony); }
            catch (Exception ex) { Log.LogError($"[DEBUG] Patch FAILED: CampfireDestroyPatch - {ex.Message}"); }

            try { harmony.PatchAll(typeof(LanternFuelOverridePatch)); Log.LogInfo("[DEBUG] Patch OK: LanternFuelOverridePatch"); }
            catch (Exception ex) { Log.LogError($"[DEBUG] Patch FAILED: LanternFuelOverridePatch - {ex.Message}"); }

            try { harmony.PatchAll(typeof(MuteZombieSfxPatch)); Log.LogInfo("[DEBUG] Patch OK: MuteZombieSfxPatch"); }
            catch (Exception ex) { Log.LogError($"[DEBUG] Patch FAILED: MuteZombieSfxPatch - {ex.Message}"); }

            try { harmony.PatchAll(typeof(MuteTornadoSfxPatch)); Log.LogInfo("[DEBUG] Patch OK: MuteTornadoSfxPatch"); }
            catch (Exception ex) { Log.LogError($"[DEBUG] Patch FAILED: MuteTornadoSfxPatch - {ex.Message}"); }

            try { ModConfigLocalization.PatchDisplayNames(harmony); Log.LogInfo("[DEBUG] Patch OK: ModConfig display names"); }
            catch (Exception ex) { Log.LogWarning($"[DEBUG] ModConfig display name patch skipped: {ex.Message}"); }

            // 3. Dependency detection & reflection cache
            ModIntegration.Initialize();
            ReflectionCache.Initialize(ModIntegration.IsBprLoaded, ModIntegration.IsSzLoaded);

            // 3.3 UI 组件
            gameObject.AddComponent<LanternUpgradeUI>();

            // 3.4 燃料广播器（跟 Plugin 同生同死）
            gameObject.AddComponent<LanternFuelBroadcaster>();

            // 3.5 Performance fixes for dependency mods
            if (ModIntegration.IsBprLoaded)
            {
                try { BprPerformanceFix.Apply(harmony); }
                catch (Exception ex) { Log.LogError($"[DEBUG] BprPerformanceFix FAILED: {ex.Message}"); }

                try { DispelFogFieldGuardPatch.TryApply(harmony); }
                catch (Exception ex) { Log.LogError($"[DEBUG] DispelFogFieldGuardPatch FAILED: {ex.Message}"); }
            }
            if (ModIntegration.IsSzLoaded)
            {
                try { ShootZombiesPerformanceFix.Apply(harmony); }
                catch (Exception ex) { Log.LogError($"[DEBUG] ShootZombiesPerformanceFix FAILED: {ex.Message}"); }
            }

            PhotonNetwork.AddCallbackTarget(this);
            SceneManager.sceneLoaded += OnSceneLoaded;
            Log.LogInfo("[DEBUG] Plugin Awake done");

            // Config summary for debugging (grouped by the 5 sections: Lantern / Restore / Bugle / Upgrade / Display)
            Log.LogInfo("[DEBUG] ═══ Config Summary ═══");
            Log.LogInfo($"[DEBUG]   [Lantern/Cold] MaxFuel={LanternMaxFuel.Value}, Reduction={EnableWarmthReduction.Value}, Multiplier={LanternWarmthMultiplier.Value:F2}");
            Log.LogInfo($"[DEBUG]   [Lantern/AutoRefill] Enabled={AutoRefillEnabled.Value}, Cap={AutoRefillCapPercent.Value:F2}, Rate={AutoRefillRate.Value:F2}/s, DaytimeOnly={AutoRefillDaytimeOnly.Value}, RequireHold={AutoRefillRequireHold.Value}");
            Log.LogInfo($"[DEBUG]   [Lantern/Drain] Flashlight={FlashlightDrainMultiplier.Value:F2}, Companion={CompanionDrainMultiplier.Value:F2}, Solo={SoloDrainMultiplier.Value:F2}, GracePeriod={ProximityGracePeriod.Value}s");
            Log.LogInfo($"[DEBUG]   [Lantern/Purge] PurgeExtra={PurgeExtraLanterns.Value}");
            Log.LogInfo($"[DEBUG]   [Restore] Enabled={EnableWarmthRestore.Value}, Hit={HitRestoreWarmth.Value}s, Radius={RestoreRadius.Value}m, Cooldown={HitRestoreCooldown.Value:F1}s, Reserve={ReserveWarmthMax.Value}, Campfire={EnableCampfireRefuel.Value}");
            Log.LogInfo($"[DEBUG]   [Bugle/Ult] Enabled={BugleUltimateEnabled.Value}, CD={BugleUltimateCooldown.Value:F0}s, Radius={BugleUltimateRadius.Value:F0}m, Restore={BugleUltimateRestore.Value:F0}s (-1=refillMax)");
            Log.LogInfo($"[DEBUG]   [Bugle/Stray] Cleanup={StrayBugleCleanupEnabled.Value}, Dist={StrayBugleDistance.Value:F0}m, Grace={StrayBugleGracePeriod.Value:F0}s");
            Log.LogInfo($"[DEBUG]   [Upgrade] Enabled={EnableUpgradeSystem.Value} (auto-upgrade, capacity first)");
            Log.LogInfo($"[DEBUG]   [Display] HUD={EnableHud.Value} Pos={HudPos.Value} Size={HudSize.Value} DayNight={ShowDayNightOnHud.Value} Mute={MuteZombieTornado.Value} Preset={ActivePreset.Value}");
            Log.LogInfo("[DEBUG] ═══════════════════════");

            // [调试探针] Thanks.Fog&ColdControl 检测
            try
            {
                if (Patches.ThanksFogColdControlProbe.IsInstalled)
                    Log.LogInfo($"[DEBUG] Thanks.Fog&ColdControl detected: {Patches.ThanksFogColdControlProbe.Snapshot()}");
                else
                    Log.LogInfo("[DEBUG] Thanks.Fog&ColdControl NOT installed on this instance.");
            }
            catch (Exception ex) { Log.LogWarning($"[DEBUG] ThanksProbe init failed: {ex.Message}"); }

            // 4. Deferred language re-check
            StartCoroutine(DeferredLanguageRefresh());
        }

        private IEnumerator DeferredLanguageRefresh()
        {
            yield return new WaitForSeconds(8f);

            bool newIsChinese = LanguageHelper.DetectChineseLanguage();
            Log?.LogInfo($"[DEBUG] DeferredLangRefresh: was={(LanguageHelper.IsChinese ? "zh" : "en")}, now={(newIsChinese ? "zh" : "en")}");

            if (newIsChinese != LanguageHelper.IsChinese)
            {
                LanguageHelper.IsChinese = newIsChinese;
                ModConfigLocalization.ApplyLocalizedDescriptions();
                try { Config.Save(); } catch { }
                Log?.LogInfo($"[DEBUG] Config descriptions updated to {(LanguageHelper.IsChinese ? "Chinese" : "English")}");

                ModConfigLocalization.RefreshCache();
            }
        }

        void Update()
        {
            SafeTick(LanternHud.Tick, nameof(LanternHud));
            SafeTick(FlashlightDrainMonitor.Tick, nameof(FlashlightDrainMonitor));
            SafeTick(ProximityDrainMonitor.Tick, nameof(ProximityDrainMonitor));
            SafeTick(DayNightTracker.Tick, nameof(DayNightTracker));
            SafeTick(NightColdWarning.Tick, nameof(NightColdWarning));
            SafeTick(ExtraLanternPurger.Tick, nameof(ExtraLanternPurger));
            SafeTick(StrayBugleCleaner.Tick, nameof(StrayBugleCleaner));
            SafeTick(LanternUpgradeSystem.Tick, nameof(LanternUpgradeSystem) + ".Tick");
            SafeTick(RoomConfigSync.UpdateSync, nameof(RoomConfigSync) + ".UpdateSync");
            SafeTick(LanternUpgradeSystem.SyncFromNetworkIfNeeded, nameof(LanternUpgradeSystem) + ".SyncFromNetworkIfNeeded");

            // 快捷键：生成缺少的灯/号角/背包
            // Unity 6 禁用了 Legacy Input，必须使用 BepInEx.UnityInput 兼容层
            if (SpawnItemsKey.Value != KeyCode.None && UnityInput.Current.GetKeyDown(SpawnItemsKey.Value))
                ItemSpawnHelper.TrySpawnMissingItems();

            // 快捷键：房主召回所有号角
            if (BugleRecallKey.Value != KeyCode.None && UnityInput.Current.GetKeyDown(BugleRecallKey.Value))
                BugleRecaller.TryRecall();
        }

        // 任意一个 Tick 抛异常都不影响后面的执行，也不会刷屏（同一模块 5秒内只记一条）
        private static readonly System.Collections.Generic.Dictionary<string, float> _tickErrorThrottle
            = new System.Collections.Generic.Dictionary<string, float>();

        private static void SafeTick(Action tick, string label)
        {
            try { tick(); }
            catch (Exception ex)
            {
                float now = Time.unscaledTime;
                if (!_tickErrorThrottle.TryGetValue(label, out float last) || now - last > 5f)
                {
                    _tickErrorThrottle[label] = now;
                    Log?.LogError($"[DEBUG] [Tick] {label} crashed: {ex.Message}\n{ex.StackTrace}");
                }
            }
        }

        void OnDestroy()
        {
            PhotonNetwork.RemoveCallbackTarget(this);
            SceneManager.sceneLoaded -= OnSceneLoaded;
            RoomConfigSync.Cleanup();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // 跨局清理与旧实例 ID / Guid / ViewID 绑定的字典，避免连续多局内存缓慢增长。
            try
            {
                LanternHelper.ResetAccumulatedState();
                Patches.LanternFuelOverridePatch.ResetAccumulatedState();
                ExtraLanternPurger.ResetAccumulatedState();
                LanternFuelSync.Reset();
                Log?.LogInfo($"[DEBUG] [Scene] Loaded '{scene.name}' ({mode}) — accumulated dictionaries reset");
            }
            catch (Exception ex) { Log?.LogError($"[DEBUG] [Scene] Reset FAILED: {ex.Message}"); }
        }

        // ── IOnEventCallback ─────────────────────────────────────
        public void OnEvent(EventData photonEvent)
        {
            if (photonEvent.Code == LanternUpgradeSystem.UpgradeEventCode)
                LanternUpgradeSystem.HandleUpgradeEvent(photonEvent);
            else if (photonEvent.Code == LanternFuelSync.EventCode)
                LanternFuelSync.HandleEvent(photonEvent);
            // 188 is LSN internal fuel sync for the local lantern HUD.
        }
    }
}
