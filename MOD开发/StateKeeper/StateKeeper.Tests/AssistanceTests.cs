using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;

namespace StateKeeper.Tests
{
    [TestClass]
    public sealed class AssistanceTests
    {
        [TestMethod]
        public void ActualHealing_RejectsBlockedAndNonFiniteChanges()
        {
            Assert.AreEqual(0f, AssistanceEvidence.HealedAmount(.8f, .8f));
            Assert.AreEqual(0f, AssistanceEvidence.HealedAmount(.8f, .9f));
            Assert.AreEqual(0f, AssistanceEvidence.HealedAmount(float.NaN, 0));
            Assert.AreEqual(0f, AssistanceEvidence.HealedAmount(1, float.NegativeInfinity));
            Assert.AreEqual(.025f, AssistanceEvidence.HealedAmount(.2f, .175f), .0001f);
        }

        [TestMethod]
        public void RescueThreshold_RejectsDuplicateActionsDeadSelfRecoveryAndPetrification()
        {
            Assert.IsTrue(AssistanceEvidence.RestoredRecoveryEligibility(true, false, 1.1f, .9f, 0));
            Assert.IsFalse(AssistanceEvidence.RestoredRecoveryEligibility(true, false, .9f, .7f, 0));
            Assert.IsFalse(AssistanceEvidence.RestoredRecoveryEligibility(true, false, 1.1f, 1f, 0));
            Assert.IsFalse(AssistanceEvidence.RestoredRecoveryEligibility(true, true, 1.1f, .9f, 0));
            Assert.IsFalse(AssistanceEvidence.RestoredRecoveryEligibility(false, false, 1.1f, .9f, 0));
            Assert.IsFalse(AssistanceEvidence.RestoredRecoveryEligibility(true, false, 1.1f, .9f, 100));
        }

        [TestMethod]
        public void Assistance_UsesExplicitActorOnlyAndSeparatesPullsFromTreatment()
        {
            var engine = Engine();
            var chunk = new RunChunk();
            StatsEvent saved = Outcome("PlayerLifeSaved", 1, 0, 1);
            chunk.events.Add(saved);
            chunk.events.Add(saved);
            chunk.events.Add(Outcome("PlayerFriendHealed", 1, 0, 1, .25f));
            chunk.events.Add(Outcome("PlayerRescuePulled", 2, 1, 2));
            chunk.events.Add(Outcome("PlayerLifeSaved", null, 0, 3));
            chunk.events.Add(Outcome("PlayerLifeSaved", 0, 0, 4));
            chunk.events.Add(new StatsEvent { type = "PlayerRevivedObserved", time = 5, subjectPlayerIndex = 2, actorPlayerIndex = 0 });
            engine.AddChunk(chunk, CancellationToken.None);
            AnalysisResult result = engine.Finish();
            Assert.AreEqual(1, result.overview.lifeSavedCount);
            Assert.AreEqual(1, result.players[1].lifeSavedCount);
            Assert.AreEqual(0, result.players[0].lifeSavedCount);
            Assert.AreEqual(1, result.players[2].rescuePullCount);
            Assert.AreEqual(.25f, result.overview.friendHealingAmount);
            Assert.AreEqual(5, result.assistance.Count);
            Assert.AreEqual(-1, result.assistance.Single(a => a.kind == "PlayerRevivedObserved").actorPlayerIndex);
        }

        [TestMethod]
        public void InventoryConflictOrDisappearance_DoesNotBecomeRescueOrTransfer()
        {
            var engine = Engine();
            var chunk = new RunChunk();
            chunk.inventorySnapshots.Add(Inventory(0, 0, "item"));
            chunk.inventorySnapshots.Add(Inventory(1, 1, "item"));
            // Persistent overlap outside the two-second handoff window stays a conflict.
            chunk.inventorySnapshots.Add(Inventory(4, 0, null));
            engine.AddChunk(chunk, CancellationToken.None);
            AnalysisResult result = engine.Finish();
            Assert.AreEqual(0, result.assistance.Count);
            Assert.AreEqual(0, result.overview.lifeSavedCount);
            Assert.IsFalse(result.items.Any(i => i.kind == "ItemTransferObserved"));
            Assert.IsTrue(result.items.Any(i => i.kind == "ItemRemovedObserved" && i.confidence == "Ambiguous"));
        }

        [TestMethod]
        public void OldRecording_HasUnknownCoverageAndPetrification()
        {
            var record = JsonConvert.DeserializeObject<RunRecord>("{\"schemaVersion\":3}");
            var engine = new RunAnalysisEngine(record);
            Assert.AreEqual(0, engine.Result.collectionCapabilities.Length);
            Assert.IsNull(JsonConvert.DeserializeObject<PlayerTelemetry>("{}").petrifyAmount);
            Assert.AreEqual(0, JsonConvert.DeserializeObject<PlayerTelemetry>("{\"petrifyAmount\":0}").petrifyAmount);
        }

        [TestMethod]
        public void OptionalAssistanceFields_RoundTrip()
        {
            StatsEvent value = Outcome("PlayerFriendHealed", 2, 1, 1, .25f);
            value.resourceKey = "Injury";
            StatsEvent loaded = JsonConvert.DeserializeObject<StatsEvent>(JsonConvert.SerializeObject(value));
            Assert.AreEqual(2, loaded.actorPlayerIndex);
            Assert.AreEqual(1, loaded.targetPlayerIndex);
            Assert.AreEqual("Injury", loaded.resourceKey);
        }

        private static RunAnalysisEngine Engine()
        {
            var record = new RunRecord();
            for (int i = 0; i < 3; i++) record.players.Add(new PlayerIdentity { playerIndex = i, displayName = "Player " + i });
            return new RunAnalysisEngine(record);
        }

        private static StatsEvent Outcome(string type, int? actor, int target, float time, float amount = 1)
        {
            return new StatsEvent { type = type, time = time, actorPlayerIndex = actor, targetPlayerIndex = target,
                subjectPlayerIndex = 0, value = amount, itemGuid = "item", itemName = "First Aid Kit" };
        }

        private static InventorySnapshot Inventory(float time, int player, string guid)
        {
            return new InventorySnapshot { time = time, playerIndex = player, slots = guid == null ? new List<ItemSnapshot>() :
                new List<ItemSnapshot> { new ItemSnapshot { guid = guid, itemName = "Bandages", slot = "main:0", itemId = 4 } } };
        }
    }
}
