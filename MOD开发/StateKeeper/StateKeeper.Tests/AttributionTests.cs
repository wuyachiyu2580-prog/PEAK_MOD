using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace StateKeeper.Tests
{
    [TestClass]
    public sealed class AttributionTests
    {
        private static readonly string[] Statuses = { "Injury", "Hunger", "Poison", "Drowsy", "Cold", "Hot", "Spores" };

        [TestMethod]
        public void ResourceReduction_FindsNearbyRecipientWithoutFeedRpc()
        {
            RunRecord run = Run(Heal()); RunChunk chunk = Timeline(10, (t, p) => { if (p.playerIndex == 1 && t >= 1.25f) p.statuses[0] = .1f; });
            chunk.inventorySnapshots.Add(Inventory(0, 0, "a", 3)); chunk.inventorySnapshots.Add(Inventory(1, 0, "a", 2));
            AnalysisResult result = Analyze(run, chunk);
            AnalysisItemObservation use = result.items.Single(r => r.kind == "ResourceChanged");
            Assert.AreEqual("Likely", use.attribution, string.Join(",", use.attributionReasons));
            Assert.AreEqual(1, use.targetPlayerIndex); Assert.IsTrue(use.possibleHelp); Assert.IsFalse(use.possibleRescue);
            Assert.AreEqual(0, result.overview.lifeSavedCount);
        }

        [TestMethod]
        public void ConsumeCastAndInventoryDelta_FormOneCandidate()
        {
            var chunk = Timeline(10, (t, p) => { if (p.playerIndex == 1 && t >= 1.25f) p.statuses[0] = .1f; });
            chunk.inventorySnapshots.Add(Inventory(0, 0, "a", 3)); chunk.inventorySnapshots.Add(Inventory(1.4f, 0, "a", 2));
            chunk.events.Add(Use(1, "a", 0, 1));
            chunk.events.Add(new StatsEvent { time = 1.1f, type = "ItemPrimaryCastFinished", subjectPlayerIndex = 0, actorPlayerIndex = 0, targetPlayerIndex = 1, itemId = 1, itemGuid = "a", itemName = "Aid", definitionKey = "Aid" });
            AnalysisResult result = Analyze(Run(Heal()), chunk);
            var rows = result.items.Where(i => i.kind != "ItemAppeared").ToList();
            Assert.AreEqual(1, rows.Select(r => r.attributionGroupId).Distinct().Count());
            Assert.IsTrue(rows.All(r => r.attribution == "Likely"));
            Assert.IsFalse(rows.Any(r => r.attributionReasons.Contains("CompetingItems")));
        }

        [TestMethod]
        public void IndependentItemsCompete_AndDoNotBecomeCertainHelp()
        {
            var chunk = Timeline(10, (t, p) => { if (p.playerIndex == 1 && t >= 1.25f) p.statuses[0] = .1f; });
            chunk.events.Add(Use(1, "a", 0, 1)); chunk.events.Add(Use(1.1f, "b", 2, 1));
            var rows = Analyze(Run(Heal()), chunk).items;
            Assert.IsTrue(rows.All(r => r.attribution == "Ambiguous" && !r.possibleHelp));
            Assert.IsTrue(rows.All(r => r.observedEffects.Any(e => e.reasons.Contains("CompetingItems"))));
        }

        [TestMethod]
        public void DisappearanceAndTransfer_AreNotCertainUse()
        {
            var chunk = Timeline(10, (t, p) => { if (p.playerIndex == 1 && t >= 1.25f) p.statuses[0] = .1f; });
            chunk.inventorySnapshots.Add(Inventory(0, 0, "a", 3)); chunk.inventorySnapshots.Add(Inventory(1, 0, null, 0));
            chunk.inventorySnapshots.Add(Inventory(1.5f, 1, "a", 3));
            AnalysisItemObservation removal = Analyze(Run(Heal()), chunk).items.Single(r => r.kind == "ItemRemovedObserved");
            Assert.AreEqual("Ambiguous", removal.attribution); Assert.IsFalse(removal.possibleHelp);
            Assert.IsTrue(removal.attributionReasons.Contains("MovedOrTransferred"));
        }

        [TestMethod]
        public void NaturalPoisonDecay_IsNotCreditedToAConvenientItem()
        {
            ItemDefinition definition = Heal("Poison", -.025f);
            var chunk = Timeline(10, (t, p) => { p.statuses[2] = p.playerIndex == 1 && t >= 1.25f ? .175f : .2f; });
            chunk.events.Add(Use(1, "a", 0, 1));
            var row = Analyze(Run(definition), chunk).items.Single();
            Assert.AreEqual("Ambiguous", row.attribution);
            Assert.IsTrue(row.observedEffects.Any(e => e.reasons.Contains("NaturalRecoveryPossible")));
        }

        [TestMethod]
        public void EarlierOutcome_CanMatchNextFrameConsume()
        {
            var chunk = Timeline(10, (t, p) => { if (p.playerIndex == 1 && t >= .75f) p.statuses[0] = .1f; });
            chunk.events.Add(Use(1, "a", 0, 1));
            var row = Analyze(Run(Heal()), chunk).items.Single();
            Assert.AreEqual("Likely", row.attribution);
            Assert.IsTrue(row.observedEffects.Any(e => e.afterTime < row.time));
        }

        [TestMethod]
        public void DelayedPoison_ContinuesBeyondOldFourSecondWindow()
        {
            var definition = Definition(Action("Action_InflictPoison", P("delay", "10"), P("inflictionTime", "12"), P("poisonPerSecond", "0.025")));
            RunRecord run = Run(definition); run.afflictionTypeOrder = new[] { "PoisonOverTime" };
            var chunk = Timeline(28, (t, p) => { if (p.playerIndex == 1 && t >= 1.25f && t < 23) p.activeAfflictionTypes.Add(0); if (p.playerIndex == 1 && t >= 11.5f) p.statuses[2] = .025f; });
            chunk.events.Add(Use(1, "a", 0, 1));
            var row = Analyze(run, chunk).items.Single();
            Assert.IsTrue(row.observedEffects.Any(e => e.channel == "Poison" && e.afterTime > 10 && e.matchesTheory));
        }

        [TestMethod]
        public void MushroomContext_ControlsDelayedAfflictionAndNeverUsesFutureContext()
        {
            var definition = Definition(Action("Action_RandomMushroomEffect", P("mushroomTypeIndex", "0"), P("useDebugEffect", "False")));
            RunRecord run = Run(definition); run.afflictionTypeOrder = new[] { "Blind" };
            run.effectContexts.Add(new RunEffectContext { observedTime = 0, mushroomEffects = new[] { 6 }, mushroomStaminaAmounts = new[] { 0 } });
            var chunk = Timeline(12, (t, p) => { if (p.playerIndex == 1 && t >= 4.25f) p.activeAfflictionTypes.Add(0); });
            chunk.events.Add(Use(1, "a", 0, 1));
            Assert.AreEqual("Likely", Analyze(run, chunk).items.Single().attribution);
            run.effectContexts[0].observedTime = 2;
            var unknown = Analyze(run, chunk).items.Single();
            Assert.AreEqual("Ambiguous", unknown.attribution);
            Assert.IsTrue(unknown.attributionReasons.Contains("RandomContextNotRecorded"));
        }

        [TestMethod]
        public void AreaEffect_ExplainsMultipleRecipientsButKeepsMissingOriginUncertain()
        {
            var affliction = new ItemDefinitionParameter { name = "affliction", type = "Peak.Afflictions.Affliction_Invincibility", children = new List<ItemDefinitionParameter>() };
            var run = Run(Definition(Action("Action_ApplyMassAffliction", affliction, P("radius", "5"), P("ignoreCaster", "True"))));
            run.afflictionTypeOrder = new[] { "Invincibility" };
            var chunk = Timeline(10, (t, p) => { if (p.playerIndex != 0 && t >= 1.25f) p.activeAfflictionTypes.Add(0); });
            chunk.events.Add(Use(1, "a", 0, 0));
            var row = Analyze(run, chunk).items.Single();
            Assert.AreEqual(2, row.observedEffects.Where(e => e.matchesTheory).Select(e => e.playerIndex).Distinct().Count());
            Assert.AreEqual("Ambiguous", row.attribution);
            Assert.IsTrue(row.attributionReasons.Contains("ItemOriginNotRecorded"));
        }

        [TestMethod]
        public void PassiveHealing_IsAnIntervalAndStopsAfterDisabled()
        {
            var definition = Definition(); definition.components.Add(new ItemDefinitionComponent { typeName = "Peak.HealingAmulet" });
            var chunk = Timeline(10, (t, p) => { if (p.playerIndex == 0) { p.statuses[0] = t >= 3 ? .2f : t >= 1 ? .375f : .4f; p.petrifyAmount = t >= 1 ? 1 : 0; } });
            var inventory = Inventory(0, 0, "a", 3); inventory.slots[0].hasPowerEnabled = true; inventory.slots[0].powerEnabled = true;
            chunk.inventorySnapshots.Add(inventory);
            var disabled = Inventory(1.25f, 0, "a", 3); disabled.slots[0].hasPowerEnabled = true; disabled.slots[0].powerEnabled = false;
            chunk.inventorySnapshots.Add(disabled);
            var row = Analyze(Run(definition), chunk).items.Single(r => r.kind == "PassiveEffectObserved");
            Assert.AreEqual(1.25f, row.endTime);
            Assert.IsTrue(row.observedEffects.Any(e => e.channel == "Injury" && e.afterTime == 1));
            Assert.IsFalse(row.observedEffects.Any(e => e.afterTime >= 3));
        }

        [TestMethod]
        public void DeathGapOrTeleport_CannotBridgeIntoARecoveryAttribution()
        {
            for (int kind = 0; kind < 3; kind++)
            {
                var chunk = Timeline(10, (t, p) =>
                {
                    if (p.playerIndex != 1) return;
                    if (kind == 0 && t >= 1 && t < 2) p.dead = true;
                    if (kind == 2 && t >= 1.25f) p.positionX = 1000;
                    if (t >= 2) p.statuses[0] = .1f;
                });
                if (kind == 1) chunk.samples.RemoveAll(s => s.time > 1 && s.time < 2.5f);
                chunk.events.Add(Use(1, "a", 0, 1));
                var row = Analyze(Run(Heal()), chunk).items.Single();
                Assert.AreEqual("Ambiguous", row.attribution, "kind=" + kind);
                Assert.IsFalse(row.possibleRescue);
            }
        }

        [TestMethod]
        public void UnknownModAction_RemainsInspectableWithoutGuessedEffects()
        {
            var chunk = Timeline(10, (t, p) => { if (p.playerIndex == 1 && t >= 1.25f) p.statuses[0] = .1f; });
            chunk.events.Add(Use(1, "a", 0, 1));
            var row = Analyze(Run(Definition(Action("SomeMod.Action_ModifyStatus", P("statusType", "Injury"), P("changeAmount", "-0.3")))), chunk).items.Single();
            Assert.AreEqual("Ambiguous", row.attribution); Assert.AreEqual(0, row.rules.Count);
            Assert.IsTrue(row.observedEffects.Any());
        }

        [TestMethod]
        public void LegacyScalarParametersAndCooking_AreReadWithoutInventingEnums()
        {
            var hunger = new ItemDefinitionAction { typeName = "Action_RestoreHunger", trigger = "OnConsumed", parameterSummary = "restorationAmount=0.1,OnConsumed=True" };
            var definition = Definition(hunger);
            definition.cookingRules.Add(new ItemDefinitionCooking { typeName = "ItemCooking", parameterSummary = "ignoreDefaultCookBehavior=False,ignoreDefaultPoisonBehavior=False,wreckWhenCooked=False" });
            var rules = new RecordedItemRules(Run(definition)); var reasons = new List<string>();
            var compiled = rules.Compile(new AnalysisItemObservation { itemId = 1, prefabName = "Aid", cookedAmount = 1 }, reasons);
            Assert.AreEqual(-.2f, compiled.Single(r => r.channel == "Hunger").amount);
            Assert.AreEqual(.1f, compiled.Single(r => r.channel == "ExtraStamina").amount);
            Assert.IsNull(new RecordedItemRules.Parameters(null, "changeAmount=-0.3").String("statusType"));
        }

        [TestMethod]
        public void DirectTreatment_IsCertainEvenWhenNoFollowingFrameExists()
        {
            var chunk = new RunChunk();
            chunk.events.Add(new StatsEvent { time = 1, type = "PlayerFriendHealed", actorPlayerIndex = 0, targetPlayerIndex = 1, subjectPlayerIndex = 0,
                itemId = 1, itemName = "Aid", itemGuid = "a", resourceKey = "Injury", previousValue = .4f, value = .3f });
            var row = Analyze(Run(Heal()), chunk).items.Single();
            Assert.AreEqual("Certain", row.attribution);
            Assert.IsTrue(row.observedEffects.Any(e => e.attribution == "Certain" && e.reasons.Contains("DirectStatusObservation")));
        }

        [TestMethod]
        public void HealingBudget_DoesNotApplyFullBudgetToEveryStatus()
        {
            var nested = new ItemDefinitionParameter { name = "affliction", type = "Peak.Afflictions.Affliction_HealAll", children = new List<ItemDefinitionParameter> { P("maxHealing", "0.3") } };
            var chunk = Timeline(10, (t, p) => { p.statuses[2] = p.playerIndex == 1 && t >= 1.25f ? .1f : .2f; });
            chunk.events.Add(Use(1, "a", 0, 1));
            var row = Analyze(Run(Definition(Action("Action_ApplyAffliction", nested))), chunk).items.Single();
            Assert.IsTrue(row.observedEffects.Any(e => e.channel == "Poison" && e.reasons.Contains("AmountOrBudgetMismatch")));
        }

        [TestMethod]
        public void OneDose_HasACumulativeBudgetAcrossSamples()
        {
            var chunk = Timeline(10, (t, p) => { if (p.playerIndex == 1) p.statuses[0] = t >= 1.75f ? .2f : t >= 1.25f ? .4f : .6f; });
            chunk.events.Add(Use(1, "a", 0, 1));
            var row = Analyze(Run(Heal()), chunk).items.Single();
            Assert.IsTrue(row.observedEffects.Any(e => e.afterTime == 1.75f && e.reasons.Contains("AmountOrBudgetMismatch")));
        }

        [TestMethod]
        public void RecoveryAfterInferredHelp_IsPossibleRescueNotDirectCredit()
        {
            var chunk = Timeline(10, (t, p) => { if (p.playerIndex == 1) { p.passedOut = t < 1.25f; p.statuses[0] = t >= 1.25f ? .1f : .4f; } });
            chunk.inventorySnapshots.Add(Inventory(0, 0, "a", 3)); chunk.inventorySnapshots.Add(Inventory(1, 0, "a", 2));
            var result = Analyze(Run(Heal()), chunk); var row = result.items.Single(r => r.kind == "ResourceChanged");
            Assert.IsTrue(row.possibleRescue); Assert.AreEqual(0, result.overview.lifeSavedCount);
        }

        [TestMethod]
        public void MatchingTwoRecipientsOrConflictingGuid_DoesNotPickOneWinner()
        {
            var chunk = Timeline(10, (t, p) => { if (p.playerIndex != 0 && t >= 1.25f) p.statuses[0] = .1f; });
            chunk.inventorySnapshots.Add(Inventory(0, 0, "a", 3)); chunk.inventorySnapshots.Add(Inventory(1, 0, "a", 2));
            var row = Analyze(Run(Heal()), chunk).items.Single(r => r.kind == "ResourceChanged");
            Assert.AreEqual("Ambiguous", row.attribution); Assert.AreEqual(-1, row.targetPlayerIndex);
            chunk.inventorySnapshots.Insert(1, Inventory(.5f, 2, "a", 3));
            row = Analyze(Run(Heal()), chunk).items.Single(r => r.kind == "ResourceChanged");
            Assert.IsTrue(row.attributionReasons.Contains("ConflictingGuid"));
        }

        [TestMethod]
        public void AnalysisCancellation_IsObservedBeforeProcessingChunk()
        {
            var engine = new RunAnalysisEngine(Run(Heal()));
            var cancellation = new CancellationTokenSource(); cancellation.Cancel();
            Assert.ThrowsException<OperationCanceledException>(() => engine.AddChunk(Timeline(3, (t, p) => { }), cancellation.Token));
        }

        [TestMethod]
        public void UnknownConcurrentUse_PreventsKnownItemFromAutomaticallyWinning()
        {
            RunRecord run = Run(Heal());
            var unknown = Definition(Action("Mod.Unknown")); unknown.itemId = 2; unknown.prefabName = "Unknown"; run.definitions.Add(unknown);
            var chunk = Timeline(10, (t, p) => { if (p.playerIndex == 1 && t >= 1.25f) p.statuses[0] = .1f; });
            chunk.events.Add(Use(1, "a", 0, 1));
            StatsEvent second = Use(1.1f, "b", 2, 1); second.itemId = 2; second.definitionKey = "Unknown"; chunk.events.Add(second);
            var known = Analyze(run, chunk).items.Single(r => r.itemId == 1);
            Assert.AreEqual("Ambiguous", known.attribution);
            Assert.IsTrue(known.attributionReasons.Contains("UnknownItemCompetition"));
        }

        [TestMethod]
        public void CharacterCenteredAura_HasAnObservedSpatialOrigin()
        {
            var definition = Definition();
            definition.components.Add(new ItemDefinitionComponent { typeName = "Peak.InfiniteStamAmulet", parameters = new List<ItemDefinitionParameter> { P("radius", "5"), P("petrifyPerSecond", "2") } });
            RunRecord run = Run(definition); run.afflictionTypeOrder = new[] { "InfiniteStamina" };
            var chunk = Timeline(10, (t, p) => { if (p.playerIndex != 0 && t >= 1) p.activeAfflictionTypes.Add(0); });
            var inventory = Inventory(0, 0, "a", 3); inventory.slots[0].hasPowerEnabled = true; inventory.slots[0].powerEnabled = true;
            chunk.inventorySnapshots.Add(inventory);
            var row = Analyze(run, chunk).items.Single(r => r.kind == "PassiveEffectObserved");
            Assert.AreEqual("Likely", row.attribution); Assert.IsTrue(row.possibleHelp);
            Assert.IsFalse(row.possibleRescue); Assert.IsFalse(row.attributionReasons.Contains("ItemOriginNotRecorded"));
        }

        [TestMethod]
        public void Transfer_HandlesReceivingInventoryArrivingBeforeSourceRemoval()
        {
            var chunk = Timeline(6, (t, p) => { });
            chunk.inventorySnapshots.Add(Inventory(0, 0, "a", 3));
            chunk.inventorySnapshots.Add(Inventory(1, 1, "a", 3));
            chunk.inventorySnapshots.Add(Inventory(1.25f, 0, null, 0));
            var transfer = Analyze(Run(Heal()), chunk).items.Single(i => i.kind == "ItemTransferObserved");
            Assert.AreEqual(0, transfer.actorPlayerIndex); Assert.AreEqual(1, transfer.targetPlayerIndex);
            Assert.IsFalse(transfer.possibleHelp);
        }

        [TestMethod]
        public void ExpirySideEffect_UsesObservedRemovalInsteadOfAnAssumedTimer()
        {
            var affliction = new ItemDefinitionParameter { name = "affliction", type = "Peak.Afflictions.Affliction_FasterBoi",
                children = new List<ItemDefinitionParameter> { P("totalTime", "2"), P("drowsyOnEnd", "0.25"), P("climbDelay", "10") } };
            RunRecord run = Run(Definition(Action("Action_ApplyAffliction", affliction))); run.afflictionTypeOrder = new[] { "FasterBoi" };
            var chunk = Timeline(18, (t, p) =>
            {
                if (p.playerIndex != 1) return;
                if (t >= 1.25f && t < 12) p.activeAfflictionTypes.Add(0);
                if (t >= 12) p.statuses[3] = .25f;
            });
            chunk.events.Add(Use(1, "a", 0, 1));
            var row = Analyze(run, chunk).items.Single();
            Assert.IsTrue(row.observedEffects.Any(e => e.channel == "Drowsy" && e.afterTime == 12 && e.attribution == "Likely"));
        }

        [TestMethod]
        public void DuplicateDirectTreatment_DoesNotDuplicateProofOrHealingTotal()
        {
            var chunk = new RunChunk();
            var treatment = new StatsEvent { time = 1, type = "PlayerFriendHealed", actorPlayerIndex = 0, targetPlayerIndex = 1,
                itemId = 1, itemName = "Aid", itemGuid = "a", resourceKey = "Injury", previousValue = .4f, value = .3f };
            chunk.events.Add(treatment); chunk.events.Add(treatment);
            var result = Analyze(Run(Heal()), chunk);
            Assert.AreEqual(1, result.assistance.Count);
            Assert.AreEqual(1, result.items.Count);
            Assert.AreEqual(1, result.effects.Count);
            Assert.AreEqual(.3f, result.players.Single(p => p.playerIndex == 0).friendHealingAmount);
        }

        [TestMethod]
        public void LateCookingMetadata_PreservesDirectTreatmentRule()
        {
            var chunk = new RunChunk();
            chunk.events.Add(new StatsEvent { time = 1, type = "PlayerFriendHealed", actorPlayerIndex = 0, targetPlayerIndex = 1,
                itemId = 1, itemName = "Aid", itemGuid = "a", definitionKey = "Aid", resourceKey = "Injury", previousValue = .4f, value = .3f });
            chunk.inventorySnapshots.Add(Inventory(1.1f, 0, "a", 3));
            chunk.events.Add(Use(1.2f, "a", 0, 1));
            var result = Analyze(Run(Heal()), chunk);
            var use = result.items.Single(r => r.kind == "ItemConsumed");
            Assert.IsTrue(use.rules.Any(r => r.source == "DirectStatusObservation"));
            Assert.AreEqual(result.items.Single(r => r.kind == "PlayerFriendHealed").attributionGroupId, use.attributionGroupId);
        }

        private static RunRecord Run(ItemDefinition definition)
        {
            var run = new RunRecord { statusTypeOrder = Statuses, afflictionTypeOrder = new string[0] };
            run.definitions.Add(definition);
            for (int i = 0; i < 3; i++) run.players.Add(new PlayerIdentity { playerIndex = i, displayName = "P" + i });
            return run;
        }
        private static ItemDefinition Heal(string status = "Injury", float amount = -.3f)
        { return Definition(Action("Action_ModifyStatus", P("statusType", status), P("changeAmount", amount.ToString(System.Globalization.CultureInfo.InvariantCulture)), P("ifSkeleton", "False"))); }
        private static ItemDefinition Definition(params ItemDefinitionAction[] actions)
        { return new ItemDefinition { itemId = 1, itemName = "Aid", prefabName = "Aid", actions = actions.ToList() }; }
        private static ItemDefinitionAction Action(string name, params ItemDefinitionParameter[] parameters)
        { return new ItemDefinitionAction { typeName = name, trigger = "OnCastFinished,OnConsumed", parameters = parameters.ToList() }; }
        private static ItemDefinitionParameter P(string name, string value) { return new ItemDefinitionParameter { name = name, value = value }; }
        private static RunChunk Timeline(float duration, Action<float, PlayerTelemetry> modify)
        {
            var chunk = new RunChunk();
            for (float t = 0; t <= duration; t += .25f)
            {
                var sample = new PlayerSample { time = t };
                for (int i = 0; i < 3; i++)
                {
                    var p = new PlayerTelemetry { playerIndex = i, regularStamina = .5f, maxStamina = 1, petrifyAmount = 0, positionX = i, positionY = 100,
                        statuses = new[] { .4f, .4f, 0f, 0f, 0f, 0f, 0f }, activeAfflictionTypes = new List<int>() };
                    modify(t, p); sample.players.Add(p);
                }
                chunk.samples.Add(sample);
            }
            return chunk;
        }
        private static StatsEvent Use(float time, string guid, int actor, int target)
        { return new StatsEvent { time = time, type = "ItemConsumed", actorPlayerIndex = actor, subjectPlayerIndex = target, targetPlayerIndex = target, itemId = 1, itemGuid = guid, itemName = "Aid", definitionKey = "Aid" }; }
        private static InventorySnapshot Inventory(float time, int player, string guid, int uses)
        { return new InventorySnapshot { time = time, playerIndex = player, slots = guid == null ? new List<ItemSnapshot>() : new List<ItemSnapshot> { new ItemSnapshot { guid = guid, slot = "main:0", itemId = 1, itemName = "Aid", prefabName = "Aid", hasUsesEntry = true, hasUsesValue = true, uses = uses, hasCookedAmount = true, cookedAmount = 0 } } }; }
        private static AnalysisResult Analyze(RunRecord run, RunChunk chunk)
        { var engine = new RunAnalysisEngine(run); engine.AddChunk(chunk, CancellationToken.None); return engine.Finish(); }
    }
}
