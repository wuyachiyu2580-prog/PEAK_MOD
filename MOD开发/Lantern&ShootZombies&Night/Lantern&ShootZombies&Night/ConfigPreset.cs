namespace Lantern_ShootZombies_Night
{
    /// <summary>
    /// 配置预设枚举。切换预设时自动批量设置相关配置项。
    /// Custom = 不自动设置，玩家自行调整。
    /// </summary>
    public enum ConfigPreset
    {
        /// <summary>自定义（不自动设置）。</summary>
        Custom = 0,
        /// <summary>休闲：低消耗、高回暖、长燃烧。</summary>
        Casual = 1,
        /// <summary>平衡：适中消耗与回暖（默认）。</summary>
        Balanced = 2,
        /// <summary>硬核：高消耗、低回暖、短燃烧。</summary>
        Hardcore = 3
    }
}
