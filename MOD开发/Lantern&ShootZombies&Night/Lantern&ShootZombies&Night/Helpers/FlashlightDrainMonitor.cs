using System;
using UnityEngine;

namespace Lantern_ShootZombies_Night
{
    /// <summary>
    /// 监控 BlackPeakRemix 手电筒模式，在手电筒开启时增加灯笼燃料消耗。
    /// 通过反射访问 BPR 的 LanternSyncHelper.localInstanceMode 字段，
    /// 避免对 BPR 程序集的硬引用。
    /// </summary>
    internal static class FlashlightDrainMonitor
    {
        private const string DrainSourceKey = "flashlight";

        // ── 节流（200ms 检测一次，手电筒状态不会帧间变化）──
        private static float _lastTickTime = -999f;
        private const float TickInterval = 0.2f;

        // ── 状态追踪 ──
        private static bool _wasFlashlight;

        /// <summary>每帧由 Plugin.Update 调用。</summary>
        public static void Tick()
        {
            // 前提：BPR 必须已加载
            if (!ModIntegration.IsBprLoaded) return;

            // ── 节流：200ms 检测一次 ──
            if (Time.time - _lastTickTime < TickInterval) return;
            _lastTickTime = Time.time;

            bool isFlashlight = false;
            Character local = Character.localCharacter;
            if (local != null)
            {
                CharacterData data = local.data;
                Item currentItem = (data != null) ? data.currentItem : null;
                if (currentItem != null && currentItem.itemID == 42)
                {
                    isFlashlight = ModIntegration.IsFlashlightActive(currentItem);
                }
                else
                {
                    ModIntegration.ClearSyncHelperCache();
                }
            }

            // 更新 drain 源
            float configMultiplier = Plugin.FlashlightDrainMultiplier.Value;
            if (isFlashlight && configMultiplier > 1f)
            {
                LanternHelper.SetDrainSource(DrainSourceKey, configMultiplier);
            }
            else
            {
                LanternHelper.RemoveDrainSource(DrainSourceKey);
            }

            // ── 调试日志 ──
            if (isFlashlight != _wasFlashlight)
            {
                _wasFlashlight = isFlashlight;
                float finalDrain = LanternHelper.FuelDrainMultiplier;
                if (isFlashlight)
                    Plugin.Log?.LogInfo($"[DEBUG] [FlashlightDrain] Flashlight ON → drain source set to {configMultiplier:F2}x (final={finalDrain:F2}x)");
                else
                    Plugin.Log?.LogInfo($"[DEBUG] [FlashlightDrain] Flashlight OFF → drain source removed (final={finalDrain:F2}x)");
            }
        }
    }
}
