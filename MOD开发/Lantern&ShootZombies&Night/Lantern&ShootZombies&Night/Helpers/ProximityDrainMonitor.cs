using Photon.Pun;
using UnityEngine;

namespace Lantern_ShootZombies_Night
{
    /// <summary>
    /// 监控玩家周围是否有同伴，据此动态调整灯笼燃料消耗倍率。
    /// - 结伴：附近持续有其他玩家一段时间后，消耗降低（0.5~1x），人越多越低
    /// - 独行：独自行动持续一段时间后，消耗增加（1~1.5x）
    /// - 状态切换后的过渡期间保持 1x 不变
    /// </summary>
    internal static class ProximityDrainMonitor
    {
        private const string CompanionSourceKey = "companion";
        private const string SoloSourceKey = "solo";
        private const float CheckInterval = 1f;   // 每秒检测一次附近玩家
        private const int MaxCompanionScale = 3;   // 最多按 3 人计算满额缩放

        // ── 状态追踪 ──
        private static bool _hasCompanion;       // 当前是否有同伴
        private static float _stateTimer;        // 当前状态持续秒数
        private static bool _companionActive;    // 结伴倍率是否已激活
        private static bool _soloActive;         // 独行倍率是否已激活
        private static int _companionCount;      // 当前附近同伴数
        private static float _lastCheckTime = -999f;

        /// <summary>每帧由 Plugin.Update 调用。</summary>
        public static void Tick()
        {
            // 单人模式 或 多人房仅1人 → 跳过检测，清理已有倍率源
            if (!PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom == null
                || PhotonNetwork.CurrentRoom.PlayerCount <= 1)
            {
                if (_companionActive || _soloActive)
                {
                    LanternHelper.RemoveDrainSource(CompanionSourceKey);
                    LanternHelper.RemoveDrainSource(SoloSourceKey);
                    _companionActive = false;
                    _soloActive = false;
                    _hasCompanion = false;
                    _stateTimer = 0f;
                    _companionCount = 0;
                    Plugin.Log?.LogInfo("[DEBUG] [ProximityDrain] Solo/offline room → disabled");
                }
                return;
            }

            Character local = Character.localCharacter;
            if (local == null) return;

            _stateTimer += Time.deltaTime;

            // 节流：每秒检测一次附近玩家数
            if (Time.time - _lastCheckTime >= CheckInterval)
            {
                _lastCheckTime = Time.time;

                float radius = Plugin.RestoreRadius.Value;
                int count = CountNearbyPlayers(local, radius);
                bool hasCompanion = count > 0;

                if (hasCompanion != _hasCompanion)
                {
                    // 状态切换 → 重置计时器，移除两个倍率源
                    _hasCompanion = hasCompanion;
                    _companionCount = count;
                    _stateTimer = 0f;
                    _companionActive = false;
                    _soloActive = false;
                    LanternHelper.RemoveDrainSource(CompanionSourceKey);
                    LanternHelper.RemoveDrainSource(SoloSourceKey);

                    float finalDrain = LanternHelper.FuelDrainMultiplier;
                    Plugin.Log?.LogInfo($"[DEBUG] [ProximityDrain] State → {(hasCompanion ? $"COMPANION(count={count})" : "SOLO")}, timer reset, finalDrain={finalDrain:F2}x");
                    return;
                }

                _companionCount = count;

                // 已激活时实时更新同伴人数带来的倍率变化
                if (_companionActive && _hasCompanion)
                {
                    float configMult = Plugin.CompanionDrainMultiplier.Value;
                    float scale = Mathf.Clamp01((float)_companionCount / MaxCompanionScale);
                    float newMult = Mathf.Lerp(1f, configMult, scale);
                    LanternHelper.SetDrainSource(CompanionSourceKey, newMult);
                }
            }

            // 检查是否到达生效时间
            TryActivate();
        }

        /// <summary>检查生效时间是否到达，激活对应倍率源。</summary>
        private static void TryActivate()
        {
            float gracePeriod = Plugin.ProximityGracePeriod.Value;
            if (_stateTimer < gracePeriod) return;

            if (_hasCompanion && !_companionActive)
            {
                float configMult = Plugin.CompanionDrainMultiplier.Value;
                // 按同伴人数线性缩放：1人=1/3效果，2人=2/3效果，3+人=满额
                float scale = Mathf.Clamp01((float)_companionCount / MaxCompanionScale);
                float appliedMult = Mathf.Lerp(1f, configMult, scale);

                LanternHelper.SetDrainSource(CompanionSourceKey, appliedMult);
                LanternHelper.RemoveDrainSource(SoloSourceKey);
                _companionActive = true;
                _soloActive = false;

                float totalDrain = LanternHelper.FuelDrainMultiplier;
                Plugin.Log?.LogInfo($"[DEBUG] [ProximityDrain] Companion ACTIVATED: count={_companionCount}, config={configMult:F2}x, applied={appliedMult:F2}x (finalDrain={totalDrain:F2}x)");
            }
            else if (!_hasCompanion && !_soloActive)
            {
                float configMult = Plugin.SoloDrainMultiplier.Value;

                LanternHelper.SetDrainSource(SoloSourceKey, configMult);
                LanternHelper.RemoveDrainSource(CompanionSourceKey);
                _soloActive = true;
                _companionActive = false;

                float totalDrain = LanternHelper.FuelDrainMultiplier;
                Plugin.Log?.LogInfo($"[DEBUG] [ProximityDrain] Solo ACTIVATED: config={configMult:F2}x (finalDrain={totalDrain:F2}x)");
            }
        }

        /// <summary>统计范围内其他玩家数量。</summary>
        private static int CountNearbyPlayers(Character local, float radius)
        {
            int count = 0;
            Vector3 localPos = local.Center;
            foreach (Character ch in Character.AllCharacters)
            {
                if (ch == null || ch == local) continue;
                if (ch.data != null && ch.data.dead) continue;
                if (Vector3.Distance(localPos, ch.Center) <= radius)
                    count++;
            }
            return count;
        }
    }
}
