using System;
using System.Collections.Generic;
using System.Linq;

namespace StateKeeper
{
    internal sealed class ReportAnalysis
    {
        internal static readonly string[] MovementTypes = { "Unconscious", "Rope", "Vine", "Climbing", "Gliding", "Sprinting", "Grounded", "Airborne" };
        internal static string Movement(PlayerTelemetry p) => p.passedOut || p.fullyPassedOut ? "Unconscious" : p.ropeClimbing ? "Rope" : p.vineClimbing ? "Vine" : p.climbing ? "Climbing" : p.gliding ? "Gliding" : p.sprinting ? "Sprinting" : p.grounded ? "Grounded" : "Airborne";
        private sealed class Frame { internal float time; internal PlayerTelemetry value; }
        private sealed class Pending { internal ReportEpisode episode; internal float last; internal bool wasActive; internal float minimum; }
        private readonly AnalysisResult _result;
        private readonly Dictionary<int, AnalysisPlayer> _players;
        private readonly Dictionary<int, Frame> _previous = new Dictionary<int, Frame>();
        private readonly Dictionary<int, Queue<Frame>> _statusWindow = new Dictionary<int, Queue<Frame>>();
        private readonly Dictionary<string, Pending> _pending = new Dictionary<string, Pending>();
        private readonly Dictionary<string, bool> _together = new Dictionary<string, bool>();
        internal ReportAnalysis(AnalysisResult result) { _result = result; _players = result.players.ToDictionary(p => p.playerIndex); }

        internal void Interrupt(int player = -1)
        {
            foreach (string key in _pending.Where(p => player < 0 || p.Value.episode.playerIndex == player || p.Value.episode.otherPlayerIndex == player).Select(p => p.Key).ToArray()) Close(key);
            if (player < 0) { _previous.Clear(); _statusWindow.Clear(); _together.Clear(); }
            else
            {
                _previous.Remove(player); _statusWindow.Remove(player);
                foreach (string key in _together.Keys.Where(k => k.StartsWith(player + ":", StringComparison.Ordinal) || k.EndsWith(":" + player, StringComparison.Ordinal)).ToArray()) _together.Remove(key);
            }
        }

        internal void Sample(float time, Dictionary<int, PlayerTelemetry> current, EvidenceReference evidence)
        {
            foreach (int missing in _previous.Keys.Where(p => !current.ContainsKey(p)).ToArray()) Interrupt(missing);
            foreach (PlayerTelemetry p in current.Values)
            {
                if (p.dead || !RunAnalysisEngine.ValidStamina(p) || !RunAnalysisEngine.ValidPosition(p)) { Interrupt(p.playerIndex); continue; }
                Frame old;
                bool continuous = _previous.TryGetValue(p.playerIndex, out old) && time > old.time && time - old.time <= RunAnalysisEngine.GapLimit;
                if (!continuous) Interrupt(p.playerIndex);
                float dt = continuous ? time - old.time : 0;
                AnalysisPlayer player = _players[p.playerIndex];
                bool low = p.maxStamina > 0 && p.regularStamina <= p.maxStamina * ReportThresholds.LowRegularRatio && p.extraStamina <= ReportThresholds.LowExtra;
                Track("LowStamina", p.playerIndex, -1, null, time, low, ReportThresholds.LowStaminaDuration, evidence);
                Track("LowCapacity", p.playerIndex, -1, null, time, p.maxStamina <= ReportThresholds.LowCapacity, ReportThresholds.LowCapacityDuration, evidence);
                if (continuous)
                {
                    PlayerTelemetry before = old.value;
                    Add(player.movementSeconds, Movement(before), dt);
                    for (int i = 0; i < Math.Min(before.statuses?.Length ?? 0, _result.statusTypeOrder.Length); i++)
                        if (RunAnalysisEngine.Finite(before.statuses[i])) Add(player.statusIntegrals, _result.statusTypeOrder[i], Math.Max(0, before.statuses[i]) * dt);
                }
                Queue<Frame> window;
                if (!_statusWindow.TryGetValue(p.playerIndex, out window)) _statusWindow[p.playerIndex] = window = new Queue<Frame>();
                window.Enqueue(new Frame { time = time, value = p });
                while (window.Count > 0 && time - window.Peek().time > ReportThresholds.StatusWindow) window.Dequeue();
                var first = window.Peek().value;
                for (int i = 0; i < Math.Min(_result.statusTypeOrder.Length, Math.Min(p.statuses?.Length ?? 0, first.statuses?.Length ?? 0)); i++)
                {
                    string channel = _result.statusTypeOrder[i];
                    if (channel == "Petrify" || channel == "Weight") continue;
                    Track("StatusRise", p.playerIndex, -1, channel, time, p.statuses[i] - first.statuses[i] >= ReportThresholds.StatusRise - .000001f, 0, evidence);
                }
                _previous[p.playerIndex] = new Frame { time = time, value = p };
            }
        }

        internal void Pair(float time, int a, int b, float distance, bool continuous, EvidenceReference evidence)
        {
            string pair = Math.Min(a, b) + ":" + Math.Max(a, b);
            if (!continuous) { Close("Together:" + a + ":" + b + ":"); _together.Remove(pair); }
            bool together;
            if (!_together.TryGetValue(pair, out together)) together = false;
            together = together ? distance <= 35 : distance < 25;
            _together[pair] = together;
            Track("Together", a, b, null, time, together, 30, evidence, 0);
        }

        private static void Add(Dictionary<string, float> values, string key, float value)
        { float before; values.TryGetValue(key, out before); values[key] = before + value; }

        private void Track(string kind, int player, int other, string channel, float time, bool active, float minimum, EvidenceReference evidence, float gap = ReportThresholds.JoinGap)
        {
            string key = kind + ":" + player + ":" + other + ":" + channel;
            Pending pending;
            if (_pending.TryGetValue(key, out pending) && (pending.episode.evidence.epoch != evidence.epoch || time < pending.last || time - pending.last > RunAnalysisEngine.GapLimit || time - pending.episode.end > gap + RunAnalysisEngine.GapLimit))
            { Close(key); pending = null; }
            if (active)
            {
                if (pending == null)
                {
                    pending = new Pending { episode = new ReportEpisode { evidence = evidence, playerIndex = player, otherPlayerIndex = other, channel = channel, kind = kind, start = time, end = time }, last = time, minimum = minimum };
                    _pending[key] = pending;
                }
                else if (pending.wasActive) pending.episode.activeSeconds += time - pending.last;
                pending.episode.end = time;
            }
            if (pending != null)
            {
                pending.wasActive = active; pending.last = time;
                if (!active && time - pending.episode.end > gap) Close(key);
            }
        }

        private void Close(string key)
        {
            Pending value;
            if (!_pending.TryGetValue(key, out value)) return;
            if (value.episode.activeSeconds + .00001f >= value.minimum)
            {
                _result.episodes.Add(value.episode);
                AnalysisPlayer p;
                if (_players.TryGetValue(value.episode.playerIndex, out p))
                {
                    if (value.episode.kind == "LowStamina") p.lowStaminaSeconds += value.episode.activeSeconds;
                    if (value.episode.kind == "LowCapacity") p.lowCapacitySeconds += value.episode.activeSeconds;
                }
            }
            _pending.Remove(key);
        }

        internal void Finish()
        {
            Interrupt();
            var risks = _result.episodes.Where(e => e.kind != "Together").ToList();
            // Shared intervals use an exact sweep; never multiply counts by the number of risk types.
            foreach (var group in risks.GroupBy(e => e.evidence?.epoch ?? -1))
            {
                if (group.Key < 0) continue;
                var edges = group.SelectMany(e => new[] { Tuple.Create(e.start, e.playerIndex, 1), Tuple.Create(e.end, e.playerIndex, -1) }).OrderBy(e => e.Item1).GroupBy(e => e.Item1);
                var active = new Dictionary<int, int>(); float? start = null;
                foreach (var edge in edges)
                {
                    foreach (var point in edge) { int count; active.TryGetValue(point.Item2, out count); active[point.Item2] = count + point.Item3; }
                    bool shared = active.Count(p => p.Value > 0) >= 2;
                    if (shared && !start.HasValue) start = edge.Key;
                    if (!shared && start.HasValue)
                    {
                        if (edge.Key - start.Value >= 3)
                        {
                            var contributors = group.Where(e => e.end > start.Value && e.start < edge.Key).ToList();
                            var sources = contributors.Select(e => e.evidence).Distinct().ToList();
                            _result.episodes.Add(new ReportEpisode { evidence = sources[0], supportingEvidence = sources, supportingPlayers = contributors.Select(e => e.playerIndex).Distinct().ToList(), kind = "SharedDanger", start = start.Value, end = edge.Key, activeSeconds = edge.Key - start.Value });
                        }
                        start = null;
                    }
                }
            }
            foreach (ReportEpisode episode in _result.episodes)
                _result.timeline.Add(new ReportEvent { evidence = episode.evidence, supportingEvidence = episode.supportingEvidence, supportingPlayers = episode.supportingPlayers, kind = episode.kind, detail = episode.channel, time = episode.start, endTime = episode.end,
                    playerIndex = episode.playerIndex, otherPlayerIndex = episode.otherPlayerIndex, confidence = "Likely" });
            foreach (var group in _result.items.Where(i => i.kind == "ItemConsumed" || i.possibleHelp).GroupBy(i => i.attributionGroupId > 0 ? i.attributionGroupId : -i.observationId))
            {
                AnalysisItemObservation item = group.FirstOrDefault(i => i.kind == "ItemConsumed") ?? group.First();
                _result.timeline.Add(new ReportEvent { evidence = item.evidence, time = item.time, endTime = item.time, playerIndex = item.actorPlayerIndex,
                    otherPlayerIndex = item.targetPlayerIndex, kind = item.possibleHelp ? "PossibleHelp" : "ItemConsumed", itemObservationId = item.observationId,
                    confidence = item.possibleHelp ? item.attribution : item.confidence, detail = item.itemName });
            }
            _result.timeline = _result.timeline.OrderBy(e => e.evidence?.epoch ?? -1).ThenBy(e => e.time).ToList();
            foreach (AnalysisPlayer player in _result.players)
            {
                player.consumedCount = _result.items.Where(i => i.kind == "ItemConsumed" && i.targetPlayerIndex == player.playerIndex).Select(i => i.attributionGroupId > 0 ? i.attributionGroupId : -i.observationId).Distinct().Count();
                player.possibleHelpCount = _result.items.Where(i => i.possibleHelp && i.actorPlayerIndex == player.playerIndex).Select(i => i.attributionGroupId).Distinct().Count();
            }
            BuildItemUseSummaries();
        }

        private void BuildItemUseSummaries()
        {
            foreach (var row in _result.items.Where(r => r.attributionGroupId == 0 && (ItemUseRules.Direct(r) || ItemUseRules.Resource(r))))
                ItemUseRules.Classify(new List<AnalysisItemObservation> { row }, new string[0]);
            var rows = _result.items.Where(r => ItemUseRules.IsUse(r.useClassification) || r.useClassification == "Transferred" || r.useClassification == "DroppedOrLost").ToList();
            _result.itemUseSummaries = ItemUseRules.ItemSummaries(rows);
            _result.playerItemSummaries.Clear();
            foreach (var player in rows.GroupBy(ItemUseRules.GroupKey).Select(g => g.ToList()).GroupBy(g => {
                var row = ItemUseRules.Representative(g); return row.actorInferred && ItemUseRules.Direct(row) ? -1 : row.actorPlayerIndex;
            }))
            foreach (var summary in ItemUseRules.ItemSummaries(player.SelectMany(g => g)))
                _result.playerItemSummaries.Add(new AnalysisPlayerItemSummary {
                    playerIndex = player.Key, itemId = summary.itemId, itemName = summary.itemName, prefabName = summary.prefabName,
                    useCount = summary.useCount, certainCount = summary.certainCount, likelyCount = summary.likelyCount, possibleCount = summary.possibleCount,
                    resourceConsumption = summary.resourceConsumption, uncountedObservationCount = summary.uncountedObservationCount,
                    targets = summary.targets, observationIds = summary.observationIds, times = summary.times
                });
        }
    }
}
