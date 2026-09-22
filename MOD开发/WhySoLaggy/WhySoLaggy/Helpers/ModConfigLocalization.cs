using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace WhySoLaggy
{
    /// <summary>
    /// Localizes WhySoLaggy entries in PEAKLib.ModConfig without changing the
    /// persisted section/key names or values in the BepInEx config file.
    /// </summary>
    internal static class ModConfigLocalization
    {
        private const string ModConfigGuid = "com.github.PEAKModding.PEAKLib.ModConfig";
        private const string ConfigFileName = "com.wuyachiyu.WhySoLaggy.cfg";

        private static readonly FieldInfo DescriptionBackingField = typeof(ConfigDescription).GetField(
            "<Description>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly LocalizedConfigEntry[] ConfigEntries =
        {
            Entry("General", "SpikeThresholdMs", "Spike Threshold", "卡顿帧阈值", "Frame time threshold for spike detection (ms). Frames exceeding this are logged.", "卡顿检测的帧时间阈值，单位毫秒。超过阈值的帧会被记录。"),
            Entry("General", "ReportIntervalSeconds", "Report Interval", "报告间隔", "Seconds between periodic performance reports.", "性能周期报告之间的间隔秒数。"),
            Entry("General", "EnablePluginProfiling", "Enable Plugin Profiling", "启用插件性能分析", "Profile each BepInEx plugin's Update/LateUpdate/FixedUpdate callbacks. Disabled by default; turn on only when diagnosing.", "分析每个 BepInEx 插件的 Update、LateUpdate 和 FixedUpdate 回调。默认关闭，仅在诊断问题时启用。"),
            Entry("General", "EnablePatchProfiling", "Enable Patch Profiling", "启用补丁性能分析", "Profile all Harmony-patched game methods. Disabled by default; turn on only when diagnosing.", "分析所有被 Harmony 补丁包裹的游戏方法。默认关闭，仅在诊断问题时启用。"),
            Entry("General", "TopMethodCount", "Top Method Count", "显示方法数量", "Number of top slow methods to show in reports.", "报告中显示的高耗时方法数量。"),
            Entry("General", "MinReportMs", "Minimum Report Time", "最低报告耗时", "Ignore patched methods whose average cost is below this threshold (ms), to reduce profiler overhead.", "忽略平均耗时低于此阈值的补丁方法，单位毫秒，以降低性能分析开销。"),
            Entry("General", "IgnorePluginGuids", "Ignored Plugin GUIDs", "忽略的插件 GUID", "Comma-separated plugin GUIDs to skip in PluginProfiler.", "PluginProfiler 要跳过的插件 GUID，使用逗号分隔。"),
            Entry("General", "IgnorePatchMethods", "Ignored Patch Methods", "忽略的补丁方法", "Comma-separated full method names (Type.Method) to skip in PatchProfiler.", "PatchProfiler 要跳过的完整方法名（Type.Method），使用逗号分隔。"),
            Entry("General", "EnableMemoryMonitor", "Enable Memory Monitor", "启用内存监控", "Sample GC allocation rate and include AllocRateKBps in FpsReport events.", "采样 GC 分配速率，并在 FpsReport 事件中记录 AllocRateKBps。"),

            Entry("AbuseDetection", "EnableAbuseDetection", "Enable Abuse Detection", "启用滥用检测", "Enable network abuse / room bombing detection.", "启用网络滥用和炸房检测。"),
            Entry("AbuseDetection", "CheckIntervalSeconds", "Check Interval", "检测间隔", "Seconds between each abuse rate check.", "每次网络滥用速率检查之间的间隔秒数。"),
            Entry("AbuseDetection", "ReportIntervalSeconds", "Report Interval", "报告间隔", "Seconds between periodic abuse summary reports.", "网络滥用汇总报告之间的间隔秒数。"),
            Entry("AbuseDetection", "InstantiateRateThreshold", "Instantiate Rate Threshold", "Instantiate 速率阈值", "Max Instantiate calls per second before triggering alert.", "触发告警前允许的每秒最大 Instantiate 调用数。"),
            Entry("AbuseDetection", "DestroyRateThreshold", "Destroy Rate Threshold", "Destroy 速率阈值", "Max Destroy calls per second before triggering alert.", "触发告警前允许的每秒最大 Destroy 调用数。"),
            Entry("AbuseDetection", "RpcRateThreshold", "RPC Rate Threshold", "RPC 速率阈值", "Max RPC calls per second before triggering alert.", "触发告警前允许的每秒最大 RPC 调用数。"),
            Entry("AbuseDetection", "ObjectSpikeThreshold", "Object Spike Threshold", "对象增长阈值", "Object count increase per check interval to trigger spike alert.", "每个检测周期内触发对象增长告警所需的数量增幅。"),
            Entry("AbuseDetection", "ActorMethodRateThreshold", "Actor Method Rate Threshold", "Actor 方法速率阈值", "Max calls for one remote Actor and RPC method per check window before alerting.", "单个远端 Actor 对同一 RPC 方法在一个检测窗口内触发告警前允许的最大调用数。"),
            Entry("AbuseDetection", "OwnershipGrabRateThreshold", "Ownership Grab Rate Threshold", "所有权抢夺阈值", "Max remote ownership transfers to one new owner per check window before alerting.", "单个新所有者在一个检测窗口内触发告警前允许的最大远端所有权转移数。"),
            Entry("AbuseDetection", "OwnershipRequestRateThreshold", "Ownership Request Rate Threshold", "所有权请求阈值", "Max ownership requests from one remote actor per check window before alerting.", "单个远端 Actor 在一个检测窗口内触发告警前允许的最大所有权请求数。"),
            Entry("AbuseDetection", "AlertCooldownSeconds", "Alert Cooldown", "告警冷却时间", "Seconds before the same abuse alert key can be emitted again.", "同一个滥用告警键再次输出前需要等待的秒数。"),

            Entry("RpcMonitor", "EnableRpcMonitor", "Enable RPC Monitor", "启用 RPC 监控", "Track all network RPC method names and their sources. Low performance overhead (<0.5ms/s for 1000 RPCs/s).", "记录所有网络 RPC 方法名及其来源。性能开销较低（1000 RPC/秒时低于 0.5 毫秒/秒）。"),
            Entry("RpcMonitor", "TopMethodCount", "Top RPC Method Count", "显示 RPC 方法数量", "Number of top RPC methods to show in periodic reports.", "周期报告中显示的高频 RPC 方法数量。"),
            Entry("RpcMonitor", "WatchedRecordPerMethodCapacity", "Watched Records Per Method", "每个方法的记录容量", "Per-method ring buffer size for watched high-risk RPCs. Each watched method has its own independent buffer so high-frequency methods cannot flood out low-frequency ones.", "高风险监控 RPC 的单方法环形缓冲区大小。每个方法拥有独立缓冲区，避免高频方法挤掉低频方法的记录。"),
            Entry("RpcMonitor", "WatchedShowPerMethod", "Watched Records To Show", "每个方法显示记录数", "How many most-recent detailed records to print per watched method in each periodic report.", "每次周期报告中每个监控方法显示的最新详细记录数量。"),
            Entry("RpcMonitor", "ExtraWatchMethods", "Extra Watch Methods", "额外监控方法", "Comma-separated extra RPC method names merged into the watch list (e.g. 'MyRPC1,MyRPC2').", "要加入监控列表的额外 RPC 方法名，使用逗号分隔（例如 'MyRPC1,MyRPC2'）。"),
            Entry("RpcMonitor", "PumpBatchSize", "RPC Pump Batch Size", "RPC 每帧处理数量", "Max RPC queue items consumed per frame on main thread.", "主线程每帧最多处理的 RPC 队列项目数。"),
            Entry("RpcMonitor", "QueueCapacity", "RPC Queue Capacity", "RPC 队列容量", "Maximum queued RPC records. When full, the oldest records are discarded so recent activity is retained.", "RPC 记录队列的最大容量。队列满时丢弃最旧记录，以保留最新活动。"),

            Entry("Logging", "LogVerbosity", "Log Verbosity", "日志详细度", "Minimal (default) = abuse alerts + their detail lines + monitor init/error/milestones. Normal = Minimal + periodic reports (RPC top/watched, FPS, patch/plugin profiler, frame spike detail).", "Minimal（默认）= 滥用告警、详情、监控初始化/错误/关键节点；Normal = Minimal 加周期报告（RPC、FPS、补丁/插件分析和帧尖峰详情）。"),
            Entry("Logging", "MaxLogFileSizeMB", "Maximum Log File Size", "日志文件最大大小", "Rotate structured CSV/JSONL files when they exceed this size.", "结构化 CSV/JSONL 文件超过此大小时进行轮转，单位 MB。"),
            Entry("Logging", "MaxRotatedFiles", "Maximum Rotated Files", "最大轮转文件数", "Maximum number of rotated CSV or JSONL files retained per format.", "每种格式最多保留的 CSV 或 JSONL 轮转文件数量。"),
            Entry("Logging", "MaxRotatedStorageMB", "Maximum Rotated Storage", "最大轮转存储空间", "Maximum total size of rotated CSV or JSONL files retained per format.", "每种格式最多保留的 CSV 或 JSONL 轮转文件总大小，单位 MB。"),

            Entry("MethodTracer", "TraceMethodNames", "Trace Method Names", "追踪方法名", "Comma-separated method full names to capture Environment.StackTrace (expensive; use only while diagnosing, e.g. 'Player.Update,ColdComponent.Apply'). Empty disables tracing.", "要捕获 Environment.StackTrace 的完整方法名，使用逗号分隔（开销较高，仅在诊断时启用，例如 'Player.Update,ColdComponent.Apply'）。留空则关闭追踪。"),
            Entry("MethodTracer", "TraceMaxDepth", "Trace Maximum Depth", "追踪最大深度", "Maximum stack frames recorded per trace sample.", "每条追踪样本记录的最大堆栈帧数。"),
            Entry("MethodTracer", "TraceRateLimit", "Trace Rate Limit", "追踪速率限制", "Max trace records per method per second. Excess samples are dropped.", "每个方法每秒最多记录的追踪样本数。超出的样本会被丢弃。"),

            Entry("FieldProbe", "EnableFieldProbe", "Enable FieldProbe", "启用字段探针", "Enable FieldProbe: reflectively snapshot arbitrary fields / parameters / return values at any method via a JSON rules file. Zero overhead when disabled.", "启用字段探针：通过 JSON 规则文件反射读取任意方法的字段、参数和返回值。关闭时无开销。"),
            Entry("FieldProbe", "RulesFile", "Rules File", "规则文件", "Path to FieldProbe JSON rules file. Relative path resolves under BepInEx/config/. See sample file for schema.", "FieldProbe JSON 规则文件路径。相对路径以 BepInEx/config/ 为基准。规则格式请参阅示例文件。"),
            Entry("FieldProbe", "DefaultRateLimit", "Default Rate Limit", "默认速率限制", "Default max snapshots per rule per second when a rule doesn't specify its own rateLimit.", "规则未指定 rateLimit 时，每条规则每秒允许的默认最大快照数。"),
            Entry("FieldProbe", "DefaultMaxValueLen", "Default Maximum Value Length", "默认最大值长度", "Default max string length per snapshot value. Longer values are truncated with '...'.", "每个快照值的默认最大字符串长度。更长的值会截断为 '...'。"),
            Entry("FieldProbe", "DefaultIncludeStack", "Default Include Stack", "默认包含堆栈", "Default: whether to attach filtered call stack when a rule doesn't specify includeStack.", "规则未指定 includeStack 时，是否默认附加过滤后的调用堆栈。"),
            Entry("FieldProbe", "DefaultStackMaxDepth", "Default Stack Maximum Depth", "默认堆栈最大深度", "Default max stack frames captured per snapshot when includeStack is enabled.", "启用 includeStack 时，每个快照默认捕获的最大堆栈帧数。"),

            Entry("UI", "ShowDashboard", "Show Dashboard", "显示性能面板", "Show draggable on-screen performance dashboard. Zero cost when disabled.", "显示可拖动的屏幕性能面板。关闭时无开销。"),
        };

        private static readonly TokenText[] Tokens = BuildTokens();
        private static FieldInfo _currentLanguageField;
        private static readonly ModConfigUiAdapter.ActionFieldSubscription LanguageSubscription = new ModConfigUiAdapter.ActionFieldSubscription();
        internal static IEnumerable<LocalizedConfigEntry> Entries => ConfigEntries;

        public static void PatchDisplayNames(Harmony harmony)
        {
            ModConfigUiAdapter.Install(harmony, ConfigFileName, WhySoLaggyPlugin.PluginName,
                entry => GetLocalizedConfigText(entry.Definition.Section, entry.Definition.Key, IsChinese),
                text => GetLocalizedToken(FindCanonicalToken(text), IsChinese),
                message => WhySoLaggyPlugin.Log?.LogWarning(message));
        }
        public static void RefreshVisibleUi() => ModConfigUiAdapter.RefreshVisibleUi();
        public static void SubscribeLanguageChanged(Action handler)
        {
            try
            {
                Type type = AccessTools.TypeByName("LocalizedText");
                _currentLanguageField = type?.GetField("CURRENT_LANGUAGE", BindingFlags.Static | BindingFlags.Public);
                LanguageSubscription.Subscribe(type?.GetField("OnLangugageChanged", BindingFlags.Static | BindingFlags.Public), handler);
            }
            catch (Exception ex) { WhySoLaggyPlugin.Log?.LogWarning("Language subscription skipped: " + ex.Message); }
        }
        public static void Shutdown()
        {
            LanguageSubscription.Dispose();
            ModConfigUiAdapter.Shutdown();
            _currentLanguageField = null;
        }
        private static bool IsOwnEntry(ConfigEntryBase entry) => ModConfigUiAdapter.Owns(entry, ConfigFileName);
        internal static bool IsOwnCategory(string category) =>
            ModConfigUiAdapter.SameMod(category, WhySoLaggyPlugin.PluginName) || category == WhySoLaggyPlugin.PluginGuid;
        private static bool IsChinese
        {
            get
            {
                if (_currentLanguageField == null)
                    _currentLanguageField = AccessTools.TypeByName("LocalizedText")?.GetField("CURRENT_LANGUAGE", BindingFlags.Static | BindingFlags.Public);
                string language = _currentLanguageField?.GetValue(null)?.ToString();
                return language == "SimplifiedChinese" || language == "TraditionalChinese";
            }
        }

        public static void ApplyLocalizedDescriptions(IEnumerable<ConfigEntryBase> entries)
        {
            if (entries == null)
            {
                return;
            }

            bool chinese = IsChinese;
            foreach (ConfigEntryBase entry in entries.Where(value => value != null))
            {
                if (!IsOwnEntry(entry))
                {
                    continue;
                }

                string description = GetLocalizedDescription(entry.Definition.Section, entry.Definition.Key, chinese);
                SetDescription(entry, description);
            }
        }

        internal static string GetLocalizedConfigText(string section, string key, bool chinese)
        {
            LocalizedConfigEntry entry = FindEntry(section, key);
            return entry == null ? null : (chinese ? entry.ChineseName : entry.EnglishName);
        }

        internal static string GetLocalizedDescription(string section, string key, bool chinese)
        {
            LocalizedConfigEntry entry = FindEntry(section, key);
            return entry == null ? null : (chinese ? entry.ChineseDescription : entry.EnglishDescription);
        }

        internal static string GetLocalizedCategoryText(string category, bool chinese)
        {
            string canonical = FindCanonicalToken(category);
            return string.IsNullOrEmpty(canonical) ? null : GetLocalizedToken(canonical, chinese);
        }

        private static string FindCanonicalToken(string text)
        {
            string normalized = Normalize(text);
            if (string.IsNullOrEmpty(normalized))
            {
                return null;
            }

            foreach (TokenText token in Tokens)
            {
                if (string.Equals(normalized, Normalize(token.English), StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(normalized, Normalize(token.Chinese), StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(normalized, Normalize(token.Key), StringComparison.OrdinalIgnoreCase))
                {
                    return token.Key;
                }
            }

            return null;
        }

        private static string GetLocalizedToken(string key, bool chinese)
        {
            foreach (TokenText token in Tokens)
            {
                if (string.Equals(token.Key, key, StringComparison.Ordinal))
                {
                    return chinese ? token.Chinese : token.English;
                }
            }

            return null;
        }

        private static TokenText[] BuildTokens()
        {
            List<TokenText> tokens = new List<TokenText>
            {
                Token("WhySoLaggy", "WhySoLaggy", "为什么这么卡"),
                Token("General", "General", "常规"),
                Token("AbuseDetection", "Abuse Detection", "滥用检测"),
                Token("RpcMonitor", "RPC Monitor", "RPC 监控"),
                Token("Logging", "Logging", "日志"),
                Token("MethodTracer", "Method Tracer", "方法追踪"),
                Token("FieldProbe", "FieldProbe", "字段探针"),
                Token("UI", "UI", "界面"),
                Token("Minimal", "Minimal", "最简"),
                Token("Normal", "Normal", "普通"),
            };

            foreach (LocalizedConfigEntry entry in ConfigEntries)
            {
                if (!tokens.Any(token => token.Key == entry.Key))
                {
                    tokens.Add(Token(entry.Key, entry.EnglishName, entry.ChineseName));
                }
            }

            return tokens.ToArray();
        }

        private static string Normalize(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Replace(" ", string.Empty).Trim();
        }

        private static LocalizedConfigEntry FindEntry(string section, string key)
        {
            return ConfigEntries.FirstOrDefault(entry =>
                string.Equals(entry.Section, section, StringComparison.Ordinal) &&
                string.Equals(entry.Key, key, StringComparison.Ordinal));
        }

        private static LocalizedConfigEntry Entry(string section, string key, string englishName, string chineseName,
            string englishDescription, string chineseDescription)
        {
            return new LocalizedConfigEntry(section, key, englishName, chineseName, englishDescription, chineseDescription);
        }

        private static TokenText Token(string key, string english, string chinese)
        {
            return new TokenText(key, english, chinese);
        }

        private static void SetDescription(ConfigEntryBase entry, string text)
        {
            if (entry == null || entry.Description == null || DescriptionBackingField == null || string.IsNullOrEmpty(text))
            {
                return;
            }

            try
            {
                DescriptionBackingField.SetValue(entry.Description, text);
            }
            catch (Exception ex)
            {
                WhySoLaggyPlugin.Log?.LogWarning("[WHY_LAG] ModConfig description localization skipped: " + ex.Message);
            }
        }

        internal sealed class LocalizedConfigEntry
        {
            internal LocalizedConfigEntry(string section, string key, string englishName, string chineseName,
                string englishDescription, string chineseDescription)
            {
                Section = section;
                Key = key;
                EnglishName = englishName;
                ChineseName = chineseName;
                EnglishDescription = englishDescription;
                ChineseDescription = chineseDescription;
            }

            internal string Section { get; }
            internal string Key { get; }
            internal string EnglishName { get; }
            internal string ChineseName { get; }
            internal string EnglishDescription { get; }
            internal string ChineseDescription { get; }
        }

        private sealed class TokenText
        {
            internal TokenText(string key, string english, string chinese)
            {
                Key = key;
                English = english;
                Chinese = chinese;
            }

            internal string Key { get; }
            internal string English { get; }
            internal string Chinese { get; }
        }

    }
}
