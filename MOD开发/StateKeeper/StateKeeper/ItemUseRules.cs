using System;
using System.Collections.Generic;
using System.Linq;

namespace StateKeeper
{
    // Use evidence and effect causality are separate: a completed action needs no visible effect.
    internal static class ItemUseRules
    {
        private const int DirectScore = 80, ResourceScore = 55, EffectScore = 20, RemovalEffectScore = 30;
        private const int ConflictPenalty = 40, InterruptedPenalty = 35;
        internal static bool Direct(AnalysisItemObservation r) => r.kind == "ItemConsumed" || r.kind == "ItemFedToPlayer" ||
            r.kind == "ItemPrimaryCastFinished" || r.kind == "ItemSecondaryCastFinished";
        internal static bool Resource(AnalysisItemObservation r) => r.kind == "ResourceChanged" && RunAnalysisEngine.Finite(r.value) && RunAnalysisEngine.Finite(r.previousValue) &&
            (r.resourceKey == "used" ? r.previousValue == 0 && r.value == 1 :
            new[] { "uses", "fuel", "useRemaining", "petterItemUses" }.Contains(r.resourceKey) && r.previousValue >= 0 && r.value >= 0 && r.value < r.previousValue);
        internal static bool IsUse(string value) => value == "CertainUse" || value == "LikelyUse" || value == "PossibleUse";
        internal static string Confidence(string value) => value == "CertainUse" ? "Certain" : value == "LikelyUse" ? "Likely" : "Ambiguous";
        internal static string GroupKey(AnalysisItemObservation r) => r.epoch + ":" + (r.attributionGroupId > 0 ? r.attributionGroupId : -r.observationId);

        internal static void Classify(List<AnalysisItemObservation> rows, IEnumerable<string> reasons)
        {
            var support = new List<string>(); var conflicts = new List<string>(); var excluded = new List<string>();
            var direct = rows.Where(Direct).ToList(); var resource = rows.Where(Resource).ToList();
            bool removed = rows.Any(r => r.kind == "ItemRemovedObserved");
            var barriers = reasons.Distinct().ToList();
            bool transfer = barriers.Contains("MovedOrTransferred") || rows.Any(r => r.kind == "ItemTransferObserved" || r.kind == "ItemMoved");
            bool interrupted = barriers.Contains("ObservationInterrupted");
            bool badIdentity = rows.Any(r => r.detail == "ConflictingGuid") || barriers.Contains("ConflictingGuid");
            bool badClock = rows.Any(r => r.epoch < 0) || rows.Select(r => r.epoch).Distinct().Count() != 1;
            bool effect = rows.SelectMany(r => r.observedEffects).Any(e => e.matchesTheory &&
                !e.reasons.Any(x => x != "DisappearanceOnly" && x != "InstanceUnknown") &&
                e.candidateGroupIds.Distinct().Count() == 1);
            int score = 0;
            if (direct.Count > 0) { score = DirectScore; support.Add("DirectAction"); }
            if (resource.Count > 0) { score = Math.Max(score, ResourceScore); support.Add("ResourceDelta"); }
            if (effect) { score = removed && direct.Count == 0 && resource.Count == 0 ? RemovalEffectScore : score + EffectScore; support.Add("ObservedEffect"); }
            if (removed && direct.Count == 0 && resource.Count == 0) conflicts.Add("DisappearanceOnly");
            if (badIdentity) { score -= ConflictPenalty; conflicts.Add("ConflictingGuid"); }
            // End-of-recording only truncates effect observation, not an already completed action.
            if (interrupted && direct.Count == 0) { score -= InterruptedPenalty; conflicts.Add("ObservationInterrupted"); }
            if (transfer && direct.Count == 0 && resource.Count == 0) { score = 0; conflicts.Add("MovedOrTransferred"); }
            if (badClock) { score = 0; conflicts.Add("ClockAmbiguous"); }
            if (direct.Count == 0 && resource.Count == 0 && !effect) excluded.Add("NoUseEvidence");
            string classification = badClock ? "Ambiguous" : transfer && direct.Count == 0 && resource.Count == 0 ? "Transferred" :
                removed && interrupted && direct.Count == 0 && resource.Count == 0 ? "DroppedOrLost" :
                score >= ReportThresholds.CertainUseScore ? "CertainUse" : score >= ReportThresholds.LikelyUseScore ? "LikelyUse" :
                score >= ReportThresholds.PossibleUseScore ? "PossibleUse" : "Ambiguous";
            // Resource observation alone is not an exact action count. Continuous fuel has no count.
            int count = Math.Max(direct.Count > 0 ? 1 : 0, resource.Where(r => r.resourceKey == "uses" || r.resourceKey == "petterItemUses")
                .Select(r => (int)Math.Min(int.MaxValue, Math.Floor((double)r.previousValue - r.value)))
                .DefaultIfEmpty(resource.Any(r => r.resourceKey == "used") ? 1 : 0).Max());
            if (!IsUse(classification)) count = 0;
            foreach (var row in rows)
            {
                row.useClassification = classification; row.attributionScore = Math.Max(0, Math.Min(100, score));
                row.supportingReasons = new List<string>(support); row.conflictingReasons = new List<string>(conflicts);
                row.excludedReasons = new List<string>(excluded); row.useCount = count;
            }
        }

        internal static List<AnalysisItemUseSummary> ItemSummaries(IEnumerable<AnalysisItemObservation> source)
        {
            // Deduplicate before grouping by display metadata (old events can lack the prefab name).
            return source.GroupBy(GroupKey).Select(g => g.ToList()).GroupBy(g => Identity(Representative(g)))
                .Select(g => Summarize(g.SelectMany(x => x).ToList())).ToList();
        }

        internal static AnalysisItemObservation Representative(IEnumerable<AnalysisItemObservation> rows) => rows
            .OrderByDescending(r => Direct(r)).ThenByDescending(r => !string.IsNullOrEmpty(r.prefabName)).ThenBy(r => r.time).First();
        private static string Identity(AnalysisItemObservation r) => r.itemId + ":" + (r.itemId == 0 ? r.prefabName ?? r.itemName : "");

        internal static AnalysisItemUseSummary Summarize(List<AnalysisItemObservation> rows)
        {
            var first = Representative(rows);
            var summary = new AnalysisItemUseSummary { itemId = first.itemId, itemName = first.itemName, prefabName = first.prefabName };
            foreach (var group in rows.GroupBy(GroupKey))
            {
                var action = Representative(group);
                int count = group.Max(r => r.useCount);
                summary.useCount += count;
                if (action.useClassification == "CertainUse") summary.certainCount += count;
                if (action.useClassification == "LikelyUse") summary.likelyCount += count;
                if (action.useClassification == "PossibleUse") summary.possibleCount += count;
                if (IsUse(action.useClassification) && count == 0) summary.uncountedObservationCount++;
                if (action.useClassification == "Transferred") summary.transferredCount++;
                if (action.useClassification == "DroppedOrLost") summary.droppedOrLostCount++;
                int actor = action.actorInferred && Direct(action) ? -1 : action.actorPlayerIndex;
                if (actor >= 0 && !summary.players.Contains(actor)) summary.players.Add(actor);
                foreach (int target in group.Select(r => r.targetPlayerIndex).Where(id => id >= 0).Distinct())
                    if (!summary.targets.Contains(target)) summary.targets.Add(target);
                summary.times.Add(action.time);
                summary.attributionGroupIds.Add(action.attributionGroupId);
                foreach (var r in group)
                {
                    summary.observationIds.Add(r.observationId);
                    if (r.evidence != null) summary.evidence.Add(r.evidence);
                }
                // Keep native units; fuel, charges and fractions must never be added together.
                foreach (var r in group.Where(Resource).GroupBy(r => new { r.resourceKey, r.time, r.previousValue, r.value }).Select(g => g.First()))
                {
                    float previous; summary.resourceConsumption.TryGetValue(r.resourceKey, out previous);
                    summary.resourceConsumption[r.resourceKey] = previous + Math.Abs(r.value - r.previousValue);
                }
            }
            return summary;
        }
    }
}
