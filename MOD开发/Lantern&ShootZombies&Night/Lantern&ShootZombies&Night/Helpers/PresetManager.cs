namespace Lantern_ShootZombies_Night
{
    /// <summary>
    /// 配置预设管理器。根据选中的预设枚举值批量设置所有关联配置项。
    /// Custom = 不自动设置；其他预设一键覆盖所有游戏平衡相关配置。
    /// 注意：HUD / 静音 / 快捷键等个人偏好不受预设影响。
    /// 0.2.0 重平衡：回暖来源收窄到"打僵尸 + 号角大招 + 自动回料"。
    /// </summary>
    internal static class PresetManager
    {
        private static bool _applying;

        /// <summary>是否正在应用预设（防止 SettingChanged 重入）。</summary>
        public static bool IsApplying => _applying;

        public static void ApplyPreset(ConfigPreset preset)
        {
            if (preset == ConfigPreset.Custom) return;

            _applying = true;
            try
            {
                switch (preset)
                {
                    case ConfigPreset.Casual:   ApplyCasual();   break;
                    case ConfigPreset.Balanced: ApplyBalanced(); break;
                    case ConfigPreset.Hardcore: ApplyHardcore(); break;
                }
                try { Plugin.Instance?.Config?.Save(); } catch { }
                Plugin.Log?.LogInfo($"[PresetManager] Applied preset: {preset}");
            }
            finally { _applying = false; }
        }

        // ── Casual: 简单难度（也是首次安装的默认值） ───────────
        private static void ApplyCasual()
        {
            Set(Plugin.LanternMaxFuel, LanternFuelOption.Seconds120);
            Set(Plugin.EnableWarmthReduction, true);
            Set(Plugin.LanternWarmthMultiplier, 0.75f);
            Set(Plugin.EnableCampfireRefuel, true);
            Set(Plugin.ReserveWarmthMax, ReserveWarmthRatio.Half);

            Set(Plugin.EnableWarmthRestore, true);
            Set(Plugin.HitRestoreWarmth, 8);
            Set(Plugin.RestoreRadius, 50);
            Set(Plugin.HitRestoreCooldown, 0.3f);

            Set(Plugin.AutoRefillEnabled, true);
            Set(Plugin.AutoRefillCapPercent, 0.5f);
            Set(Plugin.AutoRefillRate, 1.0f);
            Set(Plugin.AutoRefillDaytimeOnly, false);
            Set(Plugin.AutoRefillRequireHold, false);

            Set(Plugin.BugleUltimateEnabled, true);
            Set(Plugin.BugleUltimateCooldown, 180f);
            Set(Plugin.BugleUltimateRadius, 20f);
            Set(Plugin.BugleUltimateRestore, -1f);

            Set(Plugin.PurgeExtraLanterns, true);
            Set(Plugin.StrayBugleCleanupEnabled, true);
            Set(Plugin.StrayBugleDistance, 100f);
            Set(Plugin.StrayBugleGracePeriod, 60f);

            Set(Plugin.FlashlightDrainMultiplier, 1.2f);
            Set(Plugin.CompanionDrainMultiplier, 0.8f);
            Set(Plugin.SoloDrainMultiplier, 1.2f);
            Set(Plugin.ProximityGracePeriod, 15);

            Set(Plugin.EnableUpgradeSystem, true);
            Set(Plugin.UpgradeLevelCostsCsv, "30,60,90,120,150");
            Set(Plugin.UpgradeCapacityBonusCsv, "0.15,0.3,0.45,0.6,0.75");
            Set(Plugin.UpgradeEfficiencyBonusCsv, "0.1,0.2,0.3,0.4,0.5");
            Set(Plugin.UpgradePassiveTickInterval, 30f);
            Set(Plugin.UpgradePassivePointsPerTick, 1);
            Set(Plugin.UpgradeHitPoints, 1);
            Set(Plugin.UpgradeCampfirePoints, 5);
            Set(Plugin.UpgradeBuglePoints, 3);
            Set(Plugin.MuteZombieTornado, false);
        }

        // ── Balanced: 中等难度（以 Casual 为基线稍添压力） ──
        private static void ApplyBalanced()
        {
            Set(Plugin.LanternMaxFuel, LanternFuelOption.Seconds120);
            Set(Plugin.EnableWarmthReduction, true);
            Set(Plugin.LanternWarmthMultiplier, 0.6f);
            Set(Plugin.EnableCampfireRefuel, true);
            Set(Plugin.ReserveWarmthMax, ReserveWarmthRatio.Half);

            Set(Plugin.EnableWarmthRestore, true);
            Set(Plugin.HitRestoreWarmth, 6);
            Set(Plugin.RestoreRadius, 45);
            Set(Plugin.HitRestoreCooldown, 0.4f);

            Set(Plugin.AutoRefillEnabled, true);
            Set(Plugin.AutoRefillCapPercent, 0.45f);
            Set(Plugin.AutoRefillRate, 0.7f);
            Set(Plugin.AutoRefillDaytimeOnly, false);
            Set(Plugin.AutoRefillRequireHold, true);

            Set(Plugin.BugleUltimateEnabled, true);
            Set(Plugin.BugleUltimateCooldown, 220f);
            Set(Plugin.BugleUltimateRadius, 18f);
            Set(Plugin.BugleUltimateRestore, -1f);

            Set(Plugin.PurgeExtraLanterns, true);
            Set(Plugin.StrayBugleCleanupEnabled, true);
            Set(Plugin.StrayBugleDistance, 90f);
            Set(Plugin.StrayBugleGracePeriod, 55f);

            Set(Plugin.FlashlightDrainMultiplier, 1.4f);
            Set(Plugin.CompanionDrainMultiplier, 0.85f);
            Set(Plugin.SoloDrainMultiplier, 1.3f);
            Set(Plugin.ProximityGracePeriod, 25);

            Set(Plugin.EnableUpgradeSystem, true);
            Set(Plugin.UpgradeLevelCostsCsv, "45,90,140,200,260");
            Set(Plugin.UpgradeCapacityBonusCsv, "0.18,0.36,0.54,0.72,0.9");
            Set(Plugin.UpgradeEfficiencyBonusCsv, "0.1,0.2,0.3,0.4,0.5");
            Set(Plugin.UpgradePassiveTickInterval, 45f);
            Set(Plugin.UpgradePassivePointsPerTick, 1);
            Set(Plugin.UpgradeHitPoints, 1);
            Set(Plugin.UpgradeCampfirePoints, 5);
            Set(Plugin.UpgradeBuglePoints, 3);
            Set(Plugin.MuteZombieTornado, false);
        }

        // ── Hardcore: 高难度（明显加压，但均基于 Casual 框架渐进） ──
        private static void ApplyHardcore()
        {
            Set(Plugin.LanternMaxFuel, LanternFuelOption.Seconds90);
            Set(Plugin.EnableWarmthReduction, true);
            Set(Plugin.LanternWarmthMultiplier, 0.4f);
            Set(Plugin.EnableCampfireRefuel, true);
            Set(Plugin.ReserveWarmthMax, ReserveWarmthRatio.Quarter);

            Set(Plugin.EnableWarmthRestore, true);
            Set(Plugin.HitRestoreWarmth, 4);
            Set(Plugin.RestoreRadius, 35);
            Set(Plugin.HitRestoreCooldown, 0.5f);

            Set(Plugin.AutoRefillEnabled, true);
            Set(Plugin.AutoRefillCapPercent, 0.35f);
            Set(Plugin.AutoRefillRate, 0.4f);
            Set(Plugin.AutoRefillDaytimeOnly, true);
            Set(Plugin.AutoRefillRequireHold, true);

            Set(Plugin.BugleUltimateEnabled, true);
            Set(Plugin.BugleUltimateCooldown, 280f);
            Set(Plugin.BugleUltimateRadius, 15f);
            Set(Plugin.BugleUltimateRestore, -1f);

            Set(Plugin.PurgeExtraLanterns, true);
            Set(Plugin.StrayBugleCleanupEnabled, true);
            Set(Plugin.StrayBugleDistance, 80f);
            Set(Plugin.StrayBugleGracePeriod, 45f);

            Set(Plugin.FlashlightDrainMultiplier, 1.7f);
            Set(Plugin.CompanionDrainMultiplier, 0.9f);
            Set(Plugin.SoloDrainMultiplier, 1.4f);
            Set(Plugin.ProximityGracePeriod, 40);

            Set(Plugin.EnableUpgradeSystem, true);
            Set(Plugin.UpgradeLevelCostsCsv, "60,120,190,270,360");
            Set(Plugin.UpgradeCapacityBonusCsv, "0.2,0.4,0.6,0.8,1.0");
            Set(Plugin.UpgradeEfficiencyBonusCsv, "0.08,0.16,0.24,0.32,0.4");
            Set(Plugin.UpgradePassiveTickInterval, 60f);
            Set(Plugin.UpgradePassivePointsPerTick, 1);
            Set(Plugin.UpgradeHitPoints, 1);
            Set(Plugin.UpgradeCampfirePoints, 4);
            Set(Plugin.UpgradeBuglePoints, 2);
            Set(Plugin.MuteZombieTornado, false);
        }

        private static void Set<T>(BepInEx.Configuration.ConfigEntry<T> entry, T value)
        {
            if (entry != null) entry.Value = value;
        }
    }
}
