using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace WhySoLaggy.Tests
{
    [TestClass]
    [DoNotParallelize]
    public class CoreBehaviorTests
    {
        [TestMethod]
        public void OwnershipParser_UsesPun23aCodesAndPayloadShapes()
        {
            Assert.IsTrue(OwnershipEventParser.TryParse(209, new[] { 1001, 7 }, 12, out var request));
            Assert.AreEqual(OwnershipEventKind.Request, request.Kind);
            Assert.AreEqual(12, request.SenderActor);
            Assert.AreEqual(7, request.RelatedOwner);

            Assert.IsTrue(OwnershipEventParser.TryParse(210, new[] { 1002, 9 }, 7, out var transfer));
            Assert.AreEqual(OwnershipEventKind.Transfer, transfer.Kind);
            Assert.AreEqual(9, transfer.RelatedOwner);

            Assert.IsTrue(OwnershipEventParser.TryParse(212, new[] { 1001, 7, 1002, 9 }, 1, out var update));
            Assert.AreEqual(OwnershipEventKind.Update, update.Kind);
            Assert.AreEqual(2, update.PairCount);
            Assert.AreEqual("1001->7;1002->9", OwnershipEventParser.BuildPairSummary(update.ViewOwnerData));
            Assert.IsFalse(OwnershipEventParser.IsSupported(211));
            Assert.IsFalse(OwnershipEventParser.TryParse(209, new[] { 1001 }, 12, out _));
            Assert.IsFalse(OwnershipEventParser.TryParse(212, new[] { 1001, 7, 1002 }, 1, out _));
        }

        [TestMethod]
        public void BoundedQueue_DropsOldestAndRetainsNewest()
        {
            var queue = new BoundedConcurrentQueue<int>(3);
            for (int i = 1; i <= 5; i++) queue.Enqueue(i);

            QueueWindowStats stats = queue.TakeWindowStats();
            Assert.AreEqual(3, stats.Capacity);
            Assert.AreEqual(3, stats.Depth);
            Assert.AreEqual(3, stats.Peak);
            Assert.AreEqual(2L, stats.Dropped);
            Assert.IsTrue(queue.TryDequeue(out int first));
            Assert.AreEqual(3, first);
            Assert.IsTrue(queue.TryDequeue(out int second));
            Assert.AreEqual(4, second);
            Assert.IsTrue(queue.TryDequeue(out int third));
            Assert.AreEqual(5, third);
            Assert.AreEqual(0L, queue.TakeWindowStats().Dropped);
        }

        [TestMethod]
        public void AlertCooldown_IsolatedKeysAndReportsSuppressedPeak()
        {
            var tracker = new AlertCooldownTracker();

            AlertCooldownDecision first = tracker.Observe("ActorMethod:1:TestRpc", 0f, 20f, 10f);
            Assert.IsTrue(first.Emit);
            Assert.AreEqual(0, first.SuppressedCount);

            AlertCooldownDecision suppressed = tracker.Observe("ActorMethod:1:TestRpc", 1f, 45f, 10f);
            Assert.IsFalse(suppressed.Emit);
            Assert.AreEqual(1, suppressed.SuppressedCount);
            Assert.AreEqual(45f, suppressed.PeakValue, 0.001f);

            AlertCooldownDecision otherKey = tracker.Observe("ActorMethod:2:TestRpc", 1f, 20f, 10f);
            Assert.IsTrue(otherKey.Emit);

            AlertCooldownDecision released = tracker.Observe("ActorMethod:1:TestRpc", 10f, 25f, 10f);
            Assert.IsTrue(released.Emit);
            Assert.AreEqual(1, released.SuppressedCount);
            Assert.AreEqual(45f, released.PeakValue, 0.001f);
        }

        [TestMethod]
        public void PatchProfiler_EstimatesFromSamplesButKeepsExactCalls()
        {
            Assert.AreEqual(1000d, PatchProfiler.EstimateTotalTicks(100, 10, 100), 0.001d);
            Assert.AreEqual(0d, PatchProfiler.EstimateTotalTicks(0, 0, 100), 0.001d);

            PatchProfiler.Shutdown();
            PatchProfiler.MinReportMs = float.MaxValue;
            MethodInfo record = typeof(PatchProfiler).GetMethod("Record", BindingFlags.NonPublic | BindingFlags.Static);
            for (int i = 0; i < 100; i++) record.Invoke(null, new object[] { "test.method", 10L });

            var timings = (IDictionary)typeof(PatchProfiler)
                .GetField("_timings", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
            object timing = timings["test.method"];
            Assert.AreEqual(100, (int)timing.GetType().GetField("ExactCallCount").GetValue(timing));
            Assert.IsTrue((int)timing.GetType().GetField("SampledCallCount").GetValue(timing) < 100);

            var frames = (IDictionary)typeof(PatchProfiler)
                .GetField("_frameTimers", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
            object frame = frames["test.method"];
            Assert.AreEqual(100, (int)frame.GetType().GetField("Calls").GetValue(frame));
            PatchProfiler.Shutdown();
        }

        [TestMethod]
        public void MethodKey_IsCanonicalAndAcceptsLegacyIgnore()
        {
            MethodInfo method = typeof(OverloadedTarget).GetMethod(nameof(OverloadedTarget.Work), new[] { typeof(int), typeof(string) });
            string key = MethodKey.Canonical(method);
            StringAssert.Contains(key, typeof(OverloadedTarget).FullName + ".Work(");
            StringAssert.Contains(key, "System.Int32,System.String");

            var ignores = new HashSet<string>(StringComparer.Ordinal) { "OverloadedTarget.Work" };
            Assert.IsTrue(MethodKey.IsIgnored(method, ignores));
            ignores.Clear();
            ignores.Add(key);
            Assert.IsTrue(MethodKey.IsIgnored(method, ignores));
        }

        [TestMethod]
        public void FieldProbe_StaticRootReadsFirstStaticMember()
        {
            var expression = ExpressionEvaluator.Compile("ProbeStaticRoot.Current.Value");
            Assert.IsNull(expression.CompileError);
            Assert.AreEqual("42", ExpressionEvaluator.Evaluate(expression, null, null, null, null, 64));
        }

        [TestMethod]
        public void FieldProbe_GroupsRulesAndPatchesEachCallbackOnce()
        {
            string dir = Path.Combine(Path.GetTempPath(), "WhySoLaggy.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            string rulesPath = Path.Combine(dir, "fieldprobe.json");
            File.WriteAllText(rulesPath,
                "{\"enabled\":true,\"rules\":[" +
                "{\"target\":\"FieldProbeTarget.Add\",\"fields\":[\"__arg0\"]}," +
                "{\"target\":\"FieldProbeTarget.Add\",\"fields\":[\"__result\"]}," +
                "{\"target\":\"FieldProbeTarget.Add\",\"fields\":[\"__result\"]}]}" );

            var harmony = new Harmony("WhySoLaggy.Tests.FieldProbe." + Guid.NewGuid().ToString("N"));
            bool oldEnabled = FieldProbe.Enabled;
            string oldRulesPath = FieldProbe.RulesFilePath;
            try
            {
                FieldProbe.Shutdown();
                FieldProbe.Enabled = true;
                FieldProbe.RulesFilePath = rulesPath;
                FieldProbe.Initialize(harmony);

                MethodInfo target = typeof(FieldProbeTarget).GetMethod(nameof(FieldProbeTarget.Add));
                Patches patches = Harmony.GetPatchInfo(target);
                Assert.IsNotNull(patches);
                Assert.AreEqual(1, patches.Prefixes.Count(p => p.owner == harmony.Id));
                Assert.AreEqual(1, patches.Postfixes.Count(p => p.owner == harmony.Id));
                Assert.AreEqual(0, patches.Finalizers.Count(p => p.owner == harmony.Id));

                var mappings = (IDictionary)typeof(FieldProbe)
                    .GetField("_methodToRules", BindingFlags.NonPublic | BindingFlags.Static)
                    .GetValue(null);
                Assert.AreEqual(1, mappings.Count);
                Assert.AreEqual(3, ((IList)mappings[target]).Count);
            }
            finally
            {
                harmony.UnpatchSelf();
                FieldProbe.Shutdown();
                FieldProbe.Enabled = oldEnabled;
                FieldProbe.RulesFilePath = oldRulesPath;
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
            }
        }

        [TestMethod]
        public void StructuredLogger_BatchesRotatesSchemaAndReinitializes()
        {
            string dir = Path.Combine(Path.GetTempPath(), "WhySoLaggy.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            string csv = Path.Combine(dir, "whysolaggy_data.csv");
            try
            {
                File.WriteAllText(csv, "Old,Header\r\nold,data\r\n");
                StructuredLogger.Initialize(dir);
                Assert.AreEqual(1, Directory.GetFiles(dir, "whysolaggy_data_schema_*.csv").Length);
                Assert.AreEqual(StructuredLogger.ExpectedCsvHeader, ReadSharedLines(csv)[0].TrimStart('\uFEFF'));

                StructuredLogger.WriteEvent(new StructuredEvent
                {
                    Timestamp = "2026-08-28 00:00:00.000",
                    FrameNumber = 1,
                    Type = EventType.RpcCall,
                    Fields = new Dictionary<string, object> { { "RpcMethod", "TestRpc" } },
                });
                Assert.AreEqual(1, ReadSharedLines(csv).Length, "WriteEvent should only buffer until Tick/Flush.");
                StructuredLogger.Flush();
                Assert.AreEqual(2, ReadSharedLines(csv).Length);

                StructuredLogger.Shutdown();
                StructuredLogger.Initialize(dir);
                StructuredLogger.Shutdown();
            }
            finally
            {
                StructuredLogger.Shutdown();
                Directory.Delete(dir, true);
            }
        }

        [TestMethod]
        public void StructuredLogger_RetainsRotatedFilesPerFormatWithinLimits()
        {
            string dir = Path.Combine(Path.GetTempPath(), "WhySoLaggy.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                StructuredLogger.MaxRotatedFiles = 2;
                StructuredLogger.MaxRotatedStorageMB = 1;
                string csvBase = Path.Combine(dir, "whysolaggy_data");
                string jsonBase = Path.Combine(dir, "whysolaggy_events");
                for (int i = 0; i < 5; i++)
                {
                    File.WriteAllBytes(csvBase + "_size_20260828_00000" + i + ".csv", new byte[400000]);
                    File.WriteAllBytes(jsonBase + "_size_20260828_00000" + i + ".jsonl", new byte[400000]);
                }

                StructuredLogger.Initialize(dir);

                Assert.IsTrue(File.Exists(Path.Combine(dir, "whysolaggy_data.csv")));
                Assert.IsTrue(File.Exists(Path.Combine(dir, "whysolaggy_events.jsonl")));
                Assert.IsTrue(Directory.GetFiles(dir, "whysolaggy_data_size_*.csv").Length <= 2);
                Assert.IsTrue(Directory.GetFiles(dir, "whysolaggy_events_size_*.jsonl").Length <= 2);
                Assert.IsTrue(Directory.GetFiles(dir, "whysolaggy_data_size_*.csv").Sum(FileSize) <= 1024 * 1024);
                Assert.IsTrue(Directory.GetFiles(dir, "whysolaggy_events_size_*.jsonl").Sum(FileSize) <= 1024 * 1024);
                StructuredLogger.Shutdown();
            }
            finally
            {
                StructuredLogger.Shutdown();
                StructuredLogger.MaxRotatedFiles = 30;
                StructuredLogger.MaxRotatedStorageMB = 350;
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
            }
        }

        [TestMethod]
        public void StructuredLogger_RpcSchemaContainsEnqueueAndRuntimeColumns()
        {
            StringAssert.Contains(StructuredLogger.ExpectedCsvHeader, "QueueSequence");
            StringAssert.Contains(StructuredLogger.ExpectedCsvHeader, "EnqueueTimestamp");
            StringAssert.Contains(StructuredLogger.ExpectedCsvHeader, "EnqueueFrameNumber");
            StringAssert.Contains(StructuredLogger.ExpectedCsvHeader, "QueueDelayMs");
            StringAssert.Contains(StructuredLogger.ExpectedCsvHeader, "RpcPumpMs");
            StringAssert.Contains(StructuredLogger.ExpectedCsvHeader, "StructuredFlushMs");
        }

        [TestMethod]
        public void MonitorShutdown_IsIdempotentAndAllowsDisabledFieldProbeReinit()
        {
            NetworkAbuseDetector.Shutdown();
            NetworkAbuseDetector.Shutdown();
            RpcMonitor.Shutdown();
            RpcMonitor.Shutdown();
            PluginProfiler.Shutdown();
            PatchProfiler.Shutdown();
            MethodTracer.Shutdown();
            FieldProbe.Shutdown();
            FieldProbe.Enabled = false;
            FieldProbe.Initialize(null);
            FieldProbe.Shutdown();
            FieldProbe.Initialize(null);
            FieldProbe.Shutdown();
        }

        [TestMethod]
        public void ModConfigLocalization_CoversEveryWhySoLaggyConfigEntry()
        {
            var expected = new[]
            {
                "General/SpikeThresholdMs", "General/ReportIntervalSeconds", "General/EnablePluginProfiling",
                "General/EnablePatchProfiling", "General/TopMethodCount", "General/MinReportMs",
                "General/IgnorePluginGuids", "General/IgnorePatchMethods", "General/EnableMemoryMonitor",
                "AbuseDetection/EnableAbuseDetection", "AbuseDetection/CheckIntervalSeconds",
                "AbuseDetection/ReportIntervalSeconds", "AbuseDetection/InstantiateRateThreshold",
                "AbuseDetection/DestroyRateThreshold", "AbuseDetection/RpcRateThreshold",
                "AbuseDetection/ObjectSpikeThreshold", "AbuseDetection/ActorMethodRateThreshold",
                "AbuseDetection/OwnershipGrabRateThreshold", "AbuseDetection/OwnershipRequestRateThreshold",
                "AbuseDetection/AlertCooldownSeconds", "RpcMonitor/EnableRpcMonitor", "RpcMonitor/TopMethodCount",
                "RpcMonitor/WatchedRecordPerMethodCapacity", "RpcMonitor/WatchedShowPerMethod",
                "RpcMonitor/ExtraWatchMethods", "RpcMonitor/PumpBatchSize", "RpcMonitor/QueueCapacity",
                "Logging/LogVerbosity", "Logging/MaxLogFileSizeMB", "Logging/MaxRotatedFiles",
                "Logging/MaxRotatedStorageMB", "MethodTracer/TraceMethodNames", "MethodTracer/TraceMaxDepth",
                "MethodTracer/TraceRateLimit", "FieldProbe/EnableFieldProbe", "FieldProbe/RulesFile",
                "FieldProbe/DefaultRateLimit", "FieldProbe/DefaultMaxValueLen", "FieldProbe/DefaultIncludeStack",
                "FieldProbe/DefaultStackMaxDepth", "UI/ShowDashboard",
            };

            var actual = ModConfigLocalization.Entries
                .Select(entry => entry.Section + "/" + entry.Key)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();

            CollectionAssert.AreEqual(expected.OrderBy(value => value, StringComparer.Ordinal).ToArray(), actual);
            foreach (var entry in ModConfigLocalization.Entries)
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(entry.EnglishName));
                Assert.IsFalse(string.IsNullOrWhiteSpace(entry.ChineseName));
                Assert.IsFalse(string.IsNullOrWhiteSpace(entry.EnglishDescription));
                Assert.IsFalse(string.IsNullOrWhiteSpace(entry.ChineseDescription));
                Assert.AreNotEqual(entry.EnglishName, entry.ChineseName);
                Assert.AreNotEqual(entry.EnglishDescription, entry.ChineseDescription);
            }
        }

        [TestMethod]
        public void ModConfigLocalization_ScopesDuplicateKeysAndSupportsBothLanguages()
        {
            Assert.AreEqual("Top Method Count", ModConfigLocalization.GetLocalizedConfigText("General", "TopMethodCount", false));
            Assert.AreEqual("显示方法数量", ModConfigLocalization.GetLocalizedConfigText("General", "TopMethodCount", true));
            Assert.AreEqual("Top RPC Method Count", ModConfigLocalization.GetLocalizedConfigText("RpcMonitor", "TopMethodCount", false));
            Assert.AreEqual("显示 RPC 方法数量", ModConfigLocalization.GetLocalizedConfigText("RpcMonitor", "TopMethodCount", true));
            Assert.IsNull(ModConfigLocalization.GetLocalizedConfigText("Logging", "TopMethodCount", true));

            string english = ModConfigLocalization.GetLocalizedDescription("RpcMonitor", "QueueCapacity", false);
            string chinese = ModConfigLocalization.GetLocalizedDescription("RpcMonitor", "QueueCapacity", true);
            StringAssert.Contains(english, "Maximum queued RPC records");
            StringAssert.Contains(chinese, "RPC 记录队列");

            string traceEnglish = ModConfigLocalization.GetLocalizedDescription("MethodTracer", "TraceMethodNames", false);
            string traceChinese = ModConfigLocalization.GetLocalizedDescription("MethodTracer", "TraceMethodNames", true);
            StringAssert.Contains(traceEnglish, "expensive");
            StringAssert.Contains(traceChinese, "开销较高");
        }

        [TestMethod]
        public void ModConfigLocalization_RejectsOtherPluginsWithSharedSectionNames()
        {
            foreach (string category in new[]
            {
                "WhySoLaggy", "Why So Laggy", "com.wuyachiyu.WhySoLaggy",
            })
            {
                Assert.IsTrue(ModConfigLocalization.IsOwnCategory(category), category);
            }

            Assert.IsFalse(ModConfigLocalization.IsOwnCategory("SomeOtherMod"));
            Assert.IsFalse(ModConfigLocalization.IsOwnCategory("OtherSection"));
            foreach (string section in new[] { "General", "常规", "UI", "Logging", "RpcMonitor", "RPC Monitor" })
                Assert.IsFalse(ModConfigLocalization.IsOwnCategory(section), section);

            Assert.AreEqual("RPC 监控", ModConfigLocalization.GetLocalizedCategoryText("RpcMonitor", true));
            Assert.AreEqual("RPC 监控", ModConfigLocalization.GetLocalizedCategoryText("RPCMonitor", true));
            Assert.AreEqual("RPC Monitor", ModConfigLocalization.GetLocalizedCategoryText("rpcmonitor", false));
        }

        [TestMethod]
        public void ModConfigLocalization_ShutdownIsIdempotent()
        {
            ModConfigLocalization.Shutdown();
            ModConfigLocalization.Shutdown();
            Assert.IsNotNull(ModConfigLocalization.GetLocalizedConfigText("General", "SpikeThresholdMs", false));
        }

        [TestMethod]
        public void WatchedRpcNames_AllExistInPeak24bPunRpcSet()
        {
            string assemblyCSharp = FindPeak24bAssemblyCSharp();
            var rpcNames = new HashSet<string>(StringComparer.Ordinal);
            var declaration = new Regex(@"^\s*(?:public|private|protected|internal)\s+(?:static\s+)?[\w\.<>\[\],]+\s+(?<name>[A-Za-z_]\w*)\s*\(");

            foreach (string file in Directory.GetFiles(assemblyCSharp, "*.cs", SearchOption.AllDirectories))
            {
                string[] lines = File.ReadAllLines(file);
                for (int i = 0; i < lines.Length; i++)
                {
                    if (lines[i].Trim() != "[PunRPC]") continue;
                    for (int j = i + 1; j < Math.Min(i + 7, lines.Length); j++)
                    {
                        Match match = declaration.Match(lines[j]);
                        if (!match.Success) continue;
                        rpcNames.Add(match.Groups["name"].Value);
                        break;
                    }
                }
            }

            string[] missing = RpcMonitor.WatchedMethods.Where(name => !rpcNames.Contains(name)).OrderBy(name => name).ToArray();
            Assert.AreEqual(0, missing.Length, "Missing [PunRPC] in PEAK 2.4.b: " + string.Join(", ", missing));
            foreach (string added in new[] { "OnPickupAccepted", "SetItemInstanceDataRPC", "SetKinematicRPC", "RPCA_StartGrabbing", "RPCA_GrabCharacter", "RPC_SpawnItemInHandMaster" })
                Assert.IsTrue(RpcMonitor.WatchedMethods.Contains(added), "Expected watched RPC: " + added);
            foreach (string stale in new[] { "IncrementFriendHealingRpc", "IncrementPoisonHealedStat", "LightLanternRPC", "RPCA_AddStatusBingBing", "RPCA_ConsumeItem", "RPCA_FallWithScreenShake", "RPCA_Revive", "SetHeldItemID" })
                Assert.IsFalse(RpcMonitor.WatchedMethods.Contains(stale), "Stale watched RPC: " + stale);
            Assert.IsFalse(RpcMonitor.WatchedMethods.Contains("StartClimbRpc"));
            Assert.IsFalse(RpcMonitor.WatchedMethods.Contains("StopClimbingRpc"));
        }

        [TestMethod]
        public void AssemblyAndPluginVersions_Are105()
        {
            Assert.AreEqual("1.0.5", WhySoLaggyPlugin.PluginVersion);
            Assert.AreEqual(new Version(1, 0, 5, 0), typeof(WhySoLaggyPlugin).Assembly.GetName().Version);
        }

        [TestMethod]
        public void EventType_CompatibilityValuesRemainStable()
        {
            Assert.AreEqual(10, (int)EventType.RemoteRpcTrace);
            Assert.AreEqual(11, (int)EventType.OwnershipChange);
        }

        private static string FindPeak24bAssemblyCSharp()
        {
            foreach (string start in new[] { Environment.CurrentDirectory, AppDomain.CurrentDomain.BaseDirectory })
            {
                var directory = new DirectoryInfo(start);
                while (directory != null)
                {
                    string candidate = Path.Combine(directory.FullName, "引用参考代码", "反编译", "2.4.b", "Assembly-CSharp");
                    if (Directory.Exists(candidate)) return candidate;
                    directory = directory.Parent;
                }
            }
            Assert.Fail("Could not locate 引用参考代码\\反编译\\2.4.b\\Assembly-CSharp.");
            return null;
        }

        private static string[] ReadSharedLines(string path)
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = new StreamReader(stream))
            {
                return reader.ReadToEnd().Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            }
        }

        private static long FileSize(string path)
        {
            return new FileInfo(path).Length;
        }

        private sealed class OverloadedTarget
        {
            public void Work(int value, string text) { }
            public void Work(int value) { }
        }

        private sealed class ProbeHolder
        {
            public int Value = 42;
        }

        private static class FieldProbeTarget
        {
            public static int Add(int value)
            {
                return value + 1;
            }
        }

        private static class ProbeStaticRoot
        {
            public static ProbeHolder Current { get; } = new ProbeHolder();
        }
    }
}
