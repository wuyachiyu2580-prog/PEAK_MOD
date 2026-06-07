using System;
using System.IO;

namespace WhySoLaggy
{
    /// <summary>
    /// 独立日志写入器，所有性能日志写入 BepInEx/WhySoLaggy.log，
    /// 不与游戏原始 LogOutput.log 混合。
    /// </summary>
    internal static class LagLogger
    {
        private static StreamWriter _writer;
        private static readonly object _lock = new object();

        /// <summary>
        /// 初始化日志文件。每次启动覆盖写入（不追加）。
        /// </summary>
        public static void Initialize(string bepInExDir)
        {
            try
            {
                string path = Path.Combine(bepInExDir, "WhySoLaggy.log");
                _writer = new StreamWriter(path, append: false, encoding: System.Text.Encoding.UTF8)
                {
                    AutoFlush = true
                };
                _writer.WriteLine($"[{Timestamp()}] WhySoLaggy Performance Monitor started");
                _writer.WriteLine($"[{Timestamp()}] Log file: {path}");
                _writer.WriteLine(new string('=', 60));
            }
            catch (Exception ex)
            {
                BepInEx.Logging.Logger.CreateLogSource("WhySoLaggy")
                    .LogError($"Failed to create log file: {ex.Message}");
            }
        }

        /// <summary>
        /// 写入一行带时间戳的周期性/报告日志（Minimal 拦截，Normal 落盘）。
        /// </summary>
        public static void Write(string message)
        {
            if (_writer == null) return;
            if (!LogFilter.AllowLag()) return;
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
        /// 写入一行“系统事件类”日志（启动里程碑/错误/关键步骤），绕过 LogFilter，任何档都落盘。
        /// 语义同 AbuseLogger.Info，区别只在输出文件（WhySoLaggy.log vs WhySoLaggy_Abuse.log）。
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
        /// 写入一行不带时间戳的原始文本（用于报告内的续行）。
        /// </summary>
        public static void WriteRaw(string line)
        {
            if (_writer == null) return;
            lock (_lock)
            {
                try { _writer.WriteLine(line); }
                catch { }
            }
        }

        /// <summary>
        /// 强制刷新缓冲区。
        /// </summary>
        public static void Flush()
        {
            if (_writer == null) return;
            lock (_lock)
            {
                try { _writer.Flush(); }
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
                    _writer.WriteLine($"[{Timestamp()}] WhySoLaggy shutting down");
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
