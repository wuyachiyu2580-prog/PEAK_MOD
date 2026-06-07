namespace Lantern_ShootZombies_Night
{
    /// <summary>
    /// 浮点数比较工具：统一全局精度常量，避免分散的 magic number。
    /// </summary>
    internal static class MathExtensions
    {
        /// <summary>全局浮点比较精度（替代各处硬编码的 0.001f / 0.01f）。</summary>
        public const float Epsilon = 0.001f;

        /// <summary>判断两个浮点数是否近似相等。</summary>
        public static bool Approximately(float a, float b)
        {
            float diff = a - b;
            return diff > -Epsilon && diff < Epsilon;
        }

        /// <summary>判断浮点数是否近似为零。</summary>
        public static bool IsZero(float value)
        {
            return value > -Epsilon && value < Epsilon;
        }
    }
}
