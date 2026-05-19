using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace WhySoLaggy
{
    /// <summary>
    /// 结构化日志写入器（1.0.3 新增，1.0.3 FieldProbe 追加 Snapshot 列）。
    /// - whysolaggy_data.csv：固定表头（匹配字段按列名填充，其余留空，全字段 CSV 转义）。
    /// - whysolaggy_events.jsonl：每行一个 JSON 对象，字段名全小写，保留原始数值类型。
    /// - 每 10 条事件自动 Flush 一次。
    /// - 写入前检查文件大小，超过 MaxLogFileSizeMB 则轮转为带时间戳备份并重开。
    /// - 所有写入在静态锁 <see cref="_lock"/> 内执行。
    /// </summary>
    internal static class StructuredLogger
    {
        /// <summary>文件大小上限（MB），触发后轮转。由 Plugin 读取配置传入。</summary>
        public static int MaxLogFileSizeMB = 10;

        // 固定 CSV 列顺序（1.0.4 扩展为 50 列，补齐 AllocRateKBps/Ping/PeriodicReport 字段/TopOwners/Top 插件归因）。
        private static readonly string[] CsvColumns =
        {
            "Timestamp", "FrameNumber", "Type",
            "AvgFps", "MinFps", "MaxFps", "AvgFrameMs", "SpikeThresholdMs", "SpikeCount", "ReportDuration",
            "AllocRateKBps", "Ping",
            "Name", "AvgMs", "TotalMs", "CallCount", "Owner",
            "TopPluginName", "TopPluginMs",
            "AlertType", "Rate", "Threshold", "Delta", "CurrentCount",
            "TopActor", "TopActorName", "TopOwners",
            "TotalInstantiates", "TotalDestroys", "TotalRpcs", "AlertCount",
            "RoomName", "PlayerCount", "MaxPlayers", "ZombieCount",
            "RpcMethod", "SenderActor", "SenderName", "TargetViewID", "TargetName",
            "PayloadBytes", "ArgsSummary", "SpecificDesc",
            "TargetMethod", "PatchType", "OwnerHarmonyId", "Priority",
            "TraceStack", "TraceCaller",
            "Snapshot",
            // 1.0.3 新增：InstantiateTrace 专用字段（抓刷物品源头）
            "PrefabName", "IsMasterClient", "Position", "LocalActor",
            // 1.0.3 新增：Master 端关联到的客户端请求者（用于 InstantiateTrace 和 RemoteRpcTrace）
            "SuspectedRequesterActor", "SuspectedRequesterName", "SuspectedRequesterRpc", "SuspectedAgeMs",
        };

        private static readonly object _lock = new object();
        private static StreamWriter _csvWriter;
        private static StreamWriter _jsonlWriter;
        private static string _dir;
        private static string _csvPath;
        private static string _jsonlPath;
        private static int _pendingFlush;
        private static bool _inited;

        // 复用缓冲（均在 _lock 内使用，线程安全）。
        private static readonly StringBuilder _csvSb = new StringBuilder(512);
        private static readonly StringBuilder _jsonSb = new StringBuilder(512);

        public static void Initialize(string dir)
        {
            lock (_lock)
            {
                if (_inited) return;
                try
                {
                    _dir = dir;
                    _csvPath = Path.Combine(dir, "whysolaggy_data.csv");
                    _jsonlPath = Path.Combine(dir, "whysolaggy_events.jsonl");

                    bool needHeader = !File.Exists(_csvPath) || new FileInfo(_csvPath).Length == 0;
                    _csvWriter = new StreamWriter(_csvPath, append: true, encoding: Encoding.UTF8) { AutoFlush = false };
                    _jsonlWriter = new StreamWriter(_jsonlPath, append: true, encoding: Encoding.UTF8) { AutoFlush = false };

                    if (needHeader)
                    {
                        _csvWriter.WriteLine(string.Join(",", CsvColumns));
                        _csvWriter.Flush();
                    }
                    _inited = true;
                }
                catch (Exception ex)
                {
                    WhySoLaggyPlugin.Log?.LogError($"[WHY_LAG] StructuredLogger init failed: {ex.Message}");
                    _csvWriter = null;
                    _jsonlWriter = null;
                }
            }
        }

        public static void WriteEvent(StructuredEvent evt)
        {
            if (!_inited) return;
            lock (_lock)
            {
                if (_csvWriter == null || _jsonlWriter == null) return;
                try
                {
                    RotateIfNeeded();
                    WriteCsvRow(evt);
                    WriteJsonlRow(evt);
                    if (++_pendingFlush >= 10)
                    {
                        _csvWriter.Flush();
                        _jsonlWriter.Flush();
                        _pendingFlush = 0;
                    }
                }
                catch (Exception ex)
                {
                    WhySoLaggyPlugin.Log?.LogWarning($"[WHY_LAG] StructuredLogger write failed: {ex.Message}");
                }
            }
        }

        public static void Flush()
        {
            if (!_inited) return;
            lock (_lock)
            {
                try
                {
                    _csvWriter?.Flush();
                    _jsonlWriter?.Flush();
                    _pendingFlush = 0;
                }
                catch { }
            }
        }

        public static void Shutdown()
        {
            lock (_lock)
            {
                try
                {
                    _csvWriter?.Flush();
                    _csvWriter?.Close();
                    _jsonlWriter?.Flush();
                    _jsonlWriter?.Close();
                }
                catch { }
                _csvWriter = null;
                _jsonlWriter = null;
                _inited = false;
            }
        }

        // ── 内部：CSV 行 ──
        private static void WriteCsvRow(StructuredEvent evt)
        {
            _csvSb.Clear();
            // Fields 为 null 时视为空字典
            var f = evt.Fields;
            for (int i = 0; i < CsvColumns.Length; i++)
            {
                if (i > 0) _csvSb.Append(',');
                string col = CsvColumns[i];
                object v;
                if (col == "Timestamp") v = evt.Timestamp;
                else if (col == "FrameNumber") v = evt.FrameNumber;
                else if (col == "Type") v = evt.Type.ToString();
                else if (f != null && f.TryGetValue(col, out var fv)) v = fv;
                else v = null;
                AppendCsvField(_csvSb, v);
            }
            _csvWriter.WriteLine(_csvSb.ToString());
        }

        private static void AppendCsvField(StringBuilder sb, object v)
        {
            if (v == null) return;
            string s = Convert.ToString(v, CultureInfo.InvariantCulture) ?? "";
            bool needQuote = false;
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == ',' || c == '"' || c == '\n' || c == '\r') { needQuote = true; break; }
            }
            if (!needQuote) { sb.Append(s); return; }
            sb.Append('"');
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == '"') sb.Append("\"\"");
                else sb.Append(c);
            }
            sb.Append('"');
        }

        // ── 内部：JSONL 行 ──
        private static void WriteJsonlRow(StructuredEvent evt)
        {
            _jsonSb.Clear();
            _jsonSb.Append('{');
            AppendJsonKV(_jsonSb, "timestamp", evt.Timestamp, first: true);
            AppendJsonKV(_jsonSb, "frameNumber", evt.FrameNumber);
            AppendJsonKV(_jsonSb, "type", evt.Type.ToString());
            if (evt.Fields != null)
            {
                foreach (var kv in evt.Fields)
                {
                    // JSONL 使用原始 key 小写首字母（保持与 CSV 对应但 camelCase）
                    AppendJsonKV(_jsonSb, ToCamel(kv.Key), kv.Value);
                }
            }
            _jsonSb.Append('}');
            _jsonlWriter.WriteLine(_jsonSb.ToString());
        }

        private static void AppendJsonKV(StringBuilder sb, string key, object value, bool first = false)
        {
            if (!first) sb.Append(',');
            sb.Append('"'); AppendJsonString(sb, key); sb.Append('"').Append(':');
            AppendJsonValue(sb, value);
        }

        private static void AppendJsonValue(StringBuilder sb, object v)
        {
            if (v == null) { sb.Append("null"); return; }
            switch (v)
            {
                case string s: sb.Append('"'); AppendJsonString(sb, s); sb.Append('"'); break;
                case bool b: sb.Append(b ? "true" : "false"); break;
                case float f: sb.Append(float.IsNaN(f) || float.IsInfinity(f) ? "null" : f.ToString("R", CultureInfo.InvariantCulture)); break;
                case double d: sb.Append(double.IsNaN(d) || double.IsInfinity(d) ? "null" : d.ToString("R", CultureInfo.InvariantCulture)); break;
                case byte _:
                case sbyte _:
                case short _:
                case ushort _:
                case int _:
                case uint _:
                case long _:
                case ulong _:
                    sb.Append(Convert.ToString(v, CultureInfo.InvariantCulture));
                    break;
                default:
                    sb.Append('"'); AppendJsonString(sb, Convert.ToString(v, CultureInfo.InvariantCulture) ?? ""); sb.Append('"');
                    break;
            }
        }

        private static void AppendJsonString(StringBuilder sb, string s)
        {
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) sb.AppendFormat(CultureInfo.InvariantCulture, "\\u{0:X4}", (int)c);
                        else sb.Append(c);
                        break;
                }
            }
        }

        private static string ToCamel(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            char c0 = s[0];
            if (c0 >= 'A' && c0 <= 'Z')
                return char.ToLowerInvariant(c0) + (s.Length > 1 ? s.Substring(1) : "");
            return s;
        }

        // ── 内部：文件轮转 ──
        private static void RotateIfNeeded()
        {
            try
            {
                long maxBytes = (long)MaxLogFileSizeMB * 1024L * 1024L;
                if (maxBytes <= 0) return;
                RotateOne(ref _csvWriter, _csvPath, csv: true, maxBytes);
                RotateOne(ref _jsonlWriter, _jsonlPath, csv: false, maxBytes);
            }
            catch { /* 轮转失败不影响主流程 */ }
        }

        private static void RotateOne(ref StreamWriter writer, string path, bool csv, long maxBytes)
        {
            if (writer == null || string.IsNullOrEmpty(path)) return;
            try
            {
                var fi = new FileInfo(path);
                if (!fi.Exists || fi.Length < maxBytes) return;

                writer.Flush();
                writer.Close();

                string stamp = DateTime.Now.ToString("yyyyMMdd_HHmm", CultureInfo.InvariantCulture);
                string dir = Path.GetDirectoryName(path);
                string baseName = Path.GetFileNameWithoutExtension(path);
                string ext = Path.GetExtension(path);
                string backup = Path.Combine(dir ?? _dir ?? "", baseName + "_" + stamp + ext);
                int tries = 0;
                while (File.Exists(backup) && tries < 100)
                {
                    backup = Path.Combine(dir ?? _dir ?? "", baseName + "_" + stamp + "_" + (++tries) + ext);
                }
                File.Move(path, backup);

                writer = new StreamWriter(path, append: false, encoding: Encoding.UTF8) { AutoFlush = false };
                if (csv)
                {
                    writer.WriteLine(string.Join(",", CsvColumns));
                    writer.Flush();
                }
            }
            catch (Exception ex)
            {
                WhySoLaggyPlugin.Log?.LogWarning($"[WHY_LAG] StructuredLogger rotate failed: {ex.Message}");
            }
        }

        // ── 辅助：给调用方构造 Event 用 ──
        public static string NowStamp()
        {
            return DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);
        }
    }
}
