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
    public sealed class ReportTests
    {
        public TestContext TestContext { get; set; }
        private static RunRecord Run()
        {
            var r = new RunRecord(); r.header.runId = "report";
            r.statusTypeOrder = new[] { "Injury", "Hunger" };
            r.players.Add(new PlayerIdentity { playerIndex = 0, userId = "76561198000000001", displayName = "A", isLocal = true });
            r.players.Add(new PlayerIdentity { playerIndex = 1, userId = "actor:1", displayName = "B" });
            return r;
        }
        private static PlayerSample Sample(float t, bool dead = false, float x = 0)
        {
            return new PlayerSample { time = t, players = new List<PlayerTelemetry> {
                new PlayerTelemetry { playerIndex = 0, dead = dead, positionX = x, positionY = 100, maxStamina = 1, regularStamina = .5f, statuses = new[] { 0f, 0f } },
                new PlayerTelemetry { playerIndex = 1, positionX = 5, positionY = 100, maxStamina = 1, regularStamina = .5f, statuses = new[] { 0f, 0f } } },
                distancePlayerPairs = new[] { 0, 1 }, distanceMeters = new[] { Math.Abs(5 - x) } };
        }
        private static StatsEvent Death(float t) { return new StatsEvent { time = t, type = "PlayerDied", subjectPlayerIndex = 0 }; }
        private static AnalysisResult Analyze(RunChunk c, RunRecord r = null)
        { var engine = new RunAnalysisEngine(r ?? Run()); engine.AddChunk(c, CancellationToken.None); return engine.Finish(); }

        [TestMethod]
        public void DeathFactsAreDeduplicatedAndRequireStateEvidence()
        {
            var c = new RunChunk(); c.samples.AddRange(new[] { Sample(0), Sample(.5f), Sample(1, true), Sample(1.2f, true), Sample(1.5f, true) });
            c.events.AddRange(new[] { Death(.9f), Death(.9f), Death(.9f), Death(1.4f) });
            var r = Analyze(c); var p = r.players[0];
            Assert.AreEqual(1, p.deathCount); Assert.AreEqual(4, p.rawDeathCount); Assert.AreEqual(3, p.duplicateDeathCount);
            Assert.AreEqual(0, p.passedOutCount); Assert.AreEqual(0, p.unconfirmedDeathCount);
        }
        [TestMethod]
        public void RealRevivalRearmsAndUncorroboratedDeathStaysUnknown()
        {
            var c = new RunChunk(); c.samples.AddRange(new[] { Sample(0), Sample(.5f, true), Sample(.7f, true), Sample(1), Sample(1.2f), Sample(1.5f, true), Sample(1.7f, true) });
            c.events.AddRange(new[] { Death(.45f), Death(1.4f), new StatsEvent { time = 1, type = "PlayerDied", subjectPlayerIndex = 1 } });
            var r = Analyze(c); Assert.AreEqual(2, r.players[0].deathCount); Assert.AreEqual(1, r.players[1].unconfirmedDeathCount);
        }
        [TestMethod]
        public void RepeatedEventsAfterRapidRevivalCannotReusePreviousDeathInterval()
        {
            var c = new RunChunk(); c.samples.AddRange(new[] { Sample(0), Sample(.5f, true), Sample(.7f, true), Sample(1), Sample(1.2f), Sample(1.5f, true), Sample(1.7f, true), Sample(2, true) });
            c.events.AddRange(new[] { Death(.45f), Death(1.4f), Death(1.6f), Death(1.9f) });
            var r = Analyze(c); Assert.AreEqual(2, r.players[0].deathCount); Assert.AreEqual(2, r.players[0].duplicateDeathCount);
        }
        [TestMethod]
        public void FeedingConsumptionBelongsToRecipientNotInventoryOwner()
        {
            var c = new RunChunk(); c.samples.AddRange(new[] { Sample(0), Sample(.5f) });
            c.events.Add(new StatsEvent { type = "ItemConsumed", time = .25f, itemName = "Aid", itemGuid = "fed", subjectPlayerIndex = 1, actorPlayerIndex = 0, targetPlayerIndex = 1 });
            var r = Analyze(c); Assert.AreEqual(0, r.players[0].consumedCount); Assert.AreEqual(1, r.players[1].consumedCount);
        }
        [TestMethod]
        public void JumpEvidenceRetainsDistinctClockIntervals()
        {
            var c = new RunChunk(); c.samples.AddRange(new[] { Sample(0), Sample(1), Sample(.5f), Sample(2) });
            c.events.Add(new StatsEvent { type = "PlayerJumped", time = .25f, subjectPlayerIndex = 0 });
            c.events.Add(new StatsEvent { type = "PlayerJumped", time = .75f, subjectPlayerIndex = 0 });
            var r = Analyze(c); var p = r.players[0]; Assert.AreEqual(p.jumpTimes.Count, p.jumpEvidence.Count);
            Assert.AreEqual(0, p.jumpEvidence[p.jumpTimes.IndexOf(.25f)].epoch); Assert.AreEqual(-1, p.jumpEvidence[p.jumpTimes.IndexOf(.75f)].epoch);
        }
        [TestMethod]
        public void DeathSentinelDriftCannotStretchDistanceAxis()
        {
            var c = new RunChunk(); c.samples.Add(Sample(0)); var bad = Sample(.2f);
            bad.players[0].positionX = -.002f; bad.players[0].positionY = 4999.792f; bad.players[0].positionZ = -4999.972f;
            bad.distanceMeters[0] = 11137.35f; c.samples.Add(bad); c.samples.Add(Sample(.4f));
            var r = Analyze(c); Assert.IsTrue(r.distancePairs.All(p => p.max < 100)); Assert.IsTrue(r.diagnostics.Any(d => d.reason == "InvalidPosition"));
        }
        [TestMethod]
        public void RealLargeDistanceIsNotClampedAndInconsistentMetersAreExcluded()
        {
            var r = Run(); r.distanceUnitsToMeters = 1;
            var c = new RunChunk(); c.samples.Add(Sample(0, false, 5000)); c.samples.Add(Sample(.2f, false, 5000));
            var bad = Sample(.4f, false, 5000); bad.distanceMeters[0] = 11130.3086f; c.samples.Add(bad);
            var result = Analyze(c, r); Assert.AreEqual(4995, result.distancePairs.Single().max);
            Assert.IsTrue(result.diagnostics.Any(d => d.reason == "DistanceMismatch"));
        }
        [TestMethod]
        public void ClockRollbackRetainsEveryInventorySnapshot()
        {
            var c = new RunChunk { startTime = 0, endTime = 2 };
            c.samples.AddRange(new[] { Sample(0), Sample(1), Sample(.5f), Sample(1.5f) });
            foreach (float t in new[] { .25f, .75f, 1.25f }) c.inventorySnapshots.Add(new InventorySnapshot { time = t, playerIndex = 0, slots = new List<ItemSnapshot> { new ItemSnapshot { itemId = 1, itemName = "Aid", guid = "a", slot = "main:0" } } });
            var r = Analyze(c); Assert.AreEqual(1, r.quality.timeBackwardsCount); Assert.AreEqual(3, r.processedInventoryCount);
            Assert.IsTrue(r.items.Any(i => i.kind == "InventoryUnplaced" && i.epoch == -1));
        }
        [TestMethod]
        public void ProgressPointStartsItsStageAndLastStageReachesRunEnd()
        {
            var run = Run(); for (int i = 0; i < 2; i++) run.mountainSegments.Add(new MountainSegmentDefinition { index = i, titleKey = "S" + i });
            var c = new RunChunk(); c.samples.AddRange(new[] { Sample(0), Sample(1), Sample(2), Sample(3) });
            c.events.Add(new StatsEvent { time = 1, type = "MountainSegmentReached", segmentIndex = 0 }); c.events.Add(new StatsEvent { time = 2, type = "MountainSegmentReached", segmentIndex = 1 });
            var r = Analyze(c, run); Assert.AreEqual(0, r.segments[0].startTime); Assert.AreEqual(1, r.segments[1].startTime); Assert.AreEqual(3, r.segments.Last().endTime);
        }
        [TestMethod]
        public void RisksNeedDurationAndStopAtGaps()
        {
            var c = new RunChunk(); for (float t = 0; t <= 20; t += .25f) { var s = Sample(t); s.players[0].regularStamina = .01f; s.players[0].maxStamina = .2f; c.samples.Add(s); }
            var r = Analyze(c); Assert.IsTrue(r.episodes.Any(e => e.kind == "LowStamina" && e.activeSeconds >= 3)); Assert.IsTrue(r.episodes.Any(e => e.kind == "LowCapacity" && e.activeSeconds >= 10));
            c.samples.RemoveAll(s => s.time > 1 && s.time < 19); var shortResult = Analyze(c); Assert.IsFalse(shortResult.episodes.Any(e => e.kind == "LowStamina"));
        }
        [TestMethod]
        public void ClockReadsDoNotAccumulateAndCrossRunIdentityIsStrict()
        {
            double now = 10; var c = new RecordingClock(() => now); now = 11; Assert.AreEqual(1, c.Time); Assert.AreEqual(1, c.Time); c.Reset(20); now = 12; Assert.AreEqual(21, c.Time);
            Assert.IsTrue(ReportComparison.ValidSteamId("76561198000000001")); Assert.IsFalse(ReportComparison.ValidSteamId("actor:1")); Assert.IsFalse(ReportComparison.ValidSteamId("same name"));
            Assert.IsNull(ReportComparison.Rate(4, 0)); Assert.IsTrue(ReportComparison.Search("aid INJURY", new[] { "First Aid", "Injury" })); Assert.IsFalse(ReportComparison.Search("aid fuel", new[] { "Aid Injury" }));
        }
        [TestMethod]
        public void LatestRecordingReadOnlyReportRegression()
        {
            string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData/LocalLow/LandCrab/PEAK/StateKeeper/Runs");
            string id = "f1e61e22-26d3-4ca1-af87-0ff794ae5d0d"; string path = Path.Combine(root, id + ".json");
            if (!File.Exists(path)) Assert.Inconclusive("Optional local recording not present.");
            var watch = System.Diagnostics.Stopwatch.StartNew(); long memory = GC.GetTotalMemory(false);
            var run = JsonConvert.DeserializeObject<RunRecord>(File.ReadAllText(path)); var engine = new RunAnalysisEngine(run);
            foreach (var info in run.chunks)
                using (var f = File.OpenRead(Path.Combine(root, info.fileName)))
                using (var z = new GZipStream(f, CompressionMode.Decompress))
                using (var reader = new StreamReader(z)) { engine.AddChunk(JsonConvert.DeserializeObject<RunChunk>(reader.ReadToEnd()), CancellationToken.None); memory = Math.Max(memory, GC.GetTotalMemory(false)); }
            var result = engine.Finish();
            Assert.AreEqual(168, result.sourceChunkCount); Assert.AreEqual(28500, result.sourceSampleCount); Assert.AreEqual(38674, result.sourceEventCount);
            Assert.AreEqual(1676, result.processedInventoryCount); Assert.AreEqual(127, result.quality.timeBackwardsCount); Assert.AreEqual(6, result.mountainSegments.Count);
            Assert.IsTrue(result.distancePairs.All(p => p.max < 3000)); Assert.AreEqual(result.overview.durationSeconds, result.segments.Last().endTime);
            Assert.AreEqual(1, result.players.Single(p => p.playerIndex == 5).deathCount);
            Assert.AreEqual(4, result.players.Single(p => p.playerIndex == 3).deathCount);
            Assert.AreEqual(2, result.players.Single(p => p.playerIndex == 2).deathCount);
            Assert.AreEqual(8, result.overview.deathCount);
            Assert.AreEqual(6, result.players.Sum(p => p.duplicateDeathCount));
            Assert.IsTrue(result.timeline.All(e => e.evidence != null && e.evidence.runId == id));
            foreach (var p in result.players) TestContext.WriteLine(p.displayName + " deaths=" + p.deathCount + " raw=" + p.rawDeathCount + " duplicate=" + p.duplicateDeathCount + " unknown=" + p.unconfirmedDeathCount);
            TestContext.WriteLine("items=" + result.items.Count + " episodes=" + result.episodes.Count + " diagnostics=" + result.diagnostics.Count + " elapsedMs=" + watch.ElapsedMilliseconds + " observedProcessManagedBytes=" + memory);
        }

        [TestMethod]
        public void InitiallyDeadSingleFrameCannotConfirmAnUnrelatedDeath()
        {
            var c = new RunChunk(); c.samples.Add(Sample(1, true)); c.events.Add(Death(1));
            var r = Analyze(c); Assert.AreEqual(0, r.overview.deathCount); Assert.AreEqual(1, r.players[0].unconfirmedDeathCount);
        }
        [TestMethod]
        public void SinglePlayerObservationGapDoesNotResetTeammates()
        {
            var c = new RunChunk(); for (float t = 0; t <= 4; t += .2f) c.samples.Add(Sample(t));
            c.events.Add(new StatsEvent { type = "ObservationContextChanged", subjectPlayerIndex = 0, time = 1, value = 8 });
            c.events.Add(new StatsEvent { type = "ObservationContextChanged", subjectPlayerIndex = 0, time = 2, value = 24 });
            var r = Analyze(c); Assert.IsTrue(r.players[1].observedSeconds > 3.5f); Assert.AreEqual(1, r.players[1].staminaSeries.Select(p => p.epoch).Distinct().Count());
        }
        [TestMethod]
        public void TransformedCharacterCannotGenerateNormalRiskIntervals()
        {
            var c = new RunChunk(); for (float t = 0; t <= 6; t += .25f) { var s = Sample(t); s.players[0].regularStamina = 0; c.samples.Add(s); }
            c.events.Add(new StatsEvent { type = "ObservationContextChanged", subjectPlayerIndex = 0, time = 0, value = 26 });
            var r = Analyze(c); Assert.IsFalse(r.episodes.Any(e => e.playerIndex == 0 && e.kind == "LowStamina"));
        }
        [TestMethod]
        public void SharedDangerRequiresOverlapAndTogetherUsesHysteresis()
        {
            var c = new RunChunk(); for (float t = 0; t <= 40; t += .5f)
            {
                var s = Sample(t); foreach (var p in s.players) p.regularStamina = 0;
                s.distanceMeters[0] = t < 2 ? 20 : t < 38 ? 30 : 36; c.samples.Add(s);
            }
            var r = Analyze(c); Assert.IsTrue(r.episodes.Any(e => e.kind == "SharedDanger" && e.end - e.start >= 3));
            Assert.IsTrue(r.episodes.Where(e => e.kind == "SharedDanger").All(e => e.supportingPlayers.Count >= 2 && e.supportingEvidence.Count >= 1));
            var together = r.episodes.Single(e => e.kind == "Together"); Assert.AreEqual(0, together.start); Assert.IsTrue(together.end < 38);
        }
        [TestMethod]
        public void DistanceRangeQuantilesRespectEpochAndKeepPeaks()
        {
            var pair = new AnalysisDistancePair { intervals = new List<float> { 0, 1, 10, 0, 1, 1000 }, intervalEpochs = new List<int> { 0, 1 } };
            RunAnalysisEngine.SummarizeDistance(pair, 0, 1, 0); Assert.AreEqual(10, pair.median); Assert.AreEqual(1, pair.validSeconds);
            var c = new RunChunk(); c.samples.Add(Sample(0)); c.samples.Add(Sample(.2f)); c.samples[1].distanceMeters[0] = 50; c.samples.Add(Sample(.4f));
            var r = Analyze(c); var point = r.distancePairs[0].series.First(); Assert.AreEqual(50, point.maximum); Assert.AreEqual(5, point.minimum);
        }
        [TestMethod]
        public void ClockAmbiguousConsumeDoesNotUseLaterInventoryToInventActor()
        {
            var c = new RunChunk { startTime = 0, endTime = 3 }; c.samples.AddRange(new[] { Sample(0), Sample(1), Sample(.5f), Sample(1.5f) });
            c.events.Add(new StatsEvent { type = "ItemConsumed", time = .75f, itemName = "Aid", itemGuid = "a", subjectPlayerIndex = 1 });
            c.inventorySnapshots.Add(new InventorySnapshot { time = 1.3f, playerIndex = 0, slots = new List<ItemSnapshot> { new ItemSnapshot { guid = "a", itemId = 1, itemName = "Aid", slot = "main:0" } } });
            var r = Analyze(c); var consumed = r.items.Single(i => i.kind == "ItemConsumed"); Assert.AreEqual(-1, consumed.epoch); Assert.AreEqual(-1, consumed.actorPlayerIndex);
        }
        [TestMethod]
        public void MissingFieldsAndDifferentRecipientCoverageCannotBecomeComparableZero()
        {
            var a = new AnalysisResult { collectionCapabilities = new[] { "LocalRecipientTreatment" }, recordingUserId = "A" };
            var b = new AnalysisResult { collectionCapabilities = new[] { "LocalRecipientTreatment" }, recordingUserId = "B" };
            Assert.IsFalse(ReportComparison.ComparableHelp(a, b));
            a.overview.durationSeconds = 1000; Assert.IsFalse(ReportComparison.Reliable(a, new AnalysisPlayer { observedSeconds = 599 }));
            Assert.IsFalse(ReportComparison.Reliable(a, new AnalysisPlayer { observedSeconds = 799 }));
            Assert.IsTrue(ReportComparison.Reliable(a, new AnalysisPlayer { observedSeconds = 800 }));
        }
        [TestMethod]
        public void CancellationIsCheckedBeforeReportFinalization()
        {
            var engine = new RunAnalysisEngine(Run()); var source = new CancellationTokenSource(); source.Cancel();
            Assert.ThrowsException<OperationCanceledException>(() => engine.Finish(source.Token));
        }
        [TestMethod]
        public void ComparisonKeepsLightweightIdentityMetricsNotFullRuns()
        {
            var c = new RunChunk(); c.samples.Add(Sample(0)); c.samples.Add(Sample(.2f)); var r = Analyze(c); r.players[0].consumedCount = 7;
            var summary = ReportComparison.Summary(r);
            Assert.AreEqual(7, summary.players[0].consumedCount); Assert.AreEqual(r.players[0].stableUserId, summary.players[0].stableUserId);
            Assert.AreEqual(0, summary.players[0].staminaSeries.Count); Assert.IsTrue(r.players[0].staminaSeries.Count > 0);
            Assert.AreEqual(0, summary.distancePairs.Count);
        }
        [TestMethod]
        public void StateOnlyDeathRemainsSeparateFromConfirmedEventCount()
        {
            var c = new RunChunk(); c.samples.AddRange(new[] { Sample(0), Sample(.5f, true), Sample(.7f, true) });
            var r = Analyze(c); Assert.AreEqual(0, r.overview.deathCount); Assert.IsTrue(r.timeline.Any(e => e.kind == "DeathStateObserved"));
            Assert.IsTrue(r.lifecycleIntervals.Any(e => e.state == "Death" && e.entered));
        }
    }
}
