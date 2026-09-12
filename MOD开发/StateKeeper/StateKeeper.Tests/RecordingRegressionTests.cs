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
