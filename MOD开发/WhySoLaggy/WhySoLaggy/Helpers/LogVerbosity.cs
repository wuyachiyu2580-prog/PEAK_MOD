namespace WhySoLaggy
{
    /// <summary>
    /// 日志详细度（1.0.4 精简为两档）：
    /// - <see cref="Minimal"/>（默认）：只落盘“事件类”——ABUSE ALERT 及其详情、监测器启动/错误/关键里程碑
    ///   （LagLogger.Info / AbuseLogger.Alert / AlertDetail / AlertDetailRaw / Info 均绕过过滤）。
    /// - <see cref="Normal"/>：Minimal + “周期量化类”——RpcMonitor 周期报告、PatchProfiler / PluginProfiler / FpsTracker 报告、帧尖峰明细。
    /// 
    /// 注：1.0.3 及更早版本的 Verbose 档已移除；旧配置里若残留该值，BepInEx 反序列化会自动回落到默认 Minimal。
    /// </summary>
    public enum LogVerbosity
    {
        /// <summary>默认：仅事件/异常/启动里程碑。</summary>
        Minimal = 0,
        /// <summary>Minimal + 周期量化报告 + 帧尖峰明细。</summary>
        Normal = 1,
    }

    /// <summary>
    /// 全局日志过滤器。由 WhySoLaggyPlugin.Awake 根据配置设置 <see cref="Level"/>。
    /// Write(周期报告类) 内部调用 <see cref="AllowLag"/> / <see cref="AllowAbuse"/>；
    /// Info/Alert/AlertDetail/AlertDetailRaw 均 *不* 调用过滤，任何档都落盘。
    /// </summary>
    public static class LogFilter
    {
        /// <summary>当前详细度。默认 Minimal。</summary>
        public static LogVerbosity Level = LogVerbosity.Minimal;

        /// <summary>LagLogger.Write 是否允许落盘（Minimal 完全拦截；Verbose 等同 Normal）。</summary>
        public static bool AllowLag()
        {
            return (int)Level >= (int)LogVerbosity.Normal;
        }

        /// <summary>AbuseLogger.Write / WriteRaw 是否允许落盘（Minimal 只放行 Info/Alert/AlertDetail*）。</summary>
        public static bool AllowAbuse()
        {
            return (int)Level >= (int)LogVerbosity.Normal;
        }
    }
}
