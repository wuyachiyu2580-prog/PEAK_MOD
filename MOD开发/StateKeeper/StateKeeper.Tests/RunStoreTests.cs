using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;

namespace StateKeeper.Tests
{
    [TestClass]
    [DoNotParallelize]
    public sealed class RunStoreTests
    {
        private string _root;
        public TestContext TestContext { get; set; }

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
        public void EightPlayerChunk_StaysBoundedAndRecoversActiveData()
        {
            var store = new RunStore(_root);
            RunRecord record = CreateRun("run-eight-player");
            var stopwatch = Stopwatch.StartNew();

            for (int sample = 0; sample < 150; sample++)
            {
                record.samples.Add(CreateEightPlayerSample(sample * 0.2f));
                if ((sample + 1) % 25 == 0)
                {
                    store.SaveActive(record);
                    store.FlushPendingWrites();
                    Assert.IsTrue(record.samples.Count < 150, "A sealed chunk must release its in-memory samples.");
                }
            }

            Assert.AreEqual(1, record.chunks.Count);
            Assert.AreEqual(0, record.samples.Count);
            Assert.IsNull(record.activeChunk);

            record.samples.Add(CreateEightPlayerSample(30f));
            store.SaveActive(record);
            store.FlushPendingWrites();
            Assert.AreEqual(1, record.samples.Count);
            Assert.IsNotNull(record.activeChunk);

            RunRecord recovered = store.LoadActive();
            Assert.IsNotNull(recovered);
            Assert.AreEqual(1, recovered.chunks.Count);
            Assert.AreEqual(1, recovered.samples.Count);
            Assert.AreEqual(8, recovered.samples[0].players.Count);
            Assert.AreEqual(56, recovered.samples[0].distancePlayerPairs.Length);
            Assert.AreEqual(28, recovered.samples[0].distanceMeters.Length);

            store.Complete(record, RunStatus.Completed, RunOutcome.Victory);
            store.FlushPendingWrites();
            Assert.IsFalse(File.Exists(Path.Combine(_root, "active-run.json")));
            Assert.AreEqual(2, Directory.GetFiles(Path.Combine(_root, "Runs"), "*.json.gz").Length);
            long compressedBytes = Directory.GetFiles(Path.Combine(_root, "Runs"), "*.json.gz").Sum(path => new FileInfo(path).Length);
            Assert.AreEqual(240, (2 * 60 * 60 * 5) / 150, "Two hours at 5Hz should produce 240 sealed chunks.");
            Assert.IsTrue(compressedBytes < 1024 * 1024, "A synthetic 30-second 8-player chunk exceeded the size budget.");
            TestContext.WriteLine("151 samples, 8 players: {0:N0} compressed bytes in {1:N0} ms; projected two-hour chunk count: 240.",
                compressedBytes, stopwatch.ElapsedMilliseconds);
            Assert.IsTrue(stopwatch.Elapsed < TimeSpan.FromSeconds(10), "Synthetic 30-second chunk processing took unexpectedly long.");
        }

        [TestMethod]
        public void RetentionAndFavorite_MoveManifestTogetherWithChunks()
        {
            var store = new RunStore(_root);
            for (int i = 0; i < 11; i++)
            {
                RunRecord record = CreateRun("run-" + i.ToString("D2"));
                record.header.startedUtc = DateTime.UtcNow.AddMinutes(i).ToString("o");
                record.samples.Add(CreateEightPlayerSample(0f));
                store.Complete(record, RunStatus.Completed, RunOutcome.Defeat);
                store.FlushPendingWrites();
            }

            string runs = Path.Combine(_root, "Runs");
            string favorites = Path.Combine(_root, "Favorites");
            Assert.AreEqual(10, Directory.GetFiles(runs, "*.json").Length);
            Assert.AreEqual(10, Directory.GetFiles(runs, "*.json.gz").Length);

            Assert.IsTrue(store.ToggleFavorite("run-10"));
            Assert.AreEqual(1, Directory.GetFiles(favorites, "*.json").Length);
            Assert.AreEqual(1, Directory.GetFiles(favorites, "*.json.gz").Length);
            Assert.AreEqual(9, Directory.GetFiles(runs, "*.json").Length);
            Assert.AreEqual(9, Directory.GetFiles(runs, "*.json.gz").Length);

            RunIndexFile index = JsonConvert.DeserializeObject<RunIndexFile>(File.ReadAllText(Path.Combine(_root, "index.json")));
            Assert.AreEqual(10, index.entries.Count);
            Assert.AreEqual(1, index.entries.Count(entry => entry.favorite));
        }

        [TestMethod]
        public void Schema3Fields_RoundTripAndOptionalUsesRemainDistinguishable()
        {
            var record = CreateRun("schema3");
            record.header.customName = "中文 expedition";
            record.inventorySnapshots.Add(new InventorySnapshot
            {
                playerIndex = 0,
                slots = new List<ItemSnapshot>
                {
                    new ItemSnapshot
                    {
                        guid = "item-guid",
                        hasUsesEntry = true,
                        hasUsesValue = false,
                        hasPetterItemUses = true,
                        petterItemUses = 3,
                        hasUsed = true,
                        used = true,
                        hasFlareActive = true,
                        flareActive = true,
                        hasPowerEnabled = true,
                        powerEnabled = false
                    }
                }
            });
            record.events.Add(new StatsEvent { type = "ItemResourceChanged", itemGuid = "item-guid", previousItemGuid = "old-guid", resourceKey = "fuel", definitionKey = "Lantern", value = 0.5f, previousValue = 1f });
            string manifestJson = JsonConvert.SerializeObject(record);
            RunRecord roundTrip = JsonConvert.DeserializeObject<RunRecord>(manifestJson);
            ItemSnapshot item = JsonConvert.DeserializeObject<ItemSnapshot>(JsonConvert.SerializeObject(record.inventorySnapshots[0].slots[0]));
            StatsEvent eventRoundTrip = JsonConvert.DeserializeObject<StatsEvent>(JsonConvert.SerializeObject(record.events[0]));
            Assert.AreEqual(3, roundTrip.schemaVersion);
            Assert.AreEqual("中文 expedition", roundTrip.header.customName);
            Assert.IsTrue(item.hasUsesEntry);
            Assert.IsFalse(item.hasUsesValue);
            Assert.IsTrue(item.hasPetterItemUses);
            Assert.AreEqual(3, item.petterItemUses);
            Assert.IsTrue(item.used);
            Assert.IsTrue(item.flareActive);
            Assert.IsFalse(item.powerEnabled);
            Assert.AreEqual("old-guid", eventRoundTrip.previousItemGuid);
        }

        [TestMethod]
        public void OptionalItemData_HasDataFalseDoesNotProduceValidUses()
        {
            var data = new ItemInstanceData(Guid.NewGuid());
            data.data.Add(DataEntryKey.ItemUses, new OptionableIntItemData { HasData = false, Value = 99 });
            ItemSnapshot snapshot = RunCollector.CaptureItemResourcesForTests(data);
            Assert.IsTrue(snapshot.hasUsesEntry);
            Assert.IsFalse(snapshot.hasUsesValue);
            Assert.AreEqual(0, snapshot.uses);
        }

        [TestMethod]
        public void CustomNameFavoriteAndOldSchema_AreStableAcrossRestart()
        {
            var store = new RunStore(_root);
            RunRecord record = CreateRun("named-run");
            record.samples.Add(CreateEightPlayerSample(12.5f));
            store.Complete(record, RunStatus.Completed, RunOutcome.Victory);
            store.FlushPendingWrites();
            Assert.IsTrue(store.RenameRun("named-run", "  A\tlong\nname  "));
            Assert.IsTrue(store.ToggleFavorite("named-run"));
            var restarted = new RunStore(_root);
            RunIndexEntry entry = restarted.GetFavoriteEntries().Single();
            Assert.AreEqual("Alongname", entry.customName);
            Assert.AreEqual(8, entry.playerCount);
            Assert.AreEqual(1, entry.sampleCount);
            Assert.IsTrue(entry.favorite);

            string indexPath = Path.Combine(_root, "index.json");
            RunIndexFile mixedIndex = new RunIndexFile
            {
                schemaVersion = 3,
                entries = new List<RunIndexEntry>
                {
                    new RunIndexEntry { schemaVersion = 2, runId = "legacy", fileName = "legacy.json" },
                    new RunIndexEntry { schemaVersion = 3, runId = "named-run", favorite = true, fileName = "named-run.json" }
                }
            };
            File.WriteAllText(indexPath, JsonConvert.SerializeObject(mixedIndex));
            var filtered = new RunStore(_root);
            Assert.AreEqual(1, filtered.GetRecentEntries().Count + filtered.GetFavoriteEntries().Count);
            string activePath = Path.Combine(_root, "active-run.json");
            File.WriteAllText(activePath, "{\"schemaVersion\":2,\"header\":{\"runId\":\"legacy\"}}");
            Assert.IsNull(restarted.LoadActive());
            Assert.IsTrue(File.Exists(activePath));
        }

        private static RunRecord CreateRun(string runId)
        {
            var record = new RunRecord
            {
                statusTypeOrder = new[] { "Injury", "Hunger", "Cold", "Poison", "Crab", "Curse", "Drowsy", "Weight", "Hot", "Thorns", "Spores", "Web", "Arrow", "Petrify", "FlyTrap" }
            };
            record.header.runId = runId;
            record.header.startedUtc = DateTime.UtcNow.ToString("o");
            for (int i = 0; i < 8; i++)
                record.players.Add(new PlayerIdentity { playerIndex = i, userId = "user-" + i, displayName = "Player " + i, actorNumber = i + 1, isLocal = i == 0 });
            return record;
        }

        private static PlayerSample CreateEightPlayerSample(float time)
        {
            var sample = new PlayerSample { time = time };
            for (int player = 0; player < 8; player++)
            {
                sample.players.Add(new PlayerTelemetry
                {
                    playerIndex = player,
                    regularStamina = 0.75f,
                    extraStamina = 0.2f,
                    maxStamina = 0.9f,
                    positionX = player * 2.5f,
                    positionY = 100f + time,
                    positionZ = player * -3f,
                    climbing = player % 2 == 0,
                    statuses = Enumerable.Repeat(0.025f, 15).ToArray(),
                    activeAfflictionTypes = new List<int> { 1 }
                });
            }
            sample.distancePlayerPairs = new int[56];
            sample.distanceMeters = new float[28];
            int pair = 0;
            for (int from = 0; from < 8; from++)
                for (int to = from + 1; to < 8; to++)
                {
                    sample.distancePlayerPairs[pair * 2] = from;
                    sample.distancePlayerPairs[pair * 2 + 1] = to;
                    sample.distanceMeters[pair] = to - from;
                    pair++;
                }
            return sample;
        }
    }
}
