using System;
using System.Collections.Generic;
using System.Linq;

namespace StateKeeper
{
    // Changes wait briefly before assignment so late inventory/Consume evidence competes fairly.
    // Only this small window retains raw frames; long-lived effects retain rules and aggregates.
    internal sealed class ItemAttributionEngine
    {
        internal const float Lead = .75f;
        internal const float Lag = 1.5f;
        private const float Settle = 2f;
        private readonly AnalysisResult _result;
        private readonly RunRecord _record;
        private readonly RecordedItemRules _rules;
        private readonly List<Candidate> _candidates = new List<Candidate>();
        private readonly List<Change> _changes = new List<Change>();
        private readonly Dictionary<int, Frame> _previous = new Dictionary<int, Frame>();
        private readonly Dictionary<int, float> _barriers = new Dictionary<int, float>();
        private readonly HashSet<int> _transformed = new HashSet<int>();
        private readonly Dictionary<string, Candidate> _passive = new Dictionary<string, Candidate>();
        private readonly Dictionary<string, AnalysisEffectChange> _lastEffects = new Dictionary<string, AnalysisEffectChange>();
        private float _lastTime = float.NaN;
        private float _environmentTime = float.NegativeInfinity;
        private int _epoch;
        private bool _trackingLimited;
        internal int PeakPendingChanges { get; private set; }

        internal ItemAttributionEngine(RunRecord record, AnalysisResult result)
        { _record = record; _result = result; _rules = new RecordedItemRules(record); }

        internal void Observe(AnalysisItemObservation row)
        {
            bool removal = row.kind == "ItemRemovedObserved";
            bool use = row.kind == "ItemConsumed" || row.kind == "ItemFedToPlayer" || row.kind == "ItemPrimaryCastFinished" || row.kind == "ItemSecondaryCastFinished";
            bool resource = row.kind == "ResourceChanged" && ((new[] { "uses", "fuel", "useRemaining", "petterItemUses" }.Contains(row.resourceKey) && row.value < row.previousValue) || row.resourceKey == "used" && row.previousValue == 0 && row.value == 1);
            if (row.kind == "ItemMoved" || row.kind == "ItemTransferObserved" || row.kind == "ItemAppeared")
            {
                if (row.kind != "ItemAppeared")
                {
                    row.useClassification = "Transferred";
                    row.conflictingReasons.Add("MovedOrTransferred");
                }
                foreach (Candidate prior in _candidates.Where(c => c.weak && !string.IsNullOrEmpty(row.itemGuid) && c.row.itemGuid == row.itemGuid && c.row.epoch == row.epoch &&
                    !c.reasons.Contains("ObservationInterrupted") && row.time >= c.time && row.time - c.time <= 2))
                {
                    prior.reasons.Add("MovedOrTransferred");
                    row.attributionGroupId = prior.row.attributionGroupId;
                    prior.rows.Add(row);
                }
            }
            if (!use && !resource && !removal) return;
            if ((resource || removal) && row.actorPlayerIndex < 0) { row.actorPlayerIndex = row.playerIndex; row.actorInferred = true; }
            Candidate same = string.IsNullOrEmpty(row.itemGuid) ? null : _candidates.LastOrDefault(c => !c.passive && c.row.itemGuid == row.itemGuid &&
                !c.reasons.Contains("ObservationInterrupted") &&
                c.row.epoch == row.epoch && Math.Abs(c.time - row.time) <= Lead &&
                !c.rows.Any(r => r.kind == row.kind && r.resourceKey == row.resourceKey) &&
                !c.rows.Any(r => row.kind == "ItemPrimaryCastFinished" && r.kind == "ItemSecondaryCastFinished" ||
                    row.kind == "ItemSecondaryCastFinished" && r.kind == "ItemPrimaryCastFinished") &&
                (c.target < 0 || row.targetPlayerIndex < 0 || c.target == row.targetPlayerIndex) &&
                (c.actor < 0 || row.actorPlayerIndex < 0 || c.actor == row.actorPlayerIndex));
            if (same != null)
            {
                same.rows.Add(row); row.attributionGroupId = same.row.attributionGroupId;
                if (row.targetPlayerIndex >= 0) same.target = row.targetPlayerIndex;
                if (row.actorPlayerIndex >= 0) same.actor = row.actorPlayerIndex;
                same.weak &= removal; same.lastEvidence = Math.Max(same.lastEvidence, row.time);
                if (row.detail == "ConflictingGuid") same.reasons.Add("ConflictingGuid");
                if (row.cookedAmount.HasValue && !same.row.cookedAmount.HasValue)
                {
                    same.row.cookedAmount = row.cookedAmount;
                    var directRules = same.rules.Where(r => r.source == "DirectStatusObservation").ToList();
                    same.rules = _rules.Compile(same.row, same.reasons);
                    same.rules.AddRange(directRules);
                }
                if (row.kind == "ItemConsumed" || row.kind == "ItemFedToPlayer") same.explicitTarget = true;
                return;
            }
            var candidate = new Candidate { row = row, actor = row.actorPlayerIndex, target = row.targetPlayerIndex, time = row.time, lastEvidence = row.time,
                weak = removal, explicitTarget = row.kind == "ItemConsumed" || row.kind == "ItemFedToPlayer" };
            candidate.rows.Add(row); row.attributionGroupId = row.observationId;
            if ((resource || removal) && _barriers.TryGetValue(row.playerIndex, out float barrier) && row.time >= barrier && row.time - barrier <= 2)
                candidate.reasons.Add("ObservationInterrupted");
            candidate.rules = _rules.Compile(row, candidate.reasons);
            if (string.IsNullOrEmpty(row.itemGuid)) candidate.reasons.Add("InstanceUnknown");
            if (row.detail == "ConflictingGuid") candidate.reasons.Add("ConflictingGuid");
            _candidates.Add(candidate);
            if (_candidates.Count > 1024)
            {
                _trackingLimited = true;
                FinishCandidate(_candidates[0], "TrackingLimitReached");
                if (!_result.quality.warnings.Contains("Attribution candidate limit reached")) _result.quality.warnings.Add("Attribution candidate limit reached");
            }
        }

        internal void Inventory(InventorySnapshot snapshot, int epoch, EvidenceReference evidence = null)
        {
            _epoch = epoch;
            var slots = (snapshot.slots ?? new List<ItemSnapshot>()).Where(i => i != null && !string.IsNullOrEmpty(i.guid)).ToList();
            foreach (var entry in _passive.Where(p => p.Value.actor == snapshot.playerIndex).ToArray())
            {
                if (!slots.Any(i => i.guid == entry.Value.row.itemGuid && PocketEnabled(i)))
                { entry.Value.end = snapshot.time; _passive.Remove(entry.Key); }
            }
            foreach (ItemSnapshot item in slots.Where(PocketEnabled))
            {
                string key = snapshot.playerIndex + ":" + item.guid;
                if (_passive.ContainsKey(key)) continue;
                ItemDefinition definition = RunAnalysisEngine.Definition(_record.definitions ?? new List<ItemDefinition>(), item.itemId, item.prefabName);
                List<AnalysisEffectRule> rules = _rules.Passive(definition);
                if (rules.Count == 0) continue;
                var row = new AnalysisItemObservation { evidence = evidence, observationId = _result.items.Count + 1, time = snapshot.time, epoch = epoch, playerIndex = snapshot.playerIndex,
                    actorPlayerIndex = snapshot.playerIndex, actorInferred = true, targetPlayerIndex = snapshot.playerIndex, itemId = item.itemId,
                    itemGuid = item.guid, itemName = item.itemName, prefabName = item.prefabName, kind = "PassiveEffectObserved", confidence = "Likely" };
                row.attributionGroupId = row.observationId; _result.items.Add(row);
                var candidate = new Candidate { row = row, time = snapshot.time, actor = snapshot.playerIndex, target = snapshot.playerIndex, passive = true, rules = rules };
                candidate.rows.Add(row); _passive[key] = candidate; _candidates.Add(candidate);
            }
        }

        private static bool PocketEnabled(ItemSnapshot item)
        {
            return item.hasPowerEnabled && item.powerEnabled && item.slot != null && (item.slot.StartsWith("main:", StringComparison.Ordinal) || item.slot == "temporary");
        }

        internal void Event(StatsEvent e, int epoch)
        {
            if (e == null || !Finite(e.time)) return;
            _epoch = epoch;
            if (e.type == "MountainSegmentReached" || e.type == "CampfireLit" || e.type == "CampfireRested") _environmentTime = e.time;
            if (e.type == "ObservationContextChanged")
            {
                if (((int)e.value & 6) != 0) _transformed.Add(e.subjectPlayerIndex); else _transformed.Remove(e.subjectPlayerIndex);
                Interrupt(e.subjectPlayerIndex, e.time);
            }
            if (e.type == "PlayerDied" || e.type == "PlayerRevivedObserved" || e.type == "PlayerTeleported") Interrupt(e.subjectPlayerIndex, e.time);
        }

        internal void DirectTreatment(StatsEvent e, EvidenceReference evidence = null)
        {
            if (!e.actorPlayerIndex.HasValue || e.actorPlayerIndex.Value == e.targetPlayerIndex || e.targetPlayerIndex < 0 || string.IsNullOrEmpty(e.resourceKey) ||
                !Finite(e.value) || !Finite(e.previousValue) || e.value <= 0 || e.previousValue < e.value) return;
            var row = new AnalysisItemObservation { evidence = evidence, observationId = _result.items.Count + 1, time = e.time, epoch = _epoch, itemId = e.itemId,
                itemName = e.itemName, itemGuid = e.itemGuid, prefabName = e.definitionKey, kind = "PlayerFriendHealed", confidence = "Certain",
                playerIndex = e.actorPlayerIndex.Value, actorPlayerIndex = e.actorPlayerIndex.Value, targetPlayerIndex = e.targetPlayerIndex };
            Candidate c = _candidates.LastOrDefault(p => p.row.epoch == _epoch && !p.reasons.Contains("ObservationInterrupted") && !string.IsNullOrEmpty(e.itemGuid) && p.row.itemGuid == e.itemGuid && Math.Abs(p.time - e.time) <= Lead &&
                (p.actor < 0 || p.actor == e.actorPlayerIndex) && (p.target < 0 || p.target == e.targetPlayerIndex));
            if (c == null)
            {
                c = new Candidate { row = row, actor = row.actorPlayerIndex, target = row.targetPlayerIndex, time = row.time, lastEvidence = row.time,
                    rules = new List<AnalysisEffectRule>(), explicitTarget = true };
                row.attributionGroupId = row.observationId; _candidates.Add(c);
            }
            row.attributionGroupId = c.row.attributionGroupId; _result.items.Add(row); c.rows.Add(row);
            c.actor = row.actorPlayerIndex; c.target = row.targetPlayerIndex; c.explicitTarget = true; c.weak = false;
            c.proofs.Add(e);
            c.rules.Add(new AnalysisEffectRule { channel = e.resourceKey, amount = -e.value, direction = -1, source = "DirectStatusObservation" });
            var proof = new AnalysisEffectChange { epoch = _epoch, channel = e.resourceKey, playerIndex = e.targetPlayerIndex, previousValue = e.previousValue,
                value = e.previousValue - e.value, cumulativeDelta = -e.value, beforeTime = e.time, afterTime = e.time,
                matchesTheory = true, attribution = "Certain", reasons = new List<string> { "DirectStatusObservation" }, candidateGroupIds = new List<int> { row.attributionGroupId } };
            c.effects.Add(StoreEffect(proof, c));
        }

        internal void Sample(PlayerSample sample, int epoch)
        {
            _epoch = epoch;
            if (!Finite(sample.time)) return;
            if (Finite(_lastTime) && (sample.time <= _lastTime || sample.time - _lastTime > RunAnalysisEngine.GapLimit)) Reset();
            var current = (sample.players ?? new List<PlayerTelemetry>()).Where(p => p != null).GroupBy(p => p.playerIndex).ToDictionary(g => g.Key, g => g.Last());
            foreach (int player in _previous.Keys.Where(id => !current.ContainsKey(id)).ToArray()) Interrupt(player, sample.time);
            foreach (var pair in current)
            {
                PlayerTelemetry p = pair.Value; Frame previous;
                if (!_previous.TryGetValue(pair.Key, out previous)) continue;
                float dt = sample.time - previous.time;
                if (dt <= 0 || dt > RunAnalysisEngine.GapLimit || p.dead || previous.value.dead)
                { Interrupt(pair.Key, sample.time); continue; }
                if (!RunAnalysisEngine.ValidStamina(p) || !RunAnalysisEngine.ValidStamina(previous.value))
                { Interrupt(pair.Key, sample.time); continue; }
                if (RunAnalysisEngine.ValidPosition(p) && RunAnalysisEngine.ValidPosition(previous.value) && WorldDistance(p, previous.value) > Math.Max(20f, 60f * dt))
                { Interrupt(pair.Key, sample.time); continue; }
                var before = previous.value;
                foreach (Candidate candidate in _candidates.Where(c => c.target < 0 || c.target == p.playerIndex))
                    foreach (string name in candidate.rules.Select(r => r.afterAfflictionEnds).Where(n => n != null).Distinct())
                    {
                        int id = Array.IndexOf(_result.afflictionTypeOrder, name);
                        bool had = id >= 0 && before.activeAfflictionTypes?.Contains(id) == true;
                        bool has = id >= 0 && p.activeAfflictionTypes?.Contains(id) == true;
                        if (has) candidate.lateEnd = Math.Max(candidate.lateEnd, sample.time + Lag + Settle);
                        if (had && !has)
                        {
                            candidate.endedAt[p.playerIndex + ":" + name] = sample.time;
                            candidate.lateEnd = Math.Max(candidate.lateEnd, sample.time + candidate.rules.Where(r => r.afterAfflictionEnds == name).Max(r => r.delay + r.duration) + Lag);
                        }
                    }
                ChangeValue("RegularStamina", before.regularStamina, p.regularStamina, previous, p, sample.time, current);
                ChangeValue("ExtraStamina", before.extraStamina, p.extraStamina, previous, p, sample.time, current);
                if (before.petrifyAmount.HasValue && p.petrifyAmount.HasValue) ChangeValue("Petrify", before.petrifyAmount.Value / 100f, p.petrifyAmount.Value / 100f, previous, p, sample.time, current);
                if (before.statuses != null && p.statuses != null)
                    for (int i = 0; i < Math.Min(_result.statusTypeOrder.Length, Math.Min(before.statuses.Length, p.statuses.Length)); i++)
                        if (_result.statusTypeOrder[i] != "Petrify") ChangeValue(_result.statusTypeOrder[i], before.statuses[i], p.statuses[i], previous, p, sample.time, current);
                if (before.activeAfflictionTypes != null && p.activeAfflictionTypes != null)
                {
                    foreach (int id in before.activeAfflictionTypes.Union(p.activeAfflictionTypes).Distinct())
                        if (id >= 0 && id < _result.afflictionTypeOrder.Length)
                            ChangeValue("Affliction:" + _result.afflictionTypeOrder[id], before.activeAfflictionTypes.Contains(id) ? 1 : 0, p.activeAfflictionTypes.Contains(id) ? 1 : 0, previous, p, sample.time, current);
                }
                ChangeValue("Unconscious", before.passedOut || before.fullyPassedOut ? 1 : 0, p.passedOut || p.fullyPassedOut ? 1 : 0, previous, p, sample.time, current);
            }
            _previous.Clear(); foreach (var p in current) _previous[p.Key] = new Frame { time = sample.time, value = p.Value };
            _lastTime = sample.time;
            PeakPendingChanges = Math.Max(PeakPendingChanges, _changes.Count);
            SettleChanges(sample.time - Settle, false);
            foreach (Candidate c in _candidates.Where(c => sample.time > End(c) + Lag + Settle).ToArray()) FinishCandidate(c, null);
        }

        private void ChangeValue(string channel, float before, float after, Frame frame, PlayerTelemetry player, float time, Dictionary<int, PlayerTelemetry> players)
        {
            if (!Finite(before) || !Finite(after) || Math.Abs(after - before) < .002f) return;
            _changes.Add(new Change { channel = channel, before = before, after = after, start = frame.time, time = time, player = player.playerIndex,
                beforePlayer = frame.value, afterPlayer = player, players = players, epoch = _epoch });
        }

        private void SettleChanges(float deadline, bool interrupted)
        {
            foreach (Change change in _changes.Where(c => c.time <= deadline).ToArray())
            {
                var matches = new List<Match>();
                var nearby = new List<Candidate>();
                foreach (Candidate candidate in _candidates)
                {
                    if (change.epoch != candidate.row.epoch || change.time < candidate.time - Lead || change.start > End(candidate) + Lag) continue;
                    if (candidate.explicitTarget && candidate.target == change.player && change.time <= candidate.time + Lag) nearby.Add(candidate);
                    if (change.channel == "Unconscious" && change.after < change.before &&
                        (candidate.target == change.player || candidate.recipients.Contains(change.player))) nearby.Add(candidate);
                    foreach (AnalysisEffectRule rule in candidate.rules)
                    {
                        if (rule.channel != change.channel || rule.direction == 0 || Math.Sign(change.after - change.before) != rule.direction || !TriggerMatches(candidate, rule)) continue;
                        float start = candidate.time + rule.delay, end = rule.continuous ? candidate.passive ? candidate.end : start + rule.duration : start;
                        if (rule.afterAfflictionEnds != null)
                        {
                            float ended;
                            if (!candidate.endedAt.TryGetValue(change.player + ":" + rule.afterAfflictionEnds, out ended)) continue;
                            start = ended + rule.delay; end = start + rule.duration;
                        }
                        if (change.time < start - Lead || change.time > end + Lag) continue;
                        var match = new Match { candidate = candidate, rule = rule };
                        if (!RecipientMatches(match, change)) continue;
                        if (rule.uncertain) match.reasons.Add("IncompleteRuleParameters");
                        if (!string.IsNullOrEmpty(rule.requiredAffliction))
                        {
                            bool ending = rule.requiredAffliction.StartsWith("Ended:", StringComparison.Ordinal);
                            string name = ending ? rule.requiredAffliction.Substring(6) : rule.requiredAffliction;
                            int id = Array.IndexOf(_result.afflictionTypeOrder, name);
                            bool had = id >= 0 && change.beforePlayer.activeAfflictionTypes?.Contains(id) == true;
                            bool has = id >= 0 && change.afterPlayer.activeAfflictionTypes?.Contains(id) == true;
                            if (ending ? !had || has : !had && !has) match.reasons.Add("RequiredAfflictionNotObserved");
                        }
                        CheckAmount(match, change);
                        if (_transformed.Contains(change.player) || _transformed.Contains(candidate.actor)) match.reasons.Add("TransformedCharacter");
                        float barrier;
                        if (_barriers.TryGetValue(change.player, out barrier) && barrier >= change.start - Lag && barrier <= change.time + Settle) match.reasons.Add("DeathRevivalOrDiscontinuity");
                        if (candidate.weak) match.reasons.Add("DisappearanceOnly");
                        match.reasons.AddRange(candidate.reasons.Where(r => r == "MovedOrTransferred" || r == "ConflictingGuid" || r == "ObservationInterrupted" || r == "InstanceUnknown"));
                        if (Natural(change)) match.reasons.Add("NaturalRecoveryPossible");
                        if (change.time >= _environmentTime && change.time - _environmentTime <= 5 && change.after < change.before) match.reasons.Add("CheckpointRecoveryPossible");
                        if (change.after < change.before && _changes.Any(other => other.player != change.player && other.channel == change.channel && other.after < other.before && Math.Abs(other.time - change.time) <= .5f)) match.reasons.Add("SharedEnvironmentRecoveryPossible");
                        if (interrupted) match.reasons.Add("IncompleteWindow");
                        if (_trackingLimited) match.reasons.Add("TrackingLimitReached");
                        matches.Add(match);
                    }
                }
                List<Match> direct = matches.Where(m => m.candidate.proofs.Any(p => p.resourceKey == change.channel && p.targetPlayerIndex == change.player &&
                    Math.Abs(p.time - change.time) <= Lag && Math.Abs(p.previousValue - change.before) <= .002f && Math.Abs(p.previousValue - p.value - change.after) <= .002f)).ToList();
                if (direct.Count > 0) { matches = direct; foreach (Match m in matches) m.reasons.Clear(); }
                // A single item's multiple actions form one candidate, even when several rules match.
                var candidates = matches.Select(m => m.candidate).Distinct().ToList();
                var reasons = matches.SelectMany(m => m.reasons).Distinct().ToList();
                if (direct.Count == 0)
                {
                    List<Candidate> unknown = nearby.Where(c => !candidates.Contains(c) && !c.weak && (c.rules.Count == 0 || c.reasons.Any(r => r.StartsWith("UnsupportedAction:", StringComparison.Ordinal)))).Distinct().ToList();
                    if (unknown.Count > 0 && candidates.Count > 0) { reasons.Add("UnknownItemCompetition"); candidates.AddRange(unknown); }
                }
                if (candidates.Count > 1) reasons.Add("CompetingItems");
                bool supported = candidates.Count == 1 && reasons.Count == 0;
                var effect = new AnalysisEffectChange { channel = change.channel, playerIndex = change.player, previousValue = change.before, value = change.after,
                    beforeTime = change.start, afterTime = change.time, epoch = change.epoch, cumulativeDelta = change.after - change.before,
                    matchesTheory = matches.Count > 0, attribution = supported ? direct.Count > 0 ? "Certain" : "Likely" : "Ambiguous", reasons = reasons,
                    candidateGroupIds = candidates.Select(c => c.row.attributionGroupId).ToList() };
                if (effect.reasons.Count == 0 && !supported) effect.reasons.Add(nearby.Count > 0 ? "ObservedWithoutMatchingRule" : "NoItemCandidate");
                foreach (Candidate c in candidates.Union(nearby))
                {
                    if (candidates.Contains(c)) c.recipients.Add(change.player);
                    AnalysisEffectChange stored = StoreEffect(effect, c);
                    if (!c.effects.Contains(stored)) c.effects.Add(stored);
                }
                foreach (Match match in matches.Where(m => !m.rule.continuous).GroupBy(m => m.candidate.row.attributionGroupId + ":" + m.rule.source + ":" + m.rule.channel).Select(g => g.First()))
                {
                    string budgetKey = change.player + ":" + match.rule.source + ":" + match.rule.channel;
                    float spent; match.candidate.spent.TryGetValue(budgetKey, out spent);
                    match.candidate.spent[budgetKey] = spent + Math.Abs(change.after - change.before);
                }
                if (candidates.Count == 0 && nearby.Count == 0 && change.channel != "RegularStamina") StoreEffect(effect, null);
            }
            _changes.RemoveAll(c => c.time <= deadline);
        }

        private AnalysisEffectChange StoreEffect(AnalysisEffectChange effect, Candidate candidate)
        {
            string key = (candidate?.row.attributionGroupId ?? 0) + ":" + effect.playerIndex + ":" + effect.channel + ":" + effect.attribution + ":" + string.Join(",", effect.reasons) + ":" + string.Join(",", effect.candidateGroupIds);
            AnalysisEffectChange previous;
            if (_lastEffects.TryGetValue(key, out previous) && previous.epoch == effect.epoch && effect.beforeTime >= previous.afterTime && effect.beforeTime - previous.afterTime <= .6f &&
                Math.Sign(previous.cumulativeDelta) == Math.Sign(effect.cumulativeDelta) && effect.afterTime - previous.beforeTime <= 10f && !effect.channel.StartsWith("Affliction:", StringComparison.Ordinal))
            { previous.afterTime = effect.afterTime; previous.value = effect.value; previous.cumulativeDelta += effect.cumulativeDelta; return previous; }
            effect.effectId = _result.effects.Count + 1;
            // Clone per owner: later candidate downgrades must not mutate another item's evidence.
            var stored = new AnalysisEffectChange { effectId = effect.effectId, epoch = effect.epoch, channel = effect.channel, playerIndex = effect.playerIndex,
                previousValue = effect.previousValue, value = effect.value, beforeTime = effect.beforeTime, afterTime = effect.afterTime,
                cumulativeDelta = effect.cumulativeDelta, matchesTheory = effect.matchesTheory, attribution = effect.attribution,
                reasons = new List<string>(effect.reasons), candidateGroupIds = new List<int>(effect.candidateGroupIds) };
            _result.effects.Add(stored); _lastEffects[key] = stored;
            if (_lastEffects.Count > 4096) _lastEffects.Clear();
            return stored;
        }

        private bool RecipientMatches(Match match, Change change)
        {
            Candidate c = match.candidate; AnalysisEffectRule r = match.rule;
            bool area = r.scope == "Area" || r.scope == "PlayerArea";
            if (r.scope == "Actor") return c.actor >= 0 && c.actor == change.player;
            if (!area && c.target >= 0) return c.target == change.player;
            if (!area && c.actor == change.player) return true;
            PlayerTelemetry actor;
            if (c.actor < 0 || !change.players.TryGetValue(c.actor, out actor) || !RunAnalysisEngine.ValidPosition(actor) || !RunAnalysisEngine.ValidPosition(change.afterPlayer))
            { match.reasons.Add("SpatialEvidenceUnavailable"); return true; }
            float distance = WorldDistance(actor, change.afterPlayer);
            if (area)
            {
                if (r.ignoreCaster && c.actor == change.player) return false;
                if (r.radius.HasValue && distance > r.radius.Value + 1f) return false;
                if (r.scope == "Area") match.reasons.Add("ItemOriginNotRecorded");
                if (r.radius.HasValue && distance > r.radius.Value) match.reasons.Add("SpatialBoundaryUncertain");
                return true;
            }
            // Interaction.distance is 2 world units. One unit allows body/camera offset, not meters.
            return distance <= 3f;
        }

        private void CheckAmount(Match match, Change change)
        {
            AnalysisEffectRule rule = match.rule;
            if (!rule.amount.HasValue) { match.reasons.Add("AmountNotRecorded"); return; }
            float observed = Math.Abs(change.after - change.before);
            float expected = Math.Abs(rule.amount.Value);
            if (rule.continuous) expected *= Math.Max(.5f, change.time - change.start);
            else
            {
                float spent; match.candidate.spent.TryGetValue(change.player + ":" + rule.source + ":" + rule.channel, out spent);
                expected = Math.Max(0, expected - spent);
            }
            if (rule.direction < 0) expected = Math.Min(expected, Math.Max(0, change.before));
            else if (rule.channel == "ExtraStamina")
            {
                // Unknown petrify prevents a precise cap prediction, not a measured positive net gain.
                float cap = 1 - (change.beforePlayer.petrifyAmount ?? 0) / 100f;
                expected = Math.Min(expected, Math.Max(0, cap - change.before));
            }
            if (rule.budget.HasValue && !rule.continuous)
            {
                float earlier = match.candidate.rules.Where(r => r.budgetGroup == rule.budgetGroup && r.priority < rule.priority).Sum(r => Status(change.beforePlayer, r.channel));
                expected = Math.Min(expected, Math.Max(0, rule.budget.Value - earlier));
            }
            if (rule.budget.HasValue && rule.continuous && match.candidate.rules.Any(r => r.budgetGroup == rule.budgetGroup && r.priority < rule.priority && Status(change.beforePlayer, r.channel) >= .025f))
                match.reasons.Add("HealingPriorityMismatch");
            if (rule.channel.StartsWith("Affliction:", StringComparison.Ordinal)) expected = 1;
            if (observed > expected + .026f) match.reasons.Add("AmountOrBudgetMismatch");
            // Smaller net gains are retained: climbing, caps and simultaneous effects can consume them.
        }

        private float Status(PlayerTelemetry player, string channel)
        {
            int i = Array.IndexOf(_result.statusTypeOrder, channel);
            return i >= 0 && player.statuses != null && i < player.statuses.Length && Finite(player.statuses[i]) ? Math.Max(0, player.statuses[i]) : 0;
        }
        private static bool TriggerMatches(Candidate candidate, AnalysisEffectRule rule)
        {
            if (candidate.passive || string.IsNullOrEmpty(rule.trigger)) return true;
            string[] triggers = rule.trigger.Split(',');
            return candidate.rows.Any(row =>
                row.kind == "ItemConsumed" && (triggers.Contains("OnConsumed") || triggers.Contains("OnCastFinished")) ||
                row.kind == "ItemFedToPlayer" && (triggers.Contains("OnConsumed") || triggers.Contains("OnCastFinished")) ||
                row.kind == "ItemPrimaryCastFinished" && triggers.Contains("OnCastFinished") ||
                row.kind == "ItemSecondaryCastFinished" && triggers.Contains("OnSecondaryCastFinished") ||
                (row.kind == "ResourceChanged" || row.kind == "ItemRemovedObserved") && triggers.Any(t => t == "OnConsumed" || t == "OnCastFinished" || t == "OnSecondaryCastFinished"));
        }
        private static bool Natural(Change change)
        {
            return change.channel == "RegularStamina" || change.after < change.before && new[] { "Poison", "Drowsy", "Hot", "Cold", "Spores", "Thorns" }.Contains(change.channel);
        }
        private static float End(Candidate c)
        {
            return c.passive ? c.end : Math.Max(c.lateEnd, c.time + Math.Max(4f, c.rules.Select(r => r.delay + r.duration).DefaultIfEmpty(0).Max()));
        }

        internal void Interrupt(int player, float time)
        {
            _barriers[player] = time; _previous.Remove(player);
            foreach (Candidate c in _candidates.Where(c => c.actor == player || c.target == player))
            { c.reasons.Add("ObservationInterrupted"); if (c.passive) c.end = time; }
            foreach (var key in _passive.Where(p => p.Value.actor == player).Select(p => p.Key).ToArray()) _passive.Remove(key);
        }

        private void FinishCandidate(Candidate c, string reason)
        {
            if (reason != null) c.reasons.Add(reason);
            bool conflictingRecipients = c.target < 0 && c.recipients.Count > 1 && !c.rules.Any(r => r.scope == "Area" || r.scope == "PlayerArea");
            bool interrupted = c.reasons.Contains("ObservationInterrupted") || c.reasons.Contains("MovedOrTransferred") || c.reasons.Contains("IncompleteWindow") || c.reasons.Contains("ConflictingGuid") || c.reasons.Contains("TrackingLimitReached");
            if (conflictingRecipients || interrupted)
                foreach (var effect in c.effects.Where(e => e.attribution != "Certain")) { effect.attribution = "Ambiguous"; effect.reasons.Add(conflictingRecipients ? "CompetingRecipients" : "ObservationInterrupted"); }
            int inferredTarget = c.target >= 0 ? c.target : c.recipients.Count == 1 ? c.recipients.First() : -1;
            foreach (var row in c.rows)
            {
                row.rules = c.rules; row.endTime = c.passive ? (Finite(c.end) ? c.end : _lastTime) : c.lastEvidence;
                row.observedEffects = c.effects;
                row.attributionReasons = c.reasons.Concat(c.effects.SelectMany(e => e.reasons)).Distinct().ToList();
                row.attribution = c.effects.Any(e => e.attribution == "Certain" && e.candidateGroupIds.Contains(c.row.attributionGroupId)) ? "Certain" :
                    c.effects.Any(e => e.attribution == "Likely" && e.candidateGroupIds.Contains(c.row.attributionGroupId)) ? "Likely" : "Ambiguous";
                if (row.attributionReasons.Count == 0) row.attributionReasons.Add(c.rules.Count == 0 ? "NoObservableEffectRule" : c.effects.Count == 0 ? "NoObservedChange" : "CompatibleObservedChange");
                row.attributionReason = row.attributionReasons[0];
                if (row.targetPlayerIndex < 0 && inferredTarget >= 0) row.targetPlayerIndex = inferredTarget;
                row.possibleHelp = c.actor >= 0 && c.effects.Any(e => e.playerIndex != c.actor && e.attribution != "Ambiguous" && Helpful(e));
                row.possibleRescue = row.possibleHelp && c.effects.Any(recovery => recovery.playerIndex != c.actor && recovery.channel == "Unconscious" && recovery.cumulativeDelta < 0 &&
                    !recovery.reasons.Contains("DeathRevivalOrDiscontinuity") && c.effects.Any(healing => healing.playerIndex == recovery.playerIndex && healing.attribution != "Ambiguous" &&
                        healing.cumulativeDelta < 0 && Helpful(healing) && healing.afterTime <= recovery.afterTime && recovery.afterTime - healing.afterTime <= 2));
            }
            if (!c.passive) ItemUseRules.Classify(c.rows, c.reasons);
            _candidates.Remove(c);
            foreach (var key in _passive.Where(p => ReferenceEquals(p.Value, c)).Select(p => p.Key).ToArray()) _passive.Remove(key);
        }

        internal void Reset()
        {
            SettleChanges(float.MaxValue, true);
            foreach (Candidate candidate in _candidates.ToArray()) FinishCandidate(candidate, "ObservationInterrupted");
            _changes.Clear(); _previous.Clear(); _passive.Clear(); _lastEffects.Clear(); _barriers.Clear(); _transformed.Clear(); _trackingLimited = false; _environmentTime = float.NegativeInfinity; _lastTime = float.NaN;
        }
        internal void Finish()
        {
            SettleChanges(float.MaxValue, true);
            foreach (Candidate candidate in _candidates.ToArray()) FinishCandidate(candidate, !candidate.passive && _lastTime < End(candidate) + Lag ? "IncompleteWindow" : null);
        }

        private static bool Finite(float value) { return RunAnalysisEngine.Finite(value); }
        private static bool Helpful(AnalysisEffectChange effect)
        {
            if (effect.channel == "ExtraStamina" && effect.cumulativeDelta > 0) return true;
            if (effect.cumulativeDelta < 0) return new[] { "Injury", "Hunger", "Poison", "Cold", "Hot", "Drowsy", "Spores", "Petrify", "Curse", "Web", "Thorns", "Arrow" }.Contains(effect.channel);
            return effect.channel.StartsWith("Affliction:", StringComparison.Ordinal) && new[] { "InfiniteStamina", "FasterBoi", "Invincibility", "LowGravity", "NoHunger", "Sunscreen", "ClimbingChalk", "HealAll", "DoubleJumpAmulet", "BingBongShield" }.Contains(effect.channel.Substring(11));
        }
        private static float WorldDistance(PlayerTelemetry a, PlayerTelemetry b) { double x = a.positionX - b.positionX, y = a.positionY - b.positionY, z = a.positionZ - b.positionZ; return (float)Math.Sqrt(x * x + y * y + z * z); }
        private sealed class Frame { internal float time; internal PlayerTelemetry value; }
        private sealed class Change
        {
            internal string channel; internal float before, after, start, time; internal int player, epoch;
            internal PlayerTelemetry beforePlayer, afterPlayer; internal Dictionary<int, PlayerTelemetry> players;
        }
        private sealed class Candidate
        {
            internal AnalysisItemObservation row; internal List<AnalysisItemObservation> rows = new List<AnalysisItemObservation>();
            internal List<AnalysisEffectRule> rules; internal List<string> reasons = new List<string>();
            internal List<AnalysisEffectChange> effects = new List<AnalysisEffectChange>(); internal HashSet<int> recipients = new HashSet<int>();
            internal Dictionary<string, float> spent = new Dictionary<string, float>();
            internal List<StatsEvent> proofs = new List<StatsEvent>();
            internal Dictionary<string, float> endedAt = new Dictionary<string, float>();
            internal float lateEnd;
            internal int actor, target; internal float time, lastEvidence, end = float.PositiveInfinity; internal bool weak, explicitTarget, passive;
        }
        private sealed class Match { internal Candidate candidate; internal AnalysisEffectRule rule; internal List<string> reasons = new List<string>(); }
    }
}
