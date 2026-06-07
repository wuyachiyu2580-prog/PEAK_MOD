using UnityEngine;

namespace Lantern_ShootZombies_Night
{
    /// <summary>
    /// 日夜跟踪器：从 DayNightManager 读取游戏内部时间，
    /// 通过反射读取 BPR 黑暗状态，供 HUD 显示。
    /// </summary>
    internal static class DayNightTracker
    {
        // ── 游戏状态（每帧从 DayNightManager 读取）──
        public static int CurrentDay;        // mgr.dayCount
        public static bool IsDaytime;        // mgr.isDay > 0.5f
        public static float TimeOfDay;       // mgr.timeOfDay (0-24)

        // ── 日夜过渡日志 ──
        private static bool _prevDaytime = true;

        // ── BPR 黑暗状态 ──
        public static bool IsBprDark;

        // ── BPR 反射节流（250ms 读一次，与 HUD 刷新率对齐）──
        private static float _lastBprCheckTime = -999f;
        private const float BprCheckInterval = 0.25f;

        /// <summary>每帧由 Plugin.Update 调用。</summary>
        public static void Tick()
        {
            var mgr = DayNightManager.instance;
            if (mgr == null)
            {
                CurrentDay = 0;
                IsDaytime = true;
                TimeOfDay = 0f;
                IsBprDark = false;
                return;
            }

            CurrentDay = mgr.dayCount;
            IsDaytime = mgr.isDay > 0.5f;
            TimeOfDay = mgr.timeOfDay;

            // 日夜过渡日志（每天仅触发2次）
            if (_prevDaytime != IsDaytime)
            {
                _prevDaytime = IsDaytime;
                Plugin.Log?.LogInfo($"[DEBUG] [DayNight] Transition → {(IsDaytime ? "DAY" : "NIGHT")} (day={CurrentDay}, time={TimeOfDay:F1})");
            }

            // BPR 黑暗状态读取（节流：每 500ms 通过 ModIntegration 读一次）
            if (ModIntegration.IsBprLoaded && Time.time - _lastBprCheckTime >= BprCheckInterval)
            {
                _lastBprCheckTime = Time.time;
                IsBprDark = ModIntegration.IsBprDark();
            }
        }

        /// <summary>格式化 HUD 显示字符串。</summary>
        public static string FormatForHud(bool zh)
        {
            if (CurrentDay <= 0)
                return "";

            // 时段名称
            string period = GetPeriodName(TimeOfDay, zh);

            // 时间格式 HH:MM
            int hours = Mathf.FloorToInt(TimeOfDay) % 24;
            int minutes = Mathf.FloorToInt((TimeOfDay - Mathf.Floor(TimeOfDay)) * 60f);
            string timeStr = $"{hours:D2}:{minutes:D2}";

            // 日夜颜色
            string dayColor = IsDaytime ? "#FFFF88" : "#8888FF";

            // BPR 暗标记
            string bprMark = "";
            if (IsBprDark)
                bprMark = zh ? " <color=#FF6666>(暗)</color>" : " <color=#FF6666>(Dark)</color>";

            return $"<color={dayColor}>D{CurrentDay} {period} {timeStr}</color>{bprMark}";
        }

        /// <summary>根据时间返回时段名称。</summary>
        private static string GetPeriodName(float time, bool zh)
        {
            if (time >= 5.0f && time < 11.5f)
                return zh ? "早晨" : "Morn";
            if (time >= 11.5f && time < 17.5f)
                return zh ? "下午" : "Aftn";
            if (time >= 17.5f && time < 21.0f)
                return zh ? "傍晚" : "Eve";
            return zh ? "深夜" : "Night";
        }
    }
}
