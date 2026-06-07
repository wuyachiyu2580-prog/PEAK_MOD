using System;
using System.IO;

namespace WhySoLaggy
{
    /// <summary>
    /// 独立的炸房/滥用日志写入器，所有监测日志写入 BepInEx/WhySoLaggy_Abuse.log，
    /// 与性能日志 (WhySoLaggy.log) 分开。
    /// </summary>
    internal static class AbuseLogger
    {
        private static StreamWriter _writer;
        private static readonly object _lock = new object();

        /// <summary>
        /// 初始化日志文件。每次启动覆盖写入。
        /// </summary>
        public static void Initialize(string bepInExDir)
        {
            try
            {
                string path = Path.Combine(bepInExDir, "WhySoLaggy_Abuse.log");
                _writer = new StreamWriter(path, append: false, encoding: System.Text.Encoding.UTF8)
                {
                    AutoFlush = true
                };
                _writer.WriteLine($"[{Timestamp()}] WhySoLaggy Abuse Detection Monitor started");
                _writer.WriteLine($"[{Timestamp()}] Log file: {path}");
                _writer.WriteLine(new string('=', 60));
            }
            catch (Exception ex)
            {
                BepInEx.Logging.Logger.CreateLogSource("WhySoLaggy")
                    .LogError($"Failed to create abuse log file: {ex.Message}");
            }
        }

        /// <summary>
        /// 写入“系统事件类”日志（监测器 Initialize / 错误 / Hook 里程碑 / 关键发现）。
        /// 绕过 LogFilter，任何档都落盘 —— 语义对应 LagLogger.Info。
        /// </summary>
        public static void Info(string message)
        {
            if (_writer == null) return;
            lock (_lock)
            {
                try { _writer.WriteLine($"[{Timestamp()}] {message}"); }
                catch { }
            }
        }

        /// <summary>
        /// 写入一行日志（自动添加时间戳前缀）。
        /// </summary>
        public static void Write(string message)
        {
            if (_writer == null) return;
            if (!LogFilter.AllowAbuse()) return;
            lock (_lock)
            {
                try
                {
                    _writer.WriteLine($"[{Timestamp()}] {message}");
                }
                catch { /* swallow IO errors during gameplay */ }
            }
        }

        /// <summary>
        /// 写入告警日志（带 ⚠ 前缀），同时输出到 BepInEx 控制台。
        /// Alert 绕过 LogFilter：即使 Minimal 模式也必须落盘。
        /// </summary>
        public static void Alert(string message)
        {
            string full = $"⚠ ABUSE ALERT: {message}";
            if (_writer != null)
            {
                lock (_lock)
                {
                    try { _writer.WriteLine($"[{Timestamp()}] {full}"); }
                    catch { /* swallow IO errors during gameplay */ }
                }
            }
            // 同时输出到 BepInEx 控制台让玩家看到
            WhySoLaggyPlugin.Log?.LogWarning($"[WHY_LAG] {full}");
            // 推送到浮窗（1.0.3）
            try { PerformanceDashboard.ReportAlert(message); } catch { }
        }

        /// <summary>
        /// 写入告警详情行（紧跟 Alert 的上下文行，如 Top RPC sources / Top methods）。
        /// 跟 Alert 一样绕过 LogFilter，保证 Minimal 模式也能看到定位所需的证据。
        /// </summary>
        public static void AlertDetail(string message)
        {
            if (_writer == null) return;
            lock (_lock)
            {
                try { _writer.WriteLine($"[{Timestamp()}] {message}"); }
                catch { /* swallow IO errors during gameplay */ }
            }
        }

        /// <summary>
        /// 写入告警详情的纯文本续行（无时间戳前缀），同样绕过 LogFilter。
        /// 典型用法：AlertDetail("[ABUSE] Top X sources:") + N * AlertDetailRaw("          NAME: Yx")。
        /// </summary>
        public static void AlertDetailRaw(string line)
        {
            if (_writer == null) return;
            lock (_lock)
            {
                try { _writer.WriteLine(line); }
                catch { }
            }
        }

        /// <summary>
        /// 写入一行不带时间戳的原始文本（用于报告续行）。
        /// 与 Write() 同样受 AllowAbuse 过滤，避免 Minimal 模式下出现孤儿续行。
        /// </summary>
        public static void WriteRaw(string line)
        {
            if (_writer == null) return;
            if (!LogFilter.AllowAbuse()) return;
            lock (_lock)
            {
                try { _writer.WriteLine(line); }
                catch { }
            }
        }

        /// <summary>
        /// 关闭文件句柄。
        /// </summary>
        public static void Shutdown()
        {
            if (_writer == null) return;
            lock (_lock)
            {
                try
                {
                    _writer.WriteLine($"[{Timestamp()}] WhySoLaggy Abuse Detection shutting down");
                    _writer.Flush();
                    _writer.Close();
                    _writer = null;
                }
                catch { }
            }
        }

        private static string Timestamp()
        {
            return DateTime.Now.ToString("HH:mm:ss.fff");
        }
    }
}
