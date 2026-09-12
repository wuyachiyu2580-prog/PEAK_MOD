using System;
using System.Collections.Generic;
using System.Linq;

namespace StateKeeper
{
    // Store state intervals, not every frame. Events are evidence, never a death counter.
    internal sealed class LifecycleAnalysis
    {
        private sealed class State
        {
            internal EvidenceReference evidence;
            internal int player, epoch, count;
            internal float start, end, second = float.NaN;
            internal string kind;
            internal bool entered;
        }
        private readonly AnalysisResult _result;
        private readonly Dictionary<int, State> _current = new Dictionary<int, State>();
        private readonly List<State> _states = new List<State>();
        private readonly Dictionary<string, LifecycleObservation> _events = new Dictionary<string, LifecycleObservation>();
        private readonly List<Tuple<int, int, float>> _revivals = new List<Tuple<int, int, float>>();
        internal LifecycleAnalysis(AnalysisResult result) { _result = result; }
        internal void Interrupt(int player = -1)
        {
            if (player < 0) _current.Clear(); else _current.Remove(player);
        }
        internal void Sample(float time, PlayerTelemetry p, EvidenceReference evidence)
        {
            string kind = p.dead ? "Death" : p.passedOut || p.fullyPassedOut ? "PassedOut" : "Alive";
            State old;
            bool continuous = _current.TryGetValue(p.playerIndex, out old) && old.epoch == evidence.epoch && time > old.end && time - old.end <= RunAnalysisEngine.GapLimit;
            if (!continuous || old.kind != kind)
            {
                var next = new State { evidence = evidence, player = p.playerIndex, epoch = evidence.epoch, start = time, end = time, count = 1, kind = kind,
                    entered = continuous && (old.kind == "Alive" || kind == "Death" && old.kind == "PassedOut") };
                _current[p.playerIndex] = next; _states.Add(next);
                if (continuous && old.kind != "Alive" && kind == "Alive")
                    _result.timeline.Add(new ReportEvent { evidence = evidence, playerIndex = p.playerIndex, time = time, endTime = time, kind = "RecoveryObserved", confidence = "Likely" });
            }
            else { old.end = time; old.count++; if (old.count == 2) old.second = time; }
        }
        internal void Event(StatsEvent e, EvidenceReference evidence)
        {
            if (e.type == "PlayerRevivedObserved" && evidence.epoch >= 0)
            { _revivals.Add(Tuple.Create(e.subjectPlayerIndex, evidence.epoch, e.time)); Interrupt(e.subjectPlayerIndex); }
            if (e.type != "PlayerDied" && e.type != "PlayerPassedOut") return;
            string key = evidence.epoch + ":" + e.subjectPlayerIndex + ":" + e.type + ":" + e.time.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
            LifecycleObservation previous;
            if (evidence.epoch >= 0 && _events.TryGetValue(key, out previous)) { previous.duplicateCount++; return; }
            var value = new LifecycleObservation { evidence = evidence, playerIndex = e.subjectPlayerIndex, time = e.time,
                kind = e.type == "PlayerDied" ? "Death" : "PassedOut" };
            _result.lifecycle.Add(value); _events[key] = value;
        }
        internal void Finish()
        {
            foreach (AnalysisPlayer player in _result.players)
            {
                State creditedDeath = null, creditedPass = null;
                var observations = _result.lifecycle.Where(e => e.playerIndex == player.playerIndex).OrderBy(e => e.evidence.epoch).ThenBy(e => e.time);
                var states = _states.Where(s => s.player == player.playerIndex).ToList();
                foreach (LifecycleObservation e in observations)
                {
                    State state = e.evidence.epoch < 0 ? null : states.Where(s => s.epoch == e.evidence.epoch && s.kind == e.kind &&
                        ((s.entered && Math.Abs(s.start - e.time) <= ReportThresholds.DeathWindow) ||
                        (s.count >= 2 && s.start >= e.time && s.second <= e.time + ReportThresholds.DeathWindow)))
                        .OrderBy(s => e.time >= s.start && e.time <= s.end ? 0 : Math.Min(Math.Abs(s.start - e.time), Math.Abs(s.end - e.time)))
                        .ThenBy(s => Math.Abs(s.start - e.time)).FirstOrDefault();
                    if (e.kind == "Death") player.rawDeathCount += 1 + e.duplicateCount;
                    else player.rawPassedOutCount += 1 + e.duplicateCount;
                    State credited = e.kind == "Death" ? creditedDeath : creditedPass;
                    bool rearmed = credited == null || credited.epoch != e.evidence.epoch || states.Any(s => s.epoch == e.evidence.epoch && s.kind == "Alive" && s.count >= 2 && s.start > credited.end && s.second <= e.time) ||
                        _revivals.Any(r => r.Item1 == e.playerIndex && r.Item2 == e.evidence.epoch && r.Item3 > credited.end && r.Item3 <= e.time);
                    if (state != null && state != credited && rearmed)
                    {
                        e.verdict = "Confirmed"; e.reason = "StateCorroborated";
                        if (e.kind == "Death") { player.deathCount++; player.deathTimes.Add(e.time); creditedDeath = state; }
                        else { player.passedOutCount++; player.passedOutTimes.Add(e.time); creditedPass = state; }
                        _result.timeline.Add(new ReportEvent { evidence = e.evidence, playerIndex = e.playerIndex, time = e.time, endTime = e.time, kind = e.kind, confidence = "Certain" });
                    }
                    else if (credited != null && credited.epoch == e.evidence.epoch && (state == credited || !rearmed))
                    { e.verdict = "Duplicate"; e.reason = "AlreadyInState"; }
                    else { e.reason = e.evidence.epoch < 0 ? "ClockAmbiguous" : "StateNotCorroborated"; }
                    if (e.kind == "Death")
                    {
                        player.duplicateDeathCount += e.duplicateCount + (e.verdict == "Duplicate" ? 1 : 0);
                        if (e.verdict == "Unconfirmed") player.unconfirmedDeathCount++;
                    }
                }
            }
            _result.overview.deathCount = _result.players.Sum(p => p.deathCount);
            _result.overview.passedOutCount = _result.players.Sum(p => p.passedOutCount);
            foreach (State state in _states)
            {
                _result.lifecycleIntervals.Add(new LifecycleInterval { evidence = state.evidence, playerIndex = state.player, start = state.start, end = state.end, state = state.kind, entered = state.entered, sampleCount = state.count });
                if (state.entered && state.kind != "Alive" && !_result.lifecycle.Any(e => e.playerIndex == state.player && e.kind == state.kind && e.evidence.epoch == state.epoch && e.verdict == "Confirmed" && Math.Abs(e.time - state.start) <= 2))
                    _result.timeline.Add(new ReportEvent { evidence = state.evidence, playerIndex = state.player, time = state.start, endTime = state.start, kind = state.kind + "StateObserved", confidence = "Likely" });
            }
        }
    }
}
