using System;
using System.Collections.Generic;
using System.Linq;

namespace StateKeeper
{
    internal static class ReportComparison
    {
        internal static AnalysisResult Summary(AnalysisResult run)
        {
            return new AnalysisResult { runId = run.runId, schemaVersion = run.schemaVersion, analysisVersion = run.analysisVersion, sourceFingerprint = run.sourceFingerprint,
                overview = run.overview, hasAscentLevel = run.hasAscentLevel, ascentLevel = run.ascentLevel, hasCustomRun = run.hasCustomRun, isCustomRun = run.isCustomRun,
                gameVersion = run.gameVersion, collectionCapabilities = run.collectionCapabilities, mountainSegments = run.mountainSegments, recordingUserId = run.recordingUserId,
                players = run.players.Select(p => new AnalysisPlayer { playerIndex = p.playerIndex, stableUserId = p.stableUserId, displayName = p.displayName,
                    deathCount = p.deathCount, passedOutCount = p.passedOutCount, rawDeathCount = p.rawDeathCount, duplicateDeathCount = p.duplicateDeathCount,
                    unconfirmedDeathCount = p.unconfirmedDeathCount, jumpCount = p.jumpCount, observedSeconds = p.observedSeconds, aliveSeconds = p.aliveSeconds,
                    lowStaminaSeconds = p.lowStaminaSeconds, lowCapacitySeconds = p.lowCapacitySeconds, isolatedSeconds = p.isolatedSeconds, consumedCount = p.consumedCount,
                    possibleHelpCount = p.possibleHelpCount, rescuePullCount = p.rescuePullCount, lifeSavedCount = p.lifeSavedCount, friendHealingAmount = p.friendHealingAmount }).ToList() };
        }
        internal static bool ValidSteamId(string id)
        {
            ulong value;
            return id != null && id.Length == 17 && ulong.TryParse(id, out value) && (value >> 56) == 1 && ((value >> 52) & 15) == 1 && ((value >> 32) & 0xfffff) == 1;
        }
        internal static bool Reliable(AnalysisResult run, AnalysisPlayer player)
        { return player.observedSeconds >= 600 && run.overview.durationSeconds > 0 && player.observedSeconds / run.overview.durationSeconds >= .8f; }
        internal static float? Rate(float count, float seconds) { return seconds > 0 ? count * 600 / seconds : (float?)null; }
        internal static bool ComparableHelp(AnalysisResult a, AnalysisResult b)
        {
            string[] keys = { "LocalRecipientTreatment", "BroadcastRescuePull", "ReviveTargetOnly" };
            return keys.All(k => a.collectionCapabilities.Contains(k) == b.collectionCapabilities.Contains(k)) &&
                (!a.collectionCapabilities.Contains("LocalRecipientTreatment") || a.recordingUserId == b.recordingUserId);
        }
        internal static bool Search(string query, IEnumerable<string> fields)
        {
            string value = string.Join(" ", fields.Where(f => !string.IsNullOrEmpty(f)));
            return (query ?? "").Split((char[])null, StringSplitOptions.RemoveEmptyEntries).All(word => value.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0);
        }
    }
}
