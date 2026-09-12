using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;

namespace StateKeeper.Tests
{
    [TestClass]
    [DoNotParallelize]
    public sealed class AnalysisTests
    {
        private string _root;

        [TestInitialize]
        public void Initialize()
        {
            _root = Path.Combine(Path.GetTempPath(), "StateKeeper.Tests", Guid.NewGuid().ToString("N"));
        }

        [TestCleanup]
        public void Cleanup()
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
        }

        [TestMethod]
        public void Analysis_ProducesDynamicSegmentsDistanceStaminaAndGuidTransfer()
        {
            var store = new RunStore(_root);
            RunRecord record = CreateRun("analysis-run", 4);
            for (int i = 0; i < 5; i++)
                record.mountainSegments.Add(new MountainSegmentDefinition { index = i, titleKey = "SEGMENT_" + i, capturedTitle = "Segment " + i });
            record.samples.Add(CreateSample(0f, 0.95f, 1f));
            record.samples.Add(CreateSample(1f, 0.75f, 3f));
            record.samples.Add(CreateSample(2f, 0.55f, 5f));
            record.inventorySnapshots.Add(new InventorySnapshot { time = 0f, playerIndex = 0, slots = new List<ItemSnapshot> { Item("guid-transfer", 4) } });
            record.inventorySnapshots.Add(new InventorySnapshot { time = 0.8f, playerIndex = 0, slots = new List<ItemSnapshot>() });
            record.inventorySnapshots.Add(new InventorySnapshot { time = 1f, playerIndex = 1, slots = new List<ItemSnapshot> { Item("guid-transfer", 4) } });
            record.events.Add(new StatsEvent { time = 1f, type = "MountainSegmentReached", segmentIndex = 0, segmentKey = "SEGMENT_0" });
            record.events.Add(new StatsEvent { time = 2f, type = "MountainSegmentReached", segmentIndex = 1, segmentKey = "SEGMENT_1" });
            record.events.Add(new StatsEvent { time = 2f, type = "PlayerJumped", subjectPlayerIndex = 0 });
            store.Complete(record, RunStatus.Completed, RunOutcome.Victory);
            store.FlushPendingWrites();

            var service = new RunAnalysisService(store);
            service.Start(record.header.runId);
            WaitForAnalysis(service);

            Assert.IsNull(service.Error);
            Assert.IsNotNull(service.Result);
            Assert.AreEqual(5, service.Result.mountainSegments.Count);
            Assert.AreEqual(6, service.Result.segments.Count);
            Assert.AreEqual("INITIAL", service.Result.segments[0].titleKey);
            Assert.AreEqual(1, service.Result.overview.jumpCount);
            Assert.IsTrue(service.Result.distancePairs.Any(pair => pair.playerA == 0 && pair.playerB == 1));
            Assert.IsTrue(service.Result.players[0].staminaEpisodes.Count > 0);
            Assert.IsTrue(service.Result.items.Any(item => item.kind == "ItemTransferObserved" && item.confidence == "Likely"));
            Assert.IsTrue(File.Exists(Path.Combine(_root, "Analysis", record.header.runId + ".analysis.json.gz")));
        }

        [TestMethod]
        public void Analysis_MissingChunkIsReportedWithoutBlockingOtherData()
        {
            var store = new RunStore(_root);
            RunRecord record = CreateRun("missing-chunk", 2);
            record.samples.Add(CreateSample(0f, 0.8f, 2f));
            store.Complete(record, RunStatus.Completed, RunOutcome.Defeat);
            store.FlushPendingWrites();
            File.Delete(Path.Combine(_root, "Runs", record.chunks[0].fileName));

            var service = new RunAnalysisService(store);
            service.Start(record.header.runId);
            WaitForAnalysis(service);

            Assert.IsNull(service.Error);
            Assert.IsNotNull(service.Result);
            Assert.AreEqual(1, service.Result.quality.missingChunkCount);
            Assert.AreEqual(0, service.Result.quality.corruptChunkCount);
        }

        [TestMethod]
        public void PreviousAnalysisVersionCache_IsRebuilt()
        {
            var store = new RunStore(_root);
            RunRecord record = CreateRun("cache-version", 4);
            record.samples.Add(CreateSample(0, .8f, 2));
            store.Complete(record, RunStatus.Completed, RunOutcome.Victory); store.FlushPendingWrites();
            var service = new RunAnalysisService(store); service.Start(record.header.runId); WaitForAnalysis(service);
            AnalysisResult old = service.Result; old.analysisVersion = AnalysisResult.CurrentVersion - 1; old.overview.jumpCount = 999;
            string path = Path.Combine(_root, "Analysis", record.header.runId + ".analysis.json.gz");
            using (var file = File.Create(path))
            using (var gzip = new GZipStream(file, CompressionMode.Compress))
            using (var writer = new StreamWriter(gzip)) writer.Write(JsonConvert.SerializeObject(old));
            service.Start(record.header.runId); WaitForAnalysis(service);
            Assert.AreEqual(AnalysisResult.CurrentVersion, service.Result.analysisVersion);
            Assert.AreEqual(0, service.Result.overview.jumpCount);
            Assert.IsNotNull(service.Result.effects);
        }

        private static void WaitForAnalysis(RunAnalysisService service)
        {
            DateTime deadline = DateTime.UtcNow.AddSeconds(10);
            while (service.IsRunning && DateTime.UtcNow < deadline) Thread.Sleep(10);
            Assert.IsFalse(service.IsRunning, "Analysis did not finish within the test timeout.");
            if (service.Error != null) Assert.Fail(service.Error.ToString());
        }

        private static RunRecord CreateRun(string runId, int playerCount)
        {
            var record = new RunRecord();
            record.header.runId = runId;
            record.header.startedUtc = DateTime.UtcNow.ToString("o");
            for (int i = 0; i < playerCount; i++)
                record.players.Add(new PlayerIdentity { playerIndex = i, userId = "user-" + i, displayName = "Player " + i, isLocal = i == 0 });
            return record;
        }

        private static PlayerSample CreateSample(float time, float regularStamina, float spacing)
        {
            var sample = new PlayerSample { time = time };
            for (int i = 0; i < 4; i++)
                sample.players.Add(new PlayerTelemetry
                {
                    playerIndex = i,
                    regularStamina = regularStamina,
                    extraStamina = 0.1f,
                    maxStamina = 1f,
                    positionX = i * spacing,
                    positionY = 100f,
                    positionZ = i * spacing
                });
            sample.distancePlayerPairs = new[] { 0, 1, 0, 2, 0, 3, 1, 2, 1, 3, 2, 3 };
            sample.distanceMeters = new[] { spacing, spacing * 2f, spacing * 3f, spacing, spacing * 2f, spacing };
            return sample;
        }

        private static ItemSnapshot Item(string guid, int uses)
        {
            return new ItemSnapshot
            {
                slot = "main:0",
                itemId = 4,
                itemName = "Test Item",
                prefabName = "TestItem",
                guid = guid,
                hasUsesEntry = true,
                hasUsesValue = true,
                uses = uses
            };
        }
    }
}
