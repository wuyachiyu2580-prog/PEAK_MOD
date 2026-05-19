using System;
using System.Collections;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using ExitGames.Client.Photon;
using HarmonyLib;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

namespace WhySoLaggy
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class WhySoLaggyPlugin : BaseUnityPlugin, IOnEventCallback
    {
        public const string PluginGuid = "com.wuyachiyu.WhySoLaggy";
        public const string PluginName = "WhySoLaggy";
        public const string PluginVersion = "1.0.3";

        // ── 性能监测配置 ──
        public static ConfigEntry<int> SpikeThresholdMs;
        public static ConfigEntry<int> ReportIntervalSeconds;
        public static ConfigEntry<bool> EnablePluginProfiling;
        public static ConfigEntry<bool> EnablePatchProfiling;
        public static ConfigEntry<int> TopMethodCount;

        // ── 炸房监测配置 ──
        public static ConfigEntry<bool> EnableAbuseDetection;
        public static ConfigEntry<float> AbuseCheckInterval;
        public static ConfigEntry<float> AbuseReportInterval;
        public static ConfigEntry<int> InstantiateRateThreshold;
        public static ConfigEntry<int> DestroyRateThreshold;
        public static ConfigEntry<int> RpcRateThreshold;
        public static ConfigEntry<int> ObjectSpikeThreshold;

        // ── RPC 监控配置 ──
        public static ConfigEntry<bool> EnableRpcMonitor;
        public static ConfigEntry<int> RpcMonitorTopCount;
        public static ConfigEntry<int> RpcMonitorWatchPerMethodCapacity;
        public static ConfigEntry<int> RpcMonitorWatchShowPerMethod;
        public static ConfigEntry<string> ExtraWatchMethods;
        public static ConfigEntry<int> PumpBatchSize;
        
        // ── 1.0.3 新增：性能/过滤/日志 ──
        public static ConfigEntry<float> MinReportMs;
        public static ConfigEntry<string> IgnorePluginGuids;
        public static ConfigEntry<string> IgnorePatchMethods;
        public static ConfigEntry<LogVerbosity> VerbosityCfg;
        public static ConfigEntry<int> MaxLogFileSizeMB;
        public static ConfigEntry<bool> EnableMemoryMonitor;
        public static ConfigEntry<bool> ShowDashboard;
        
        // ── 1.0.3 新增：方法追踪 ──
        public static ConfigEntry<string> TraceMethodNames;
        public static ConfigEntry<int> TraceMaxDepth;
        public static ConfigEntry<int> TraceRateLimit;

        // ── 1.0.3 新增：字段探针 FieldProbe ──
        public static ConfigEntry<bool> EnableFieldProbe;
        public static ConfigEntry<string> FieldProbeRulesFile;
        public static ConfigEntry<int> FieldProbeDefaultRate;
        public static ConfigEntry<int> FieldProbeDefaultMaxLen;
        public static ConfigEntry<bool> FieldProbeDefaultStack;
        public static ConfigEntry<int> FieldProbeDefaultStackDepth;
        
        private static string _bepInExDir;
        
        internal static ManualLogSource Log;
        private Harmony _harmony;
        private int _reportFrameCount;
        private float _reportTimer;
        private bool _profilingActive;

        void Awake()
        {
            Log = Logger;
            Log.LogInfo("[WHY_LAG] WhySoLaggy Awake");

            // 配置绑定
            SpikeThresholdMs = Config.Bind("General", "SpikeThresholdMs", 50,
                new ConfigDescription(
                    "Frame time threshold for spike detection (ms). Frames exceeding this are logged.",
                    new AcceptableValueRange<int>(16, 200)));

            ReportIntervalSeconds = Config.Bind("General", "ReportIntervalSeconds", 10,
                new ConfigDescription(
                    "Seconds between periodic performance reports.",
                    new AcceptableValueRange<int>(5, 60)));

            EnablePluginProfiling = Config.Bind("General", "EnablePluginProfiling", false,
                "Profile each BepInEx plugin's Update/LateUpdate/FixedUpdate callbacks. Disabled by default; turn on only when diagnosing.");

            EnablePatchProfiling = Config.Bind("General", "EnablePatchProfiling", false,
                "Profile all Harmony-patched game methods. Disabled by default; turn on only when diagnosing.");

            TopMethodCount = Config.Bind("General", "TopMethodCount", 10,
                new ConfigDescription(
                    "Number of top slow methods to show in reports.",
                    new AcceptableValueRange<int>(3, 30)));

            // ── 炸房监测配置 ──
            EnableAbuseDetection = Config.Bind("AbuseDetection", "EnableAbuseDetection", true,
                "Enable network abuse / room bombing detection.");

            AbuseCheckInterval = Config.Bind("AbuseDetection", "CheckIntervalSeconds", 1f,
                new ConfigDescription(
                    "Seconds between each abuse rate check.",
                    new AcceptableValueRange<float>(0.5f, 5f)));

            AbuseReportInterval = Config.Bind("AbuseDetection", "ReportIntervalSeconds", 30f,
                new ConfigDescription(
                    "Seconds between periodic abuse summary reports.",
                    new AcceptableValueRange<float>(10f, 120f)));

            InstantiateRateThreshold = Config.Bind("AbuseDetection", "InstantiateRateThreshold", 15,
                new ConfigDescription(
                    "Max Instantiate calls per second before triggering alert.",
                    new AcceptableValueRange<int>(5, 100)));

            DestroyRateThreshold = Config.Bind("AbuseDetection", "DestroyRateThreshold", 20,
                new ConfigDescription(
                    "Max Destroy calls per second before triggering alert.",
                    new AcceptableValueRange<int>(5, 100)));

            RpcRateThreshold = Config.Bind("AbuseDetection", "RpcRateThreshold", 50,
                new ConfigDescription(
                    "Max RPC calls per second before triggering alert.",
                    new AcceptableValueRange<int>(10, 200)));

            ObjectSpikeThreshold = Config.Bind("AbuseDetection", "ObjectSpikeThreshold", 30,
                new ConfigDescription(
                    "Object count increase per check interval to trigger spike alert.",
                    new AcceptableValueRange<int>(5, 100)));

            // ── RPC 监控配置 ──
            EnableRpcMonitor = Config.Bind("RpcMonitor", "EnableRpcMonitor", true,
                "Track all network RPC method names and their sources. Low performance overhead (<0.5ms/s for 1000 RPCs/s).");

            RpcMonitorTopCount = Config.Bind("RpcMonitor", "TopMethodCount", 10,
                new ConfigDescription(
                    "Number of top RPC methods to show in periodic reports.",
                    new AcceptableValueRange<int>(3, 30)));

            RpcMonitorWatchPerMethodCapacity = Config.Bind("RpcMonitor", "WatchedRecordPerMethodCapacity", 32,
                new ConfigDescription(
                    "Per-method ring buffer size for watched high-risk RPCs. Each watched method has its own independent buffer so high-frequency methods (e.g. SyncAfflictionsRPC) cannot flood out low-frequency ones (e.g. SendFeedDataRPC).",
                    new AcceptableValueRange<int>(8, 256)));

            RpcMonitorWatchShowPerMethod = Config.Bind("RpcMonitor", "WatchedShowPerMethod", 6,
                new ConfigDescription(
                    "How many most-recent detailed records to print per watched method in each periodic report.",
                    new AcceptableValueRange<int>(1, 50)));
        
            ExtraWatchMethods = Config.Bind("RpcMonitor", "ExtraWatchMethods", "",
                "Comma-separated extra RPC method names merged into the watch list (e.g. 'MyRPC1,MyRPC2').");
        
            PumpBatchSize = Config.Bind("RpcMonitor", "PumpBatchSize", 32,
                new ConfigDescription(
                    "Max RPC queue items consumed per frame on main thread.",
                    new AcceptableValueRange<int>(8, 256)));
        
            // ── 1.0.3 性能/过滤/日志 ──
            MinReportMs = Config.Bind("General", "MinReportMs", 0.1f,
                new ConfigDescription(
                    "Ignore patched methods whose average cost is below this threshold (ms), to reduce profiler overhead.",
                    new AcceptableValueRange<float>(0f, 5f)));
        
            IgnorePluginGuids = Config.Bind("General", "IgnorePluginGuids", "",
                "Comma-separated plugin GUIDs to skip in PluginProfiler.");
        
            IgnorePatchMethods = Config.Bind("General", "IgnorePatchMethods", "",
                "Comma-separated full method names (Type.Method) to skip in PatchProfiler.");
        
            VerbosityCfg = Config.Bind("Logging", "LogVerbosity", LogVerbosity.Minimal,
                "Minimal (default) = abuse alerts + their detail lines + monitor init/error/milestones. Normal = Minimal + periodic reports (RPC top/watched, FPS, patch/plugin profiler, frame spike detail).");
        
            MaxLogFileSizeMB = Config.Bind("Logging", "MaxLogFileSizeMB", 10,
                new ConfigDescription(
                    "Rotate structured CSV/JSONL files when they exceed this size.",
                    new AcceptableValueRange<int>(1, 100)));
        
            EnableMemoryMonitor = Config.Bind("General", "EnableMemoryMonitor", true,
                "Sample GC allocation rate and include AllocRateKBps in FpsReport events.");
        
            ShowDashboard = Config.Bind("UI", "ShowDashboard", false,
                "Show draggable on-screen performance dashboard. Zero cost when disabled.");
        
            // ── 1.0.3 方法追踪 ──
            TraceMethodNames = Config.Bind("MethodTracer", "TraceMethodNames", "",
                "Comma-separated method full names to attach a stack-tracing prefix (e.g. 'Player.Update,ColdComponent.Apply'). Empty disables tracing.");
        
            TraceMaxDepth = Config.Bind("MethodTracer", "TraceMaxDepth", 5,
                new ConfigDescription(
                    "Maximum stack frames recorded per trace sample.",
                    new AcceptableValueRange<int>(3, 20)));
        
            TraceRateLimit = Config.Bind("MethodTracer", "TraceRateLimit", 100,
                new ConfigDescription(
                    "Max trace records per method per second. Excess samples are dropped.",
                    new AcceptableValueRange<int>(10, 1000)));
        
            // ── 1.0.3 字段探针 FieldProbe ──
            EnableFieldProbe = Config.Bind("FieldProbe", "EnableFieldProbe", false,
                "Enable FieldProbe: reflectively snapshot arbitrary fields / parameters / return values at any method via a JSON rules file. Zero overhead when disabled.");

            FieldProbeRulesFile = Config.Bind("FieldProbe", "RulesFile", "WhySoLaggy.fieldprobe.json",
                "Path to FieldProbe JSON rules file. Relative path resolves under BepInEx/config/. See sample file for schema.");

            FieldProbeDefaultRate = Config.Bind("FieldProbe", "DefaultRateLimit", 60,
                new ConfigDescription(
                    "Default max snapshots per rule per second when a rule doesn't specify its own rateLimit.",
                    new AcceptableValueRange<int>(1, 10000)));

            FieldProbeDefaultMaxLen = Config.Bind("FieldProbe", "DefaultMaxValueLen", 128,
                new ConfigDescription(
                    "Default max string length per snapshot value. Longer values are truncated with '...'.",
                    new AcceptableValueRange<int>(16, 4096)));

            FieldProbeDefaultStack = Config.Bind("FieldProbe", "DefaultIncludeStack", false,
                "Default: whether to attach filtered call stack when a rule doesn't specify includeStack.");

            FieldProbeDefaultStackDepth = Config.Bind("FieldProbe", "DefaultStackMaxDepth", 5,
                new ConfigDescription(
                    "Default max stack frames captured per snapshot when includeStack is enabled.",
                    new AcceptableValueRange<int>(1, 30)));
        
            // 初始化日志系统
            _bepInExDir = Path.GetDirectoryName(
                Path.GetDirectoryName(typeof(BaseUnityPlugin).Assembly.Location))
                ?? Paths.BepInExRootPath;
            // 1.0.3 修正：LogVerbosity 枚举已移除 Verbose（1.0.3 精简为 Minimal/Normal 两档）。
            // 若旧 cfg 残留 "Verbose" 值，BepInEx Enum 反序列化可能抛异常；此处兜底回退到 Minimal，
            // 避免读取失败波及后续初始化流程。
            try
            {
                LogFilter.Level = VerbosityCfg.Value;
            }
            catch (Exception verbEx)
            {
                Log.LogWarning($"[WHY_LAG] Failed to read LogVerbosity from cfg (possibly legacy 'Verbose' value): {verbEx.Message}. Falling back to Minimal. Fix: delete com.wuyachiyu.WhySoLaggy.cfg or set LogVerbosity=Minimal/Normal manually.");
                LogFilter.Level = LogVerbosity.Minimal;
            }
            LagLogger.Initialize(_bepInExDir);
            AbuseLogger.Initialize(_bepInExDir);
            StructuredLogger.MaxLogFileSizeMB = MaxLogFileSizeMB.Value;
            StructuredLogger.Initialize(_bepInExDir);
        
            // 传递配置
            FpsTracker.SpikeThresholdMs = SpikeThresholdMs.Value;
            FpsTracker.ReportIntervalSeconds = ReportIntervalSeconds.Value;
            FpsTracker.EnableMemoryMonitor = EnableMemoryMonitor.Value;
            PluginProfiler.Enabled = EnablePluginProfiling.Value;
            PatchProfiler.Enabled = EnablePatchProfiling.Value;
            PatchProfiler.TopMethodCount = TopMethodCount.Value;
            PatchProfiler.MinReportMs = MinReportMs.Value;
            PerformanceDashboard.ShowDashboard = ShowDashboard.Value;
        
            // 装填忽略名单
            FillIgnoreSet(IgnorePluginGuids.Value, PluginProfiler.IgnoreGuids);
            FillIgnoreSet(IgnorePatchMethods.Value, PatchProfiler.IgnoreMethods);
        
            // 创建 Harmony 实例
            _harmony = new Harmony(PluginGuid);
            PatchProfiler.OwnHarmonyId = PluginGuid;
        
            Log.LogInfo("[WHY_LAG] Config bound, loggers initialized");
        }
        
        private static void FillIgnoreSet(string csv, System.Collections.Generic.HashSet<string> set)
        {
            if (string.IsNullOrEmpty(csv) || set == null) return;
            foreach (string raw in csv.Split(','))
            {
                string s = raw?.Trim();
                if (!string.IsNullOrEmpty(s)) set.Add(s);
            }
        }

        /// <summary>
        /// 将 FieldProbe 规则文件路径解析为绝对路径：相对路径统一挂到 BepInEx/config/。
        /// </summary>
        private static string ResolveFieldProbeRulesPath(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "";
            if (Path.IsPathRooted(raw)) return raw;
            string cfgDir = Path.Combine(_bepInExDir ?? Paths.BepInExRootPath, "config");
            return Path.Combine(cfgDir, raw);
        }

        IEnumerator Start()
        {
            Log.LogInfo("[WHY_LAG] Waiting 5s for all plugins to finish loading...");
            LagLogger.Info("[WHY_LAG] Waiting 5s for all plugins to finish loading...");

            yield return new WaitForSeconds(5f);

            // 扫描插件并注入计时
            if (EnablePluginProfiling.Value)
                PluginProfiler.Initialize(_harmony);

            if (EnablePatchProfiling.Value)
                PatchProfiler.Initialize(_harmony);

            // 炸房监测初始化
            if (EnableAbuseDetection.Value)
            {
                NetworkAbuseDetector.InstantiateRateThreshold = InstantiateRateThreshold.Value;
                NetworkAbuseDetector.DestroyRateThreshold = DestroyRateThreshold.Value;
                NetworkAbuseDetector.RpcRateThreshold = RpcRateThreshold.Value;
                NetworkAbuseDetector.ObjectSpikeThreshold = ObjectSpikeThreshold.Value;
                NetworkAbuseDetector.CheckIntervalSeconds = AbuseCheckInterval.Value;
                NetworkAbuseDetector.ReportIntervalSeconds = AbuseReportInterval.Value;
                NetworkAbuseDetector.Initialize(_harmony);
            }

            // RPC 监控初始化（独立开关）
            if (EnableRpcMonitor.Value)
            {
                RpcMonitor.Enabled = true;
                RpcMonitor.TopMethodCount = RpcMonitorTopCount.Value;
                RpcMonitor.WatchedRecordPerMethodCapacity = RpcMonitorWatchPerMethodCapacity.Value;
                RpcMonitor.WatchedShowPerMethod = RpcMonitorWatchShowPerMethod.Value;
                RpcMonitor.PumpBatchSize = PumpBatchSize.Value;
                RpcMonitor.AddExtraWatchMethods(ExtraWatchMethods.Value);
                RpcMonitor.Initialize(_harmony);
            }
            else
            {
                RpcMonitor.Enabled = false;
            }
        
            // Harmony 全量扫描（一次性，PatchProfiler.Initialize 之后）
            try { HarmonyScanner.Scan(_bepInExDir); }
            catch (Exception ex) { Log.LogWarning($"[WHY_LAG] HarmonyScanner.Scan failed: {ex.Message}"); }
        
            // 方法追踪（按需）
            MethodTracer.TraceMethodNames = TraceMethodNames.Value;
            MethodTracer.TraceMaxDepth = TraceMaxDepth.Value;
            MethodTracer.TraceRateLimit = TraceRateLimit.Value;
            try { MethodTracer.Initialize(_harmony); }
            catch (Exception ex) { Log.LogWarning($"[WHY_LAG] MethodTracer.Initialize failed: {ex.Message}"); }

            // 字段探针 FieldProbe（按需）
            FieldProbe.Enabled = EnableFieldProbe.Value;
            FieldProbe.RulesFilePath = ResolveFieldProbeRulesPath(FieldProbeRulesFile.Value);
            FieldProbe.DefaultRateLimit = FieldProbeDefaultRate.Value;
            FieldProbe.DefaultMaxValueLen = FieldProbeDefaultMaxLen.Value;
            FieldProbe.DefaultIncludeStack = FieldProbeDefaultStack.Value;
            FieldProbe.DefaultStackMaxDepth = FieldProbeDefaultStackDepth.Value;
            try { FieldProbe.Initialize(_harmony); }
            catch (Exception ex) { Log.LogWarning($"[WHY_LAG] FieldProbe.Initialize failed: {ex.Message}"); }
        
            _profilingActive = true;
            Log.LogInfo("[WHY_LAG] Profiling active!");
            LagLogger.Info("[WHY_LAG] === Profiling active ===");
        }

        void Update()
        {
            // FPS 追踪始终运行
            FpsTracker.Tick();
        
            // 炸房监测 Tick
            if (EnableAbuseDetection.Value)
                NetworkAbuseDetector.Tick();
        
            // RPC 队列主线程消费（即便 _profilingActive 未置位也需 Pump，避免积压）
            if (RpcMonitor.Enabled)
                RpcMonitor.PumpQueue();
        
            if (!_profilingActive) return;
        
            _reportFrameCount++;
            _reportTimer += Time.unscaledDeltaTime;
        
            // 尖峰检测 → 即时日志
            if (FpsTracker.IsSpikeFrame)
            {
                PluginProfiler.WriteSpikeDetail(FpsTracker.CurrentFrameMs);
                PatchProfiler.WriteSpikeDetail();
            }
        
            // 定期汇总报告
            if (_reportTimer >= ReportIntervalSeconds.Value)
            {
                LagLogger.Write(new string('-', 60));
                PluginProfiler.WriteReport(_reportFrameCount);
                PatchProfiler.WriteReport(_reportFrameCount);
                StructuredLogger.Flush();
                _reportFrameCount = 0;
                _reportTimer = 0f;
            }
        
            // 帧末重置即时计时
            PluginProfiler.ResetFrameTimers();
            PatchProfiler.ResetFrameTimers();
        }
        
        void OnDestroy()
        {
            PhotonNetwork.RemoveCallbackTarget(this);
            LagLogger.Shutdown();
            AbuseLogger.Shutdown();
            StructuredLogger.Shutdown();
        }

        // ═══════════════════════════════════════════════
        //  IOnEventCallback — 远端事件监听
        // ═══════════════════════════════════════════════

        void OnEnable() => PhotonNetwork.AddCallbackTarget(this);
        void OnDisable() => PhotonNetwork.RemoveCallbackTarget(this);

        public void OnEvent(EventData photonEvent)
        {
            if (EnableAbuseDetection.Value)
            {
                NetworkAbuseDetector.OnNetworkEvent(photonEvent.Code, photonEvent.Sender);
                // 1.0.3：解包 EventCode=200 的 CustomData，提取方法名+viewID+真实 sender，
                // 对白名单高危 RPC 写结构化事件；Master 端抓客户端代刷源头
                NetworkAbuseDetector.OnRemoteRpcEvent(photonEvent);
                // 1.0.3 功能 D：PhotonView Ownership 审计（Request/Transfer/Update）
                NetworkAbuseDetector.OnOwnershipEvent(photonEvent);
            }
        }

        // ═══════════════════════════════════════════════
        //  IMGUI — 屏幕通知渲染
        // ═══════════════════════════════════════════════

        void OnGUI()
        {
            if (EnableAbuseDetection.Value)
                AbuseNotificationUI.DrawGUI();
            PerformanceDashboard.DrawGUI();
        }
    }
}
