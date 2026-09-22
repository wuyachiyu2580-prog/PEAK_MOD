using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;

namespace StateKeeper.Tests
{
    [TestClass]
    public sealed class RecordingRegressionTests
    {
        public TestContext TestContext { get; set; }

        [TestMethod]
        public void AvailableRecordings_UseRulesReadOnlyReplay()
        {
            string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData", "LocalLow", "LandCrab", "PEAK", "StateKeeper", "Runs");
            if (!Directory.Exists(root)) Assert.Inconclusive("Optional local recordings unavailable");
            string[] paths = Directory.GetFiles(root, "*.json");
            if (paths.Length == 0) Assert.Inconclusive("No local manifests");
            int files = 0, samples = 0, inventory = 0, events = 0;
            foreach (string path in paths)
            {
                byte[] original = File.ReadAllBytes(path);
                var manifest = JsonConvert.DeserializeObject<RunRecord>(System.Text.Encoding.UTF8.GetString(original).TrimStart('\uFEFF'));
                var engine = new RunAnalysisEngine(manifest); var watch = Stopwatch.StartNew(); int availableInventory = 0;
                foreach (var info in manifest.chunks)
                {
                    string chunkPath = Path.Combine(root, info.fileName);
                    if (!File.Exists(chunkPath)) { engine.Result.quality.missingChunkCount++; engine.Break("MissingChunk"); continue; }
                    byte[] bytes = File.ReadAllBytes(chunkPath);
                    using (var memory = new MemoryStream(bytes))
                    using (var gzip = new GZipStream(memory, CompressionMode.Decompress))
                    using (var reader = new StreamReader(gzip))
                    using (var json = new JsonTextReader(reader))
                    {
                        var chunk = new JsonSerializer().Deserialize<RunChunk>(json);
                        Assert.AreEqual(info.sampleCount, chunk.samples.Count);
                        Assert.AreEqual(info.inventorySnapshotCount, chunk.inventorySnapshots.Count);
                        Assert.AreEqual(info.eventCount, chunk.events.Count);
                        samples += chunk.samples.Count; inventory += chunk.inventorySnapshots.Count; events += chunk.events.Count;
                        availableInventory += chunk.inventorySnapshots.Count;
                        engine.AddChunk(chunk, CancellationToken.None);
                    }
                    CollectionAssert.AreEqual(bytes, File.ReadAllBytes(chunkPath), "Replay changed a source chunk");
                    files++;
                }
                var result = engine.Finish();
                Assert.AreEqual(availableInventory, result.processedInventoryCount);
                foreach (var group in result.items.Where(r => r.attributionGroupId > 0).GroupBy(ItemUseRules.GroupKey))
                    Assert.AreEqual(1, group.Select(r => r.useClassification).Distinct().Count(), "Mixed verdicts inside a use group");
                Assert.AreEqual(result.itemUseSummaries.Sum(s => s.useCount), result.playerItemSummaries.Sum(s => s.useCount));
                Assert.IsTrue(result.items.Where(r => !ItemUseRules.IsUse(r.useClassification)).All(r => r.useCount == 0));
                Assert.IsTrue(result.itemUseSummaries.All(s => s.resourceConsumption.Values.All(v => v >= 0 && RunAnalysisEngine.Finite(v))));
                CollectionAssert.AreEqual(original, File.ReadAllBytes(path), "Replay changed manifest");
                TestContext.WriteLine(Path.GetFileNameWithoutExtension(path) + " samples=" + result.sourceSampleCount + " groups=" + result.items.GroupBy(ItemUseRules.GroupKey).Count() +
                    " useCount=" + result.itemUseSummaries.Sum(s => s.useCount) + " resourceOnly=" + result.itemUseSummaries.Sum(s => s.uncountedObservationCount) + " missing=" + result.quality.missingChunkCount + " ms=" + watch.ElapsedMilliseconds);
            }
            TestContext.WriteLine("Runs=" + paths.Length + " chunks=" + files + " samples=" + samples + " inventory=" + inventory + " events=" + events);
        }

        [TestMethod]
        public void SixReferenceRecordings_ReadOnlyReplay()
        {
            string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData", "LocalLow", "LandCrab", "PEAK", "StateKeeper");
            string[] ids = { "98fc17ac-7ede-495c-a0a2-7dd00f208fb7", "77f6bbb7-b690-4b95-9ee1-3b5091aea50d", "67eb3a83-ad86-453d-9f61-539c212ed026",
                "a120f547-2d68-4799-9acd-0e409f0acc15", "635b97e3-fbc3-4bde-a948-132ddaba6bae", "27276489-b68f-46eb-9993-1dfd89ffbed4" };
            int allSamples = 0, allInventory = 0, allEvents = 0, allChunks = 0;
            foreach (string id in ids)
            {
                string folder = new[] { "Runs", "Favorites" }.Select(f => Path.Combine(root, f)).FirstOrDefault(f => File.Exists(Path.Combine(f, id + ".json")));
                if (folder == null) Assert.Inconclusive("Optional local reference recording unavailable: " + id);
                RunRecord manifest = JsonConvert.DeserializeObject<RunRecord>(File.ReadAllText(Path.Combine(folder, id + ".json")));
                // Schema 2 is replayed through the pure engine only, never imported into RunStore or the UI.
                var watch = Stopwatch.StartNew();
                var engine = new RunAnalysisEngine(manifest);
                foreach (RunChunkInfo info in manifest.chunks)
                {
                    using (FileStream file = File.OpenRead(Path.Combine(folder, info.fileName)))
                    using (var gzip = new GZipStream(file, CompressionMode.Decompress))
                    using (var reader = new StreamReader(gzip))
                    using (var json = new JsonTextReader(reader))
                    {
                        RunChunk chunk = new JsonSerializer().Deserialize<RunChunk>(json);
                        Assert.AreEqual(info.sampleCount, chunk.samples.Count);
                        Assert.AreEqual(info.inventorySnapshotCount, chunk.inventorySnapshots.Count);
                        Assert.AreEqual(info.eventCount, chunk.events.Count);
                        Assert.AreEqual(info.sequence, chunk.sequence);
                        allSamples += chunk.samples.Count; allInventory += chunk.inventorySnapshots.Count; allEvents += chunk.events.Count; allChunks++;
                        engine.AddChunk(chunk, CancellationToken.None);
                    }
                }
                AnalysisResult result = engine.Finish();
                Assert.AreEqual(0, result.assistance.Count, "Old runs have no direct rescue evidence.");
                Assert.IsFalse(result.collectionCapabilities.Contains("LocalRecipientTreatment"));
                Assert.IsTrue(result.players.All(p => RunAnalysisEngine.Finite(p.staminaAverage)));
                Assert.IsTrue(result.distancePairs.All(p => p.over25Ratio >= p.over50Ratio && p.over50Ratio >= p.over100Ratio));
                if (manifest.schemaVersion == 3)
                {
                    Assert.AreEqual(174, result.sourceChunkCount);
                    Assert.AreEqual(29454, result.sourceSampleCount);
                    Assert.AreEqual(1366, result.sourceInventorySnapshotCount);
                    Assert.AreEqual(38441, result.sourceEventCount);
                    Assert.AreEqual(0, result.segments.Count);
                    var cross = result.items.Where(i => i.kind == "ItemConsumed" && i.actorInferred && i.actorPlayerIndex >= 0 && i.targetPlayerIndex >= 0 && i.actorPlayerIndex != i.targetPlayerIndex).ToList();
                    Assert.AreEqual(5, cross.Count, "Known GUID holder/consumer mismatches must remain readable.");
                    foreach (var item in cross) TestContext.WriteLine("CrossPlayer: " + item.time + " " + item.itemName + " " + item.actorPlayerIndex + "->" + item.targetPlayerIndex +
                        " attribution=" + item.attribution + " effects=" + item.observedEffects.Count + " reasons=" + string.Join(",", item.attributionReasons));
                }
                TestContext.WriteLine(id + " schema=" + manifest.schemaVersion + " chunks=" + manifest.chunks.Count + " elapsedMs=" + watch.ElapsedMilliseconds +
                    " backwards=" + result.quality.timeBackwardsCount + " itemRows=" + result.items.Count + " effects=" + result.effects.Count +
                    " likelyGroups=" + result.items.Where(i => i.attribution == "Likely").Select(i => i.attributionGroupId).Distinct().Count());
            }
            Assert.AreEqual(813, allChunks); Assert.AreEqual(136584, allSamples);
            Assert.AreEqual(8277, allInventory); Assert.AreEqual(277356, allEvents);
        }
    }
}
