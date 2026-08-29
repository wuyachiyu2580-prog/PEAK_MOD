using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace WhySoLaggy
{
    internal static class StructuredLogger
    {
        public static int MaxLogFileSizeMB = 10;
        public static int MaxRotatedFiles = 30;
        public static int MaxRotatedStorageMB = 350;

        private static readonly string[] CsvColumns =
        {
            "Timestamp", "FrameNumber", "Type",
            "AvgFps", "MinFps", "MaxFps", "AvgFrameMs", "SpikeThresholdMs", "SpikeCount", "ReportDuration",
            "AllocRateKBps", "Ping", "Name", "AvgMs", "TotalMs", "CallCount", "Owner",
            "TopPluginName", "TopPluginMs", "AlertType", "Rate", "Threshold", "Delta", "CurrentCount",
            "TopActor", "TopActorName", "TopOwners",
            "TotalInstantiates", "TotalDestroys", "TotalRpcs",
            "LocalInstantiates", "LocalDestroys", "LocalRpcs",
            "RemoteInstantiates", "RemoteDestroys", "RemoteRpcs", "AlertCount",
            "RoomName", "PlayerCount", "MaxPlayers", "ZombieCount",
            "RpcMethod", "SenderActor", "SenderName", "TargetViewID", "TargetName", "TargetPath",
            "PayloadBytes", "ArgsSummary", "SpecificDesc",
            "EventCode", "OwnershipAction", "PreviousOwner", "NewOwner", "PairCount",
            "QueueCapacity", "QueueDepth", "QueuePeak", "QueueDropped",
            "TargetMethod", "PatchType", "OwnerHarmonyId", "Priority", "TraceStack", "TraceCaller", "Snapshot",
            "PrefabName", "IsMasterClient", "Position", "LocalActor",
            "SuspectedRequesterActor", "SuspectedRequesterName", "SuspectedRequesterRpc", "SuspectedAgeMs",
            "QueueSequence", "EnqueueTimestamp", "EnqueueFrameNumber", "QueueDelayMs",
            "RpcQueueDepth", "RpcQueuePeak", "RpcQueueDropped", "RpcProcessedCount", "RpcPumpMs",
            "StructuredEventsWritten", "StructuredFlushMs",
            "SuppressedCount", "PeakRate",
        };

        private static readonly object _lock = new object();
        private static readonly StringBuilder _csvRow = new StringBuilder(512);
        private static readonly StringBuilder _jsonRow = new StringBuilder(512);
        private static readonly StringBuilder _csvBuffer = new StringBuilder(8192);
        private static readonly StringBuilder _jsonBuffer = new StringBuilder(8192);
        private static readonly long FlushIntervalTicks = TimeSpan.TicksPerSecond;
        private static StreamWriter _csvWriter;
        private static StreamWriter _jsonlWriter;
        private static string _dir;
        private static string _csvPath;
        private static string _jsonlPath;
        private static long _lastFlushTicks;
        private static long _eventsSinceMetrics;
        private static double _lastFlushMs;
        private static bool _inited;

        internal static string ExpectedCsvHeader => string.Join(",", CsvColumns);

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
                    RotateCsvForSchemaMismatch();
                    CleanupRotatedFiles(_csvPath);
                    CleanupRotatedFiles(_jsonlPath);
                    bool needHeader = !File.Exists(_csvPath) || new FileInfo(_csvPath).Length == 0;
                    _csvWriter = NewWriter(_csvPath, true);
                    _jsonlWriter = NewWriter(_jsonlPath, true);
                    if (needHeader)
                    {
                        _csvWriter.WriteLine(ExpectedCsvHeader);
                        _csvWriter.Flush();
                    }
                    _csvBuffer.Clear();
                    _jsonBuffer.Clear();
                    _eventsSinceMetrics = 0;
                    _lastFlushMs = 0;
                    _lastFlushTicks = DateTime.UtcNow.Ticks;
                    _inited = true;
                }
                catch (Exception ex)
                {
                    WhySoLaggyPlugin.Log?.LogError($"[WHY_LAG] StructuredLogger init failed: {ex.Message}");
                    CloseWriters();
                    _inited = false;
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
                    AppendCsvRow(evt);
                    AppendJsonlRow(evt);
                    _eventsSinceMetrics++;
                }
                catch (Exception ex)
                {
                    WhySoLaggyPlugin.Log?.LogWarning($"[WHY_LAG] StructuredLogger buffer failed: {ex.Message}");
                }
            }
        }

        public static void Tick()
        {
            if (!_inited) return;
            lock (_lock)
            {
                if (DateTime.UtcNow.Ticks - _lastFlushTicks >= FlushIntervalTicks)
                    FlushBuffersLocked();
            }
        }

        public static void Flush()
        {
            if (!_inited) return;
            lock (_lock) FlushBuffersLocked();
        }

        public static void Shutdown()
        {
            lock (_lock)
            {
                if (_inited) FlushBuffersLocked();
                CloseWriters();
                _csvBuffer.Clear();
                _jsonBuffer.Clear();
                _inited = false;
                _lastFlushTicks = 0;
                _eventsSinceMetrics = 0;
                _lastFlushMs = 0;
            }
        }

        private static void FlushBuffersLocked()
        {
            long started = DateTime.UtcNow.Ticks;
            try
            {
                RotateIfNeeded();
                if (_csvBuffer.Length > 0)
                {
                    _csvWriter?.Write(_csvBuffer.ToString());
                    _csvBuffer.Clear();
                }
                if (_jsonBuffer.Length > 0)
                {
                    _jsonlWriter?.Write(_jsonBuffer.ToString());
                    _jsonBuffer.Clear();
                }
                _csvWriter?.Flush();
                _jsonlWriter?.Flush();
            }
            catch (Exception ex)
            {
                WhySoLaggyPlugin.Log?.LogWarning($"[WHY_LAG] StructuredLogger flush failed: {ex.Message}");
            }
            finally
            {
                _lastFlushTicks = DateTime.UtcNow.Ticks;
                _lastFlushMs = Math.Max(0d, (_lastFlushTicks - started) * 1000d / TimeSpan.TicksPerSecond);
            }
        }

        private static void AppendCsvRow(StructuredEvent evt)
        {
            _csvRow.Clear();
            for (int i = 0; i < CsvColumns.Length; i++)
            {
                if (i > 0) _csvRow.Append(',');
                string column = CsvColumns[i];
                object value;
                if (column == "Timestamp") value = evt.Timestamp;
                else if (column == "FrameNumber") value = evt.FrameNumber;
                else if (column == "Type") value = evt.Type.ToString();
                else if (evt.Fields != null && evt.Fields.TryGetValue(column, out var fieldValue)) value = fieldValue;
                else value = null;
                AppendCsvField(_csvRow, value);
            }
            _csvBuffer.AppendLine(_csvRow.ToString());
        }

        private static void AppendCsvField(StringBuilder sb, object value)
        {
            if (value == null) return;
            string text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
            bool quote = text.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0;
            if (!quote) { sb.Append(text); return; }
            sb.Append('"');
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == '"') sb.Append("\"\"");
                else sb.Append(text[i]);
            }
            sb.Append('"');
        }

        private static void AppendJsonlRow(StructuredEvent evt)
        {
            _jsonRow.Clear();
            _jsonRow.Append('{');
            AppendJsonKV(_jsonRow, "timestamp", evt.Timestamp, true);
            AppendJsonKV(_jsonRow, "frameNumber", evt.FrameNumber);
            AppendJsonKV(_jsonRow, "type", evt.Type.ToString());
            if (evt.Fields != null)
                foreach (var pair in evt.Fields) AppendJsonKV(_jsonRow, ToCamel(pair.Key), pair.Value);
            _jsonRow.Append('}');
            _jsonBuffer.AppendLine(_jsonRow.ToString());
        }

        private static void AppendJsonKV(StringBuilder sb, string key, object value, bool first = false)
        {
            if (!first) sb.Append(',');
            sb.Append('"'); AppendJsonString(sb, key); sb.Append('"').Append(':');
            AppendJsonValue(sb, value);
        }

        private static void AppendJsonValue(StringBuilder sb, object value)
        {
            if (value == null) { sb.Append("null"); return; }
            switch (value)
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
                    sb.Append(Convert.ToString(value, CultureInfo.InvariantCulture));
                    break;
                default:
                    sb.Append('"'); AppendJsonString(sb, Convert.ToString(value, CultureInfo.InvariantCulture) ?? ""); sb.Append('"');
                    break;
            }
        }

        private static void AppendJsonString(StringBuilder sb, string value)
        {
            for (int i = 0; i < value.Length; i++)
            {
                switch (value[i])
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (value[i] < 0x20) sb.AppendFormat(CultureInfo.InvariantCulture, "\\u{0:X4}", (int)value[i]);
                        else sb.Append(value[i]);
                        break;
                }
            }
        }

        private static string ToCamel(string value)
        {
            if (string.IsNullOrEmpty(value)) return value;
            return char.IsUpper(value[0])
                ? char.ToLowerInvariant(value[0]) + (value.Length > 1 ? value.Substring(1) : "")
                : value;
        }

        private static void RotateCsvForSchemaMismatch()
        {
            if (!File.Exists(_csvPath) || new FileInfo(_csvPath).Length == 0) return;
            string header;
            using (var reader = new StreamReader(_csvPath, Encoding.UTF8, true)) header = reader.ReadLine();
            if ((header ?? "").TrimStart('\uFEFF') == ExpectedCsvHeader) return;
            File.Move(_csvPath, BuildBackupPath(_csvPath, "schema"));
        }

        private static void RotateIfNeeded()
        {
            long maxBytes = (long)MaxLogFileSizeMB * 1024L * 1024L;
            if (maxBytes <= 0) return;
            RotateOne(ref _csvWriter, _csvPath, true, maxBytes, Encoding.UTF8.GetByteCount(_csvBuffer.ToString()));
            RotateOne(ref _jsonlWriter, _jsonlPath, false, maxBytes, Encoding.UTF8.GetByteCount(_jsonBuffer.ToString()));
        }

        private static void RotateOne(ref StreamWriter writer, string path, bool csv, long maxBytes, int pendingBytes)
        {
            if (writer == null || string.IsNullOrEmpty(path)) return;
            var info = new FileInfo(path);
            if (!info.Exists || info.Length + pendingBytes < maxBytes) return;
            writer.Flush();
            writer.Close();
            File.Move(path, BuildBackupPath(path, "size"));
            writer = NewWriter(path, false);
            if (csv)
            {
                writer.WriteLine(ExpectedCsvHeader);
                writer.Flush();
            }
            CleanupRotatedFiles(path);
        }

        private static void CleanupRotatedFiles(string activePath)
        {
            if (string.IsNullOrEmpty(activePath)) return;
            try
            {
                string directory = Path.GetDirectoryName(activePath) ?? _dir ?? "";
                string baseName = Path.GetFileNameWithoutExtension(activePath);
                string extension = Path.GetExtension(activePath);
                var files = new System.Collections.Generic.List<FileInfo>();
                foreach (string path in Directory.GetFiles(directory, baseName + "_*" + extension))
                {
                    string name = Path.GetFileNameWithoutExtension(path);
                    if (name.IndexOf(baseName + "_size_", StringComparison.Ordinal) != 0
                        && name.IndexOf(baseName + "_schema_", StringComparison.Ordinal) != 0)
                        continue;
                    files.Add(new FileInfo(path));
                }
                files.Sort((a, b) => a.LastWriteTimeUtc.CompareTo(b.LastWriteTimeUtc));
                long maxBytes = Math.Max(0L, (long)MaxRotatedStorageMB * 1024L * 1024L);
                long totalBytes = 0;
                foreach (var file in files) totalBytes += file.Length;
                int keepFrom = 0;
                while (files.Count - keepFrom > Math.Max(0, MaxRotatedFiles) || (maxBytes > 0 && totalBytes > maxBytes))
                {
                    if (keepFrom >= files.Count) break;
                    var old = files[keepFrom++];
                    totalBytes -= old.Length;
                    try { old.Delete(); } catch { }
                }
            }
            catch (Exception ex)
            {
                WhySoLaggyPlugin.Log?.LogWarning($"[WHY_LAG] StructuredLogger retention cleanup failed: {ex.Message}");
            }
        }

        private static string BuildBackupPath(string path, string reason)
        {
            string directory = Path.GetDirectoryName(path) ?? _dir ?? "";
            string name = Path.GetFileNameWithoutExtension(path);
            string extension = Path.GetExtension(path);
            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff", CultureInfo.InvariantCulture);
            string candidate = Path.Combine(directory, name + "_" + reason + "_" + stamp + extension);
            int suffix = 0;
            while (File.Exists(candidate))
                candidate = Path.Combine(directory, name + "_" + reason + "_" + stamp + "_" + (++suffix) + extension);
            return candidate;
        }

        private static StreamWriter NewWriter(string path, bool append)
        {
            return new StreamWriter(path, append, new UTF8Encoding(true)) { AutoFlush = false };
        }

        private static void CloseWriters()
        {
            try { _csvWriter?.Close(); } catch { }
            try { _jsonlWriter?.Close(); } catch { }
            _csvWriter = null;
            _jsonlWriter = null;
        }

        public static string NowStamp()
        {
            return DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);
        }

        internal static string StampFromUtcTicks(long ticks)
        {
            if (ticks <= 0) return "";
            return new DateTime(ticks, DateTimeKind.Utc).ToLocalTime()
                .ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);
        }

        internal static StructuredRuntimeMetrics TakeRuntimeMetrics()
        {
            lock (_lock)
            {
                var result = new StructuredRuntimeMetrics(_eventsSinceMetrics, _lastFlushMs);
                _eventsSinceMetrics = 0;
                return result;
            }
        }
    }

    internal struct StructuredRuntimeMetrics
    {
        public StructuredRuntimeMetrics(long eventsWritten, double lastFlushMs)
        {
            EventsWritten = eventsWritten;
            LastFlushMs = lastFlushMs;
        }

        public long EventsWritten { get; }
        public double LastFlushMs { get; }
    }
}
