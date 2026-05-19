using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace PlayersInfo.Helpers
{
    /// <summary>
    /// 计算异常状态的预期消除剩余时间（秒），仿 com.github.chuxiaaaa.StaminaInfo 实现。
    ///
    /// 规则：
    /// - 支持自动衰减的类型：Poison / Drowsy / Hot / Spores（用 status / reductionPerSecond + 未过期 cooldown 剩余）
    /// - Thorns 特殊：对所有还 stuckIn 的刺取 |Time.time - popOutTime| 的平均值（popOutTime 为未来时间戳）
    /// - 其余类型（Injury/Hunger/Cold/Crab/Curse/Weight/Web）返回 0，不显示时间
    /// </summary>
    internal static class AfflictionTimeHelper
    {
        private static FieldInfo _fiPopOutTime;
        private static bool _fiResolved;

        /// <summary>
        /// 返回异常预计多久后清零，秒。无法估算时返回 0。
        /// </summary>
        public static float GetReductionTimeRemaining(CharacterAfflictions ca, CharacterAfflictions.STATUSTYPE t)
        {
            if (ca == null) return 0f;

            if (t == CharacterAfflictions.STATUSTYPE.Thorns)
                return GetThornsRemaining(ca);

            try
            {
                float status = ca.GetCurrentStatus(t);
                if (status <= 0f) return 0f;
                float rate = GetRate(ca, t);
                if (rate <= 0f) return 0f;

                float cd = GetCooldown(ca, t);
                float lastAdded = 0f;
                try { lastAdded = ca.LastAddedStatus(t); } catch { }
                float now = Time.time;
                float reductionTime = status / rate;

                if (cd > 0f && now - lastAdded < cd)
                {
                    float cdLeft = cd - (now - lastAdded);
                    return cdLeft + reductionTime;
                }
                return reductionTime;
            }
            catch { return 0f; }
        }

        private static float GetThornsRemaining(CharacterAfflictions ca)
        {
            try
            {
                if (!_fiResolved)
                {
                    _fiResolved = true;
                    _fiPopOutTime = typeof(ThornOnMe).GetField("popOutTime",
                        BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                }
                if (_fiPopOutTime == null) return 0f;

                var list = ca.physicalThorns;
                if (list == null) return 0f;

                float sum = 0f;
                int cnt = 0;
                for (int i = 0; i < list.Count; i++)
                {
                    var th = list[i];
                    if (th == null || !th.stuckIn) continue;
                    float pop;
                    try { pop = (float)_fiPopOutTime.GetValue(th); } catch { continue; }
                    sum += Time.time - pop;
                    cnt++;
                }
                if (cnt <= 0) return 0f;
                return Mathf.Abs(sum) / cnt;
            }
            catch { return 0f; }
        }

        private static float GetRate(CharacterAfflictions ca, CharacterAfflictions.STATUSTYPE t)
        {
            switch (t)
            {
                case CharacterAfflictions.STATUSTYPE.Poison: return ca.poisonReductionPerSecond;
                case CharacterAfflictions.STATUSTYPE.Drowsy: return ca.drowsyReductionPerSecond;
                case CharacterAfflictions.STATUSTYPE.Hot:    return ca.hotReductionPerSecond;
                case CharacterAfflictions.STATUSTYPE.Spores: return ca.sporesReductionPerSecond;
                default: return 0f;
            }
        }

        private static float GetCooldown(CharacterAfflictions ca, CharacterAfflictions.STATUSTYPE t)
        {
            switch (t)
            {
                case CharacterAfflictions.STATUSTYPE.Poison: return ca.poisonReductionCooldown;
                case CharacterAfflictions.STATUSTYPE.Drowsy: return ca.drowsyReductionCooldown;
                case CharacterAfflictions.STATUSTYPE.Hot:    return ca.hotReductionCooldown;
                case CharacterAfflictions.STATUSTYPE.Spores: return ca.sporesReductionCooldown;
                default: return 0f;
            }
        }

        /// <summary>格式化为秒 / m:ss / h:mm；0 返回空串。</summary>
        public static string FormatTime(float seconds)
        {
            if (seconds <= 0f) return string.Empty;
            if (seconds < 60f) return Mathf.CeilToInt(seconds).ToString();
            if (seconds < 3600f)
            {
                int m = Mathf.FloorToInt(seconds / 60f);
                int s = Mathf.CeilToInt(seconds % 60f);
                if (s >= 60) { m += 1; s = 0; }
                return string.Format("{0}:{1:D2}", m, s);
            }
            int h = Mathf.FloorToInt(seconds / 3600f);
            int mm = Mathf.FloorToInt((seconds % 3600f) / 60f);
            return string.Format("{0}:{1:D2}", h, mm);
        }
    }
}
