using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading.Tasks;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace PeakWorkshopCNScanner
{
    [BepInPlugin(Guid, "PeakWorkshopCNScanner", "0.1.0")]
    [BepInDependency(WorkshopGuid, BepInDependency.DependencyFlags.HardDependency)]
    [BepInDependency(ChineseGuid, BepInDependency.DependencyFlags.SoftDependency)]
    public sealed partial class Plugin : BaseUnityPlugin
    {
        public const string Guid = "com.wuyachiyu.peakworkshopcnscanner";
        internal const string WorkshopGuid = "com.peakcode.peakworkshop";
        internal const string ChineseGuid = "com.wuyachiyu.peakworkshopcn";
        internal static Plugin Instance;
        internal bool Scanning, DrawingMarker;
        private ConfigEntry<bool> _enabled, _markers, _showStatus;
        private ConfigEntry<float> _interval;
        private Harmony _harmony;
        private Assembly _workshop;
        private readonly Dictionary<MethodBase, string> _scopes = new Dictionary<MethodBase, string>();
        private readonly HashSet<string> _visited = new HashSet<string>(StringComparer.Ordinal);
        private ScanStore _store;
        private Func<Rect> _visibleRect;
        private Func<bool> _chineseEnabled;
        private bool _ready, _sample, _exportRequested, _quitting;
        private string _failure, _workshopHash, _chineseHash, _chineseState, _scene, _utc, _directory;
        private int _frame, _stackBudget, _markerBudget;
        private float _nextSample, _nextExport;
        private long _draws, _stacks, _observerTicks, _sampleTicks, _exportedRevision = -1;
        private double _startupMs;
        private Task _writer;
        private const long ScanTickBudgetDivisor = 500; // 2 ms soft analysis ceiling; marker draws have their own count cap.

        private void Awake()
        {
            var startup = Stopwatch.StartNew();
            Instance = this;
            _enabled = Config.Bind("General", "Enabled", true, "启用漏译扫描。Ctrl+F12 暂停或恢复；不会修改原文。");
            _markers = Config.Bind("General", "ShowMarkers", true, "显示彩色边框和报告编号。Ctrl+Alt+F12 切换，关闭边框仍可记录。");
            _showStatus = Config.Bind("General", "ShowStatus", true, "显示扫描状态和快捷键。");
            _interval = Config.Bind("Performance", "SampleIntervalSeconds", 0.5f,
                new ConfigDescription("分析新文字的最小间隔；已记录边框使用缓存。", new AcceptableValueRange<float>(0.25f, 5f)));
            int limit = Config.Bind("Performance", "MaximumRecords", 2000,
                new ConfigDescription("本次会话条目上限；达到上限后仍标注已知项。重启开始新会话。", new AcceptableValueRange<int>(100, 10000))).Value;
            string ignore = Config.Bind("Review", "IgnoreExactText", "", "用 | 分隔已人工确认可保留的完整文本；重启生效，区分大小写。").Value;
            _directory = Path.Combine(Paths.BepInExRootPath, "PeakWorkshopCNScanner",
                DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Process.GetCurrentProcess().Id);
            try
            {
                var catalog = SourceCatalog.Load();
                var original = Chainloader.PluginInfos[WorkshopGuid];
                _workshop = original.Instance.GetType().Assembly;
                _workshopHash = Hash(_workshop.Location);
                if (!string.Equals(_workshopHash, catalog.Sha256, StringComparison.Ordinal))
                    throw new InvalidOperationException("Unsupported PeakWorkshop DLL fingerprint; no scan patches installed.");
                _store = new ScanStore(new TextClassifier(catalog.Literals, ignore.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries)), limit, 8192);
                if (Chainloader.PluginInfos.TryGetValue(ChineseGuid, out var chinese))
                {
                    _chineseHash = Hash(chinese.Instance.GetType().Assembly.Location);
                    var getter = AccessTools.PropertyGetter(chinese.Instance.GetType(), "Chinese");
                    if (getter != null && getter.IsStatic && getter.ReturnType == typeof(bool))
                        _chineseEnabled = (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>), getter);
                    _chineseState = "installed; active state unavailable";
                }
                else _chineseState = "not installed (English/German baseline)";
                try
                {
                    var getter = AccessTools.PropertyGetter(typeof(GUI).Assembly.GetType("UnityEngine.GUIClip"), "visibleRect");
                    if (getter != null) _visibleRect = (Func<Rect>)Delegate.CreateDelegate(typeof(Func<Rect>), getter);
                }
                catch (Exception ex) { Logger.LogWarning("Clip filtering unavailable: " + ex.GetType().Name); }
                _harmony = new Harmony(Guid);
                foreach (var target in catalog.Scopes)
                {
                    var method = _workshop.ManifestModule.ResolveMethod(target.Token);
                    if (method.Name != "OnGUI" || method.DeclaringType.FullName != target.Type || method.IsStatic ||
                        method.ContainsGenericParameters || method.GetParameters().Length != 0 || ((MethodInfo)method).ReturnType != typeof(void))
                        throw new InvalidOperationException("Scope signature changed: " + target.Type);
                    _harmony.Patch(method, prefix: new HarmonyMethod(typeof(ScopePatch), nameof(ScopePatch.Prefix)) { priority = Priority.First },
                        finalizer: new HarmonyMethod(typeof(ScopePatch), nameof(ScopePatch.Finalizer)) { priority = Priority.Last });
                    _scopes.Add(method, target.Type + ".OnGUI");
                }
                var textField = AccessTools.Method(typeof(GUI), "DoTextField", new[] { typeof(Rect), typeof(int), typeof(GUIContent),
                    typeof(bool), typeof(int), typeof(GUIStyle), typeof(string), typeof(char) });
                if (textField == null || textField.GetMethodBody() == null) throw new MissingMethodException("Managed GUI.DoTextField target unavailable.");
                _harmony.Patch(textField, prefix: new HarmonyMethod(typeof(TextFieldPatch), nameof(TextFieldPatch.Prefix)) { priority = Priority.First },
                    finalizer: new HarmonyMethod(typeof(TextFieldPatch), nameof(TextFieldPatch.Finalizer)) { priority = Priority.Last });
                var draw = AccessTools.Method(typeof(GUIStyle), "Draw", new[] { typeof(Rect), typeof(GUIContent), typeof(int), typeof(bool), typeof(bool), typeof(bool), typeof(bool) });
                if (draw == null || draw.GetMethodBody() == null) throw new MissingMethodException("Managed GUIStyle.Draw target unavailable.");
                _harmony.Patch(draw, postfix: new HarmonyMethod(typeof(DrawPatch), nameof(DrawPatch.Postfix))
                { priority = Priority.Last, after = new[] { ChineseGuid } });
                _ready = true;
                Scanning = _enabled.Value;
                _exportRequested = true;
                Logger.LogInfo("PeakWorkshopCNScanner ready: " + _scopes.Count + " OnGUI scopes; Ctrl+F12 scan, Ctrl+Shift+F12 export, Ctrl+Alt+F12 markers. Reports: " + _directory);
            }
            catch (Exception ex)
            {
                Fail(ex);
                try { _harmony?.UnpatchSelf(); } catch (Exception cleanup) { Logger.LogWarning("Scanner cleanup: " + cleanup.Message); }
            }
            _startupMs = startup.Elapsed.TotalMilliseconds;
        }

        private void Update()
        {
            if (!_ready || _quitting) return;
            try
            {
                var keyboard = Keyboard.current;
                if (keyboard != null && keyboard.ctrlKey.isPressed && keyboard.f12Key.wasPressedThisFrame)
                {
                    if (keyboard.shiftKey.isPressed) _exportRequested = true;
                    else if (keyboard.altKey.isPressed) _markers.Value = !_markers.Value;
                    else { _enabled.Value = !_enabled.Value; _exportRequested = true; }
                }
                Scanning = _failure == null && _enabled.Value;
                _frame = Time.frameCount;
                _markerBudget = 250;
                _sampleTicks = 0;
                float now = Time.realtimeSinceStartup;
                _sample = Scanning && now >= _nextSample;
                if (_sample)
                {
                    _nextSample = now + _interval.Value;
                    _store.AnalysisBudget = 64;
                    _stackBudget = 4;
                    _utc = DateTime.UtcNow.ToString("o");
                    _scene = SceneManager.GetActiveScene().name;
                    if (_chineseEnabled != null) _chineseState = _chineseEnabled() ? "enabled" : "disabled (English/German baseline)";
                }
                if (_writer != null && _writer.IsCompleted)
                {
                    if (_writer.IsFaulted) { Logger.LogError("Scanner report write failed: " + _writer.Exception.GetBaseException().Message); _exportedRevision = -1; }
                    _writer = null;
                }
                if (_writer == null && (_exportRequested || (now >= _nextExport && _store.Revision != _exportedRevision)))
                {
                    var snapshot = MakeReport();
                    _exportedRevision = _store.Revision;
                    _writer = Task.Run(() => ReportWriter.Write(_directory, snapshot));
                    _nextExport = now + 15f;
                    _exportRequested = false;
                }
            }
            catch (Exception ex) { Fail(ex); }
        }

        private ScanReport MakeReport() => new ScanReport
        {
            ExportedUtc = DateTime.UtcNow.ToString("o"), WorkshopSha256 = _workshopHash, ChinesePatchSha256 = _chineseHash,
            ChinesePatchState = _chineseState, InstalledScopes = new List<string>(_scopes.Values).ToArray(),
            VisitedScopes = new List<string>(_visited).ToArray(), Failure = _failure,
            ClipFilterAvailable = _visibleRect != null, LimitReached = _store.LimitReached, OversizeTextSeen = _store.OversizeTextSeen,
            DrawSubmissions = _draws, Classifications = _store.Classifications, StackCaptures = _stacks,
            ObserverMilliseconds = _observerTicks * 1000.0 / Stopwatch.Frequency, StartupMilliseconds = _startupMs,
            CacheEntries = _store.CacheCount, Records = _store.Snapshot(),
            Coverage = "Observed PeakWorkshop IMGUI draw submissions only; sampled every " + _interval.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) +
                "s; no text fields, native TMP/world labels or unopened pages. " +
                (_visibleRect != null ? "Clipped-out rects excluded; occlusion by other windows is not resolved." : "Clip filter unavailable; offscreen submissions may appear.") +
                " 64 classifications and 4 stacks per sampled frame; 2ms soft analysis budget (markers excluded); 250 markers per frame; cache <=8192 entries/2000000 characters."
        };

        internal void Fail(Exception ex)
        {
            if (_failure != null) return;
            Scanning = false;
            _failure = ex.GetType().Name + ": " + ex.Message;
            _exportRequested = true;
            Logger.LogError("Scanner paused after an error (logged once): " + ex);
        }

        private void OnApplicationQuit() { _quitting = true; Scanning = false; SaveOnExit(); }
        private void SaveOnExit()
        {
            if (_store == null) return;
            try
            {
                // Only exit waits for the bounded background write, never a draw/update frame.
                try { _writer?.GetAwaiter().GetResult(); } catch { }
                ReportWriter.Write(_directory, MakeReport());
            }
            catch (Exception ex) { Logger.LogError("Scanner exit report: " + ex.Message); }
        }
        private void OnDestroy()
        {
            Scanning = false;
            if (!_quitting) SaveOnExit();
            try { _harmony?.UnpatchSelf(); } catch (Exception ex) { Logger.LogWarning("Scanner unpatch: " + ex.Message); }
            if (_statusFont != null) Destroy(_statusFont);
            ScopeState.Current = null;
            Instance = null;
        }
        private static string Hash(string path)
        {
            using (var file = File.OpenRead(path))
            using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(file)).Replace("-", "");
        }
    }
}
