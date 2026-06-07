namespace Lantern_ShootZombies_Night
{
    /// <summary>
    /// 回暖来源记录（供 HUD 显示）。
    /// 0.2.0 起精简为：Hit / Bugle / Campfire / Reserve / AutoRefill 五种来源。
    /// 不再做"多样性检查"；升级点数改由 LanternUpgradeSystem 自己维护。
    /// </summary>
    internal static class RestoreTracker
    {
        private static string _lastSource;
        private static float _lastWarmth;
        private static float _lastTime;
        private static float _lastAutoRefillTime = -999f;

        /// <summary>上次回暖来源键 (Hit/Bugle/Campfire/Reserve/AutoRefill)。</summary>
        public static string LastSource => _lastSource;

        /// <summary>上次回暖值（秒）。≤ 0 表示 REFILL_MAX。</summary>
        public static float LastWarmth => _lastWarmth;

        /// <summary>上次回暖时间（Time.time）。</summary>
        public static float LastTime => _lastTime;

        /// <summary>上次 AutoRefill Tick 时间（Time.time）。用于 HUD 显示"回血中…"。</summary>
        public static float LastAutoRefillTime => _lastAutoRefillTime;

        /// <summary>记录一次成功的回暖事件。warmth ≤ 0 表示全满。</summary>
        public static void ReportLast(string sourceKey, float warmth)
        {
            _lastSource = sourceKey;
            _lastWarmth = warmth;
            _lastTime = UnityEngine.Time.time;
        }

        /// <summary>标记 AutoRefill 正在生效（HUD 据此显示小文案）。</summary>
        public static void ReportAutoRefillActive()
        {
            _lastAutoRefillTime = UnityEngine.Time.time;
        }

        /// <summary>清理物品名（去掉 "(Clone)" 后缀）。保留给日志使用。</summary>
        public static string CleanItemName(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return raw;
            int idx = raw.IndexOf("(Clone)");
            return idx >= 0 ? raw.Substring(0, idx).Trim() : raw.Trim();
        }
    }
}
