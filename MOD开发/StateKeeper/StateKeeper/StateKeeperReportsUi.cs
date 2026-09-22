using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace StateKeeper
{
    internal static partial class StateKeeperUi
    {
        private static TMP_InputField _searchInput, _timeFrom, _timeTo;
        private static TextMeshProUGUI _searchLabel, _rangeLabel, _renameMessage;
        private static StateKeeperButton _applyRange, _resetRange;
        private static float? _rangeStart, _rangeEnd;
        private static int? _evidenceEpoch;
        private static string _itemQuery = "";
        private static float _searchDue;
        private static bool _searchDirty;
        private static int _timelinePage, _qualityPage, _qualityMode, _relationIndex = -1;
        private static int _itemMode, _contributionPage;
        private static ReportEvent _focusedEvent;
        private static float? _returnStart, _returnEnd;
        private static int? _returnEpoch;
        private static int _returnSegment;
        private static GameObject _renameFocus;
        private static readonly List<Selectable> _modalDisabled = new List<Selectable>();
        private static readonly Dictionary<int, string[]> _searchTerms = new Dictionary<int, string[]>();

        private static void UpdateRenameMessage(bool failed)
        {
            if (_renameMessage == null) return;
            _renameMessage.text = (_nameInput.text?.Length ?? 0) + " / 40" + (failed ? "\n" + Text("Save failed. Name has not been confirmed.", "保存失败，名称尚未确认。") : "");
        }
        private static void RefreshRenamedEntry(string id)
        {
            RunIndexEntry entry = AllHistoryEntries().FirstOrDefault(e => e.runId == id);
            if (entry == null) return;
            if (_selectedEntry?.runId == id) { _selectedEntry = entry; _detailsTitle.text = RunName(entry); }
            if (_compareEntries.ContainsKey(id)) _compareEntries[id] = entry;
            if (_comparePage != null && _comparePage.gameObject.activeInHierarchy) RenderComparison();
        }
        private static string RunName(RunIndexEntry entry) => string.IsNullOrWhiteSpace(entry.customName) ? Text("UNTITLED EXPEDITION", "未命名远征") : entry.customName;
        private static void CreateReportControls(Transform parent)
        {
            _rangeLabel = CreateText(parent, "TimeRangeLabel", 18, TextAlignmentOptions.Left);
            Anchor(_rangeLabel.rectTransform, .5f, 1, .5f, 1, -500, -238, 180, 30);
            _timeFrom = CreateInput(parent); _timeTo = CreateInput(parent);
            _timeFrom.characterLimit = _timeTo.characterLimit = 12;
            Anchor(_timeFrom.GetComponent<RectTransform>(), .5f, 1, .5f, 1, -300, -238, 140, 32);
            Anchor(_timeTo.GetComponent<RectTransform>(), .5f, 1, .5f, 1, -130, -238, 140, 32);
            _applyRange = CreateButton(parent, "ApplyRange", ""); Anchor(_applyRange.RectTransform, .5f, 1, .5f, 1, 65, -238, 160, 34);
            _resetRange = CreateButton(parent, "ResetRange", ""); Anchor(_resetRange.RectTransform, .5f, 1, .5f, 1, 255, -238, 160, 34);
            _applyRange.AddListener(() =>
            {
                if (!ParseTime(_timeFrom.text, out float from) || !ParseTime(_timeTo.text, out float to) || from > to)
                { _rangeLabel.text = Text("Invalid time range", "时间范围无效"); return; }
                _rangeStart = from; _rangeEnd = to; _focusedEvent = null; _timelinePage = 0; _itemPage = 0; RenderDetails();
            });
            _resetRange.AddListener(() => { _rangeStart = _rangeEnd = null; _evidenceEpoch = null; _focusedEvent = null; RenderDetails(); });
            _searchLabel = CreateText(parent, "ItemSearchLabel", 18, TextAlignmentOptions.Left);
            Anchor(_searchLabel.rectTransform, .5f, 1, .5f, 1, -500, -282, 180, 30);
            _searchInput = CreateInput(parent); _searchInput.characterLimit = 100;
            Anchor(_searchInput.GetComponent<RectTransform>(), .5f, 1, .5f, 1, 0, -282, 700, 34);
            _searchInput.onValueChanged.AddListener(_ => { _searchDue = Time.unscaledTime + .2f; _searchDirty = true; });
            RefreshReportLanguage();
        }
        private static bool ParseTime(string text, out float seconds)
        {
            seconds = 0;
            string[] parts = (text ?? "").Trim().Split(':');
            if (parts.Length > 3 || parts.Length == 0) return false;
            foreach (string part in parts)
            {
                if (!float.TryParse(part, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float value) || !RunAnalysisEngine.Finite(value) || value < 0) return false;
                seconds = seconds * 60 + value;
            }
            return RunAnalysisEngine.Finite(seconds);
        }
        private static void PollReportInput()
        {
            if (_searchDirty && _searchInput != null && Time.unscaledTime >= _searchDue && string.IsNullOrEmpty(Input.compositionString))
            { _searchDirty = false; _itemQuery = _searchInput.text; _itemPage = 0; _contributionPage = 0; if (_detailsView == 3) RenderDetails(); }
        }
        private static void RefreshReportLanguage()
        {
            _searchTerms.Clear();
            if (_searchLabel != null) _searchLabel.text = Text("Item search", "搜索物品");
            if (_rangeLabel != null) _rangeLabel.text = Text("Time range", "时间范围");
            _applyRange?.SetCompactText(Text("APPLY", "应用范围")); _resetRange?.SetCompactText(Text("RESET", "重置范围"));
            RefreshCompareLabel();
            _compareBack?.SetCompactText(Text("BACK", "返回"));
            if (_comparePage != null && _comparePage.gameObject.activeInHierarchy) RenderComparison();
        }
        private static void RefreshReportControls()
        {
            bool items = _detailsView == 3;
            if (_searchInput != null) { _searchInput.gameObject.SetActive(items); _searchLabel.gameObject.SetActive(items); }
            if (_confidenceFilterText != null) _confidenceFilterText.GetComponentInParent<Button>(true)?.gameObject.SetActive(items);
            if (_resourceFilterText != null) _resourceFilterText.GetComponentInParent<Button>(true)?.gameObject.SetActive(items);
            if (_displayedAnalysis != null && _timeFrom != null)
            {
                GetTimeRange(out float from, out float to);
                if (!_timeFrom.isFocused) _timeFrom.SetTextWithoutNotify(FormatDuration(from));
                if (!_timeTo.isFocused) _timeTo.SetTextWithoutNotify(FormatDuration(to));
            }
        }
        private static bool EpochMatches(int epoch) => !_evidenceEpoch.HasValue || epoch == _evidenceEpoch.Value;
        private static IEnumerable<float> SelectedJumps(AnalysisPlayer player) => player.jumpTimes.Where((t, i) => InSelectedRange(t) && EpochMatches(i < player.jumpEvidence.Count ? player.jumpEvidence[i].epoch : -1));
        private static bool SearchItem(AnalysisItemObservation item)
        {
            if (string.IsNullOrWhiteSpace(_itemQuery)) return true;
            if (!_searchTerms.TryGetValue(item.observationId, out string[] fields))
            {
                ItemDefinition definition = RunAnalysisEngine.Definition(_displayedAnalysis.definitions, item.itemId, item.prefabName);
                fields = new[] { item.itemName, ItemDisplayName(item.itemName), item.prefabName, string.Join(" ", definition?.itemTags ?? new List<string>()),
                    string.Join(" ", definition?.effectHints.Select(h => h.type + " " + h.target + " " + StatusName(h.target)) ?? Enumerable.Empty<string>()),
                    string.Join(" ", item.rules.Select(r => r.channel + " " + StatusName(r.channel))), string.Join(" ", item.observedEffects.Select(r => r.channel + " " + StatusName(r.channel))) };
                _searchTerms[item.observationId] = fields;
            }
            return ReportComparison.Search(_itemQuery, fields);
        }
        private static string LifecycleSummary(AnalysisPlayer p)
        {
            var facts = _displayedAnalysis.lifecycle.Where(e => e.playerIndex == p.playerIndex && e.kind == "Death" && InSelectedRange(e.time) && EpochMatches(e.evidence.epoch)).ToList();
            return DisplayPlayerName(p) + " | " + Text("Deaths confirmed / observed / duplicate / unknown: ", "死亡 确认／观察／重复／未确认：") +
                facts.Count(e => e.verdict == "Confirmed") + " / " + facts.Sum(e => 1 + e.duplicateCount) + " / " + facts.Sum(e => e.duplicateCount + (e.verdict == "Duplicate" ? 1 : 0)) + " / " + facts.Count(e => e.verdict == "Unconfirmed");
        }
        private static string EventName(string kind)
        {
            if (kind == "DeathStateObserved") return Text("Observed death state", "死亡状态观察");
            if (kind == "PassedOutStateObserved") return Text("Observed unconscious state", "昏迷状态观察");
            if (kind == "AliveStateObserved") return Text("Observed alive state", "存活状态观察");
            if (kind == "RawDeathObserved") return Text("Raw death observation", "原始死亡观察");
            if (kind == "RawPassedOutObserved") return Text("Raw unconscious observation", "原始昏迷观察");
            string[] en = { "Death", "PassedOut", "RecoveryObserved", "ProgressReached", "LowStamina", "LowCapacity", "StatusRise", "Isolation", "SharedDanger", "Together", "PossibleHelp", "ItemConsumed", "PlayerRescuePulled", "PlayerRevivedObserved", "PlayerLifeSaved", "PlayerFriendHealed", "TimeSelection" };
            string[] zh = { "确认死亡", "确认昏迷", "恢复活动观察", "到达进度点", "低体力", "体力容量受限", "状态快速恶化", "单独活动", "多人同时危险", "近距离共同活动", "可能帮助", "明确消耗", "拉回启动", "复活观察（施救者未知）", "急救越过阈值", "直接治疗", "所选时刻" };
            int i = Array.IndexOf(en, kind); return i < 0 ? ItemKindName(kind) : Text(en[i], zh[i]);
        }
        private static string SourceText(EvidenceReference e)
        { return e == null ? Text("Source not located", "未定位原始证据") : Text("Clock interval ", "时钟区间 ") + e.epoch + " | chunk " + e.chunk + " | " + e.stream + "[" + e.index + "]"; }
        private static bool EventMatches(ReportEvent e)
        {
            var selected = GetSelectedPlayer();
            return InSelectedRange(e.time) && EpochMatches(e.evidence?.epoch ?? -1) && (selected == null || e.playerIndex == selected.playerIndex || e.otherPlayerIndex == selected.playerIndex || (e.supportingPlayers?.Contains(selected.playerIndex) ?? false));
        }
        private static void AddEventRow(ReportEvent e)
        {
            RectTransform row = DashboardRow("ReportEvent", 76);
            var label = CreateText(row, "Summary", 18, TextAlignmentOptions.Left);
            Stretch(label.rectTransform); label.rectTransform.offsetMax = new Vector2(-145, 0); label.raycastTarget = false;
            label.text = FormatDuration(e.time) + (e.endTime > e.time ? " - " + FormatDuration(e.endTime) : "") + " | " + EventName(e.kind) + " | " + ConfidenceText(e.confidence) +
                "\n" + PlayerName(e.playerIndex) + (e.otherPlayerIndex >= 0 ? " -> " + PlayerName(e.otherPlayerIndex) : "") + (string.IsNullOrEmpty(e.detail) ? "" : " | " + (e.itemObservationId > 0 ? ItemDisplayName(e.detail) : StatusName(e.detail)));
            if (e.itemObservationId > 0 && _displayedAnalysis.items.Any(i => i.observationId == e.itemObservationId && i.actorInferred)) label.text += Text(" (source inferred)", "（来源为推断）");
            StateKeeperButton evidence = CreateButton(row, "OpenEvidence", ""); evidence.SetCompactText(Text("EVIDENCE", "证据"));
            Anchor(evidence.RectTransform, 1, .5f, 1, .5f, 0, 0, 125, 38); evidence.AddListener(() => OpenEvent(e));
        }
        private static void Pager(int page, int count, Action<int> change)
        {
            int pages = Math.Max(1, (count + 49) / 50);
            RectTransform row = DashboardRow("ReportPager", 40);
            var label = CreateText(row, "Page", 18, TextAlignmentOptions.Center); Stretch(label.rectTransform); label.text = (page + 1) + " / " + pages; label.raycastTarget = false;
            var previous = CreateButton(row, "PreviousPage", ""); previous.SetCompactText(Text("PREVIOUS", "上一页")); Anchor(previous.RectTransform, 0, .5f, 0, .5f, 0, 0, 145, 38);
            previous.SetInteractable(page > 0); previous.AddListener(() => change(page - 1));
            var next = CreateButton(row, "NextPage", ""); next.SetCompactText(Text("NEXT", "下一页")); Anchor(next.RectTransform, 1, .5f, 1, .5f, 0, 0, 145, 38);
            next.SetInteractable(page + 1 < pages); next.AddListener(() => change(page + 1));
        }
        private static void RenderTimeline()
        {
            var entries = _displayedAnalysis.timeline.Where(EventMatches).ToList();
            _timelinePage = Math.Min(_timelinePage, Math.Max(0, (entries.Count - 1) / 50));
            AddDashboardText(Text("EVENT TIMELINE", "事件时间线") + " | " + entries.Count, 24);
            Pager(_timelinePage, entries.Count, page => { _timelinePage = page; RenderDetails(); _dashboardScroll.verticalNormalizedPosition = 1; });
            foreach (var e in entries.Skip(_timelinePage * 50).Take(50)) AddEventRow(e);
            if (entries.Count == 0) AddDashboardText(Text("No matching evidence", "没有符合条件的证据"), 20);
        }
        private static void AddReportHighlights()
        {
            AddDashboardText(Text("KEY OBSERVATIONS", "关键观察"), 24);
            foreach (var e in _displayedAnalysis.timeline.Where(EventMatches).Where(e => e.kind == "Death" || e.kind == "SharedDanger" || e.kind == "PossibleHelp").Take(8)) AddEventRow(e);
        }
        private static void AddPlayerReport(AnalysisPlayer player)
        {
            GetTimeRange(out float start, out float end);
            var episodes = _displayedAnalysis.episodes.Where(e => e.playerIndex == player.playerIndex && EpochMatches(e.evidence?.epoch ?? -1) && e.end >= start && e.start <= end).ToList();
            var points = player.staminaSeries.Where(p => p.endTime >= start && p.time <= end && EpochMatches(p.epoch)).ToList();
            float Weight(AnalysisSeriesPoint p) => p.endTime > p.time ? Mathf.Clamp01((Math.Min(end, p.endTime) - Math.Max(start, p.time)) / (p.endTime - p.time)) : 1;
            float alive = points.Sum(p => p.validSeconds * Weight(p)), observed = points.Sum(p => p.observedSeconds * Weight(p));
            float EpisodeTime(string kind) => episodes.Where(e => e.kind == kind).Sum(e => e.end > e.start ? e.activeSeconds * Math.Max(0, Math.Min(end, e.end) - Math.Max(start, e.start)) / (e.end - e.start) : 0);
            AddDashboardText(LifecycleSummary(player), 20);
            AddDashboardText(Text("Range observed / alive (binned): ", "范围有效观察／存活（分桶近似）：") + FormatDuration(observed) + " / " + FormatDuration(alive) +
                "\n" + Text("Passed out / jumps in range: ", "范围内确认昏迷／跳跃：") + _displayedAnalysis.lifecycle.Count(e => e.playerIndex == player.playerIndex && e.kind == "PassedOut" && e.verdict == "Confirmed" && InSelectedRange(e.time) && EpochMatches(e.evidence.epoch)) + " / " + SelectedJumps(player).Count(), 18);
            AddDashboardText(Text("Risk windows in range: ", "范围内危险窗口：") + episodes.Count(e => e.kind != "Together") +
                "\n" + Text("Low stamina / capacity / isolation (approx.): ", "低体力／容量受限／单独活动（约）：") + FormatDuration(EpisodeTime("LowStamina")) + " / " + FormatDuration(EpisodeTime("LowCapacity")) + " / " + FormatDuration(EpisodeTime("Isolation")), 18);
            AddDashboardText(Text("Movement shares (binned): ", "活动占比（分桶近似）：") + string.Join(" | ", ReportAnalysis.MovementTypes.Select((key, i) => ActivityName(key) + " " +
                (alive > 0 ? Percent(points.Sum(p => p.movementSeconds == null ? 0 : p.movementSeconds[i] * Weight(p)) / alive) : Text("unknown", "未知")))), 18);
            AddDashboardText(Text("Mean status occupancy (binned): ", "平均状态占用（分桶近似）：") + string.Join(" | ", _displayedAnalysis.statusTypeOrder.Select((key, i) => new { key, value = points.Sum(p => p.statusIntegrals == null ? 0 : p.statusIntegrals[i] * Weight(p)) }).Where(p => p.value > 0).Select(p => StatusName(p.key) + " " + (alive > 0 ? Percent(p.value / alive) : Text("unknown", "未知")))), 18);
            foreach (var e in _displayedAnalysis.timeline.Where(EventMatches).Where(e => e.playerIndex == player.playerIndex && (e.kind == "LowStamina" || e.kind == "LowCapacity" || e.kind == "Isolation")).Take(5)) AddEventRow(e);
        }
        private static string ActivityName(string key)
        {
            string[] en = { "Unconscious", "Rope", "Vine", "Climbing", "Gliding", "Sprinting", "Grounded", "Airborne" };
            string[] zh = { "昏迷", "攀绳", "藤蔓", "攀爬", "滑翔", "奔跑", "地面", "空中" };
            int i = Array.IndexOf(en, key); return i < 0 ? key : Text(en[i], zh[i]);
        }
        private static void OpenEvent(ReportEvent e)
        {
            if (_focusedEvent == null) { _returnStart = _rangeStart; _returnEnd = _rangeEnd; _returnEpoch = _evidenceEpoch; _returnSegment = _segmentFilter; }
            _segmentFilter = -1;
            _focusedEvent = e; _rangeStart = Math.Max(0, e.time - 30); _rangeEnd = Math.Min(_displayedAnalysis.overview.durationSeconds, e.time + 15);
            _evidenceEpoch = e.evidence?.epoch; RenderDetails(); _dashboardScroll.verticalNormalizedPosition = 1;
        }
        private static void RenderEventEvidence()
        {
            ReportEvent e = _focusedEvent;
            var row = DashboardRow("EvidenceReturn", 42);
            var back = CreateButton(row, "BackToReport", ""); back.SetCompactText(Text("BACK TO REPORT", "返回报告")); Anchor(back.RectTransform, 0, .5f, 0, .5f, 0, 0, 200, 38);
            back.AddListener(() => { _focusedEvent = null; _rangeStart = _returnStart; _rangeEnd = _returnEnd; _evidenceEpoch = _returnEpoch; _segmentFilter = _returnSegment; RenderDetails(); });
            AddDashboardText(EventName(e.kind) + " | " + FormatDuration(e.time) + " | " + ConfidenceText(e.confidence), 24);
            AddDashboardText(SourceText(e.evidence), 16);
            if (e.supportingPlayers != null) AddDashboardText(string.Join(" | ", e.supportingPlayers.Select(PlayerName)), 18);
            foreach (var reference in e.supportingEvidence ?? Enumerable.Empty<EvidenceReference>())
                if (!ReferenceEquals(reference, e.evidence)) AddDashboardText(SourceText(reference), 16);
            if (e.itemObservationId > 0)
            {
                AnalysisItemObservation item = _displayedAnalysis.items.FirstOrDefault(i => i.observationId == e.itemObservationId);
                if (item != null)
                {
                    var itemRow = DashboardRow("TheoryLink", 42); var theory = CreateButton(itemRow, "Theory", ""); theory.SetCompactText(Text("ITEM DEFINITION", "物品作用与证据"));
                    Anchor(theory.RectTransform, 0, .5f, 0, .5f, 0, 0, 230, 38); theory.AddListener(() => ShowItemEvidence(item));
                }
            }
            AddDashboardText(Text("Observed nearby, not proof of cause", "以下为同期观察，不代表因果关系"), 18);
            foreach (var p in _displayedAnalysis.players.Where(p => e.playerIndex < 0 || p.playerIndex == e.playerIndex || p.playerIndex == e.otherPlayerIndex)) AddPlayerChart(p);
            var pairs = _displayedAnalysis.distancePairs.Where(p => e.playerIndex < 0 || p.playerA == e.playerIndex || p.playerB == e.playerIndex).Take(2);
            foreach (var pair in pairs) AddDistanceChart(pair);
            GetTimeRange(out float start, out float end);
            foreach (var risk in _displayedAnalysis.episodes.Where(p => p.start <= e.time && p.end >= start && p.kind != "Together" && EpochMatches(p.evidence?.epoch ?? -1) && (e.playerIndex < 0 || p.playerIndex == e.playerIndex)))
                AddDashboardText(EventName(risk.kind) + " | " + FormatDuration(risk.start) + " - " + FormatDuration(risk.end) + " | " + StatusName(risk.channel), 18);
            foreach (var item in _displayedAnalysis.items.Where(i => i.time >= start && i.time <= end && EpochMatches(i.epoch) && (e.playerIndex < 0 || i.actorPlayerIndex == e.playerIndex || i.targetPlayerIndex == e.playerIndex || i.playerIndex == e.playerIndex)).Take(50))
                AddEventRow(new ReportEvent { evidence = item.evidence, time = item.time, endTime = item.time, playerIndex = item.actorPlayerIndex, otherPlayerIndex = item.targetPlayerIndex, kind = item.kind, confidence = item.attribution, detail = item.itemName, itemObservationId = item.observationId });
        }
        private static void RenderRelations()
        {
            AddDashboardText(Text("TEAM RELATIONS", "队伍关系"), 24);
            var player = GetSelectedPlayer();
            var pairs = _displayedAnalysis.distancePairs.Where(p => player == null || p.playerA == player.playerIndex || p.playerB == player.playerIndex).ToList();
            foreach (var pair in pairs)
            {
                var row = DashboardRow("Relation", 70); var label = CreateText(row, "Summary", 18, TextAlignmentOptions.Left); Stretch(label.rectTransform); label.rectTransform.offsetMax = new Vector2(-140, 0); label.raycastTarget = false;
                GetTimeRange(out float start, out float end); var summary = new AnalysisDistancePair { intervals = pair.intervals, intervalEpochs = pair.intervalEpochs }; RunAnalysisEngine.SummarizeDistance(summary, start, end, _evidenceEpoch);
                label.text = PlayerName(pair.playerA) + " <-> " + PlayerName(pair.playerB) + " | " + Text("median ", "中位 ") + summary.median.ToString("0.0") + "m\n" +
                    Text("Within 25 / 50 / 100m: ", "25／50／100米内：") + (summary.validSeconds > 0 ? Percent(1 - summary.over25Ratio) + " / " + Percent(1 - summary.over50Ratio) + " / " + Percent(1 - summary.over100Ratio) : Text("unavailable", "不可用"));
                var open = CreateButton(row, "RelationDetail", ""); open.SetCompactText(Text("DETAILS", "详情")); Anchor(open.RectTransform, 1, .5f, 1, .5f, 0, 0, 125, 38);
                int index = _displayedAnalysis.distancePairs.IndexOf(pair); open.AddListener(() => { _relationIndex = index; RenderDetails(); });
            }
            if (_relationIndex >= 0 && _relationIndex < _displayedAnalysis.distancePairs.Count)
            {
                var selected = _displayedAnalysis.distancePairs[_relationIndex]; AddDistanceChart(selected);
                foreach (var e in _displayedAnalysis.timeline.Where(EventMatches).Where(e => (e.playerIndex == selected.playerA && e.otherPlayerIndex == selected.playerB || e.playerIndex == selected.playerB && e.otherPlayerIndex == selected.playerA)).Take(50)) AddEventRow(e);
            }
        }
        private static void RenderContributions()
        {
            var items = _displayedAnalysis.items.Where(ItemMatches).Where(i => i.kind == "ItemConsumed" || i.possibleHelp || i.kind == "ResourceChanged" || i.kind == "PassiveEffectObserved").ToList();
            AddDashboardText(Text("ITEM CONTRIBUTIONS", "物品贡献"), 24);
            var groups = items.GroupBy(i => new { i.itemId, i.prefabName, i.itemName, i.actorPlayerIndex, i.targetPlayerIndex }).ToList();
            _contributionPage = Math.Min(_contributionPage, Math.Max(0, (groups.Count - 1) / 50));
            Pager(_contributionPage, groups.Count, page => { _contributionPage = page; RenderDetails(); });
            foreach (var group in groups.Skip(_contributionPage * 50).Take(50))
            {
                int consumed = group.Where(i => i.kind == "ItemConsumed").Select(i => i.attributionGroupId > 0 ? i.attributionGroupId : -i.observationId).Distinct().Count();
                var effects = group.SelectMany(i => i.observedEffects).GroupBy(e => e.effectId).Select(g => g.First()).ToList();
                AddDashboardText(ItemDisplayName(group.Key.itemName) + " | " + PlayerName(group.Key.actorPlayerIndex) + (group.Any(i => i.actorInferred) ? Text(" (source inferred)", "（推断来源）") : "") + " -> " + PlayerName(group.Key.targetPlayerIndex) +
                    "\n" + Text("Consumed: ", "明确消耗：") + consumed + " | " + Text("Credible effects: ", "可信效果：") + effects.Count(e => e.attribution == "Certain" || e.attribution == "Likely") +
                    " | " + Text("Uncertain effects: ", "未确认效果：") + effects.Count(e => e.attribution == "Ambiguous"), 18);
                var resources = group.Where(i => i.kind == "ResourceChanged").GroupBy(i => i.resourceKey);
                AddDashboardText(Text("Resource net changes: ", "资源净变化：") + string.Join(" | ", resources.Select(g => ResourceName(g.Key) + " " + g.Sum(i => i.value - i.previousValue).ToString("+0.###;-0.###;0"))) +
                    "\n" + Text("Enabled passive intervals (not proven effect time): ", "持续效果启用区间（非确认生效时长）：") + FormatDuration(group.Where(i => i.kind == "PassiveEffectObserved").Sum(i => Math.Max(0, i.endTime - i.time))), 18);
                var representative = group.First();
                AddEventRow(new ReportEvent { evidence = representative.evidence, itemObservationId = representative.observationId, time = representative.time, endTime = representative.time,
                    playerIndex = representative.actorPlayerIndex, otherPlayerIndex = representative.targetPlayerIndex, kind = representative.kind, detail = representative.itemName, confidence = representative.attribution });
            }
            if (groups.Count == 0) AddDashboardText(Text("No matching contributions", "没有符合条件的物品贡献"), 18);
        }
        private static void RenderItemMode()
        {
            RectTransform row = DashboardRow("ItemModes", 44);
            foreach (int mode in new[] { 0, 1, 2, 3 })
            {
                int selected = mode;
                var button = CreateButton(row, "ItemMode" + mode, ""); button.SetCompactText(mode == 0 ? Text("ITEM USE", "使用汇总") : mode == 1 ? Text("ITEM FLOW", "物品流转") : mode == 2 ? Text("CONTRIBUTIONS", "贡献汇总") : Text("OBSERVATIONS", "观察流水"));
                Anchor(button.RectTransform, 0, .5f, 0, .5f, mode * 215, 0, 205, 40); button.SetInteractable(_itemMode != mode);
                button.AddListener(() => { _itemMode = selected; _contributionPage = 0; _useClass = 0; RenderDetails(); });
            }
            if (_itemMode < 2) RenderUseReport(_itemMode == 1); else if (_itemMode == 2) RenderContributions(); else RenderItemList();
        }
        private static string ResourceName(string key)
        {
            string[] en = { "uses", "fuel", "useRemaining", "cookedAmount", "used", "flareActive", "powerEnabled", "petterItemUses" };
            string[] zh = { "次数", "燃料", "剩余比例", "烹饪量", "使用标记", "信号棒", "启用状态", "绳枪次数" };
            int i = Array.IndexOf(en, key); return i < 0 ? key : Text(en[i], zh[i]);
        }
        private static void RefreshHistoryNavigation()
        {
            var controls = _historyPage.GetComponentsInChildren<Selectable>(false).Where(s => s.IsInteractable()).ToArray();
            foreach (var control in controls)
            {
                int i = Array.IndexOf(controls, control); control.navigation = new Navigation { mode = Navigation.Mode.Explicit,
                    selectOnUp = controls[(i + controls.Length - 1) % controls.Length], selectOnDown = controls[(i + 1) % controls.Length],
                    selectOnLeft = controls[(i + controls.Length - 1) % controls.Length], selectOnRight = controls[(i + 1) % controls.Length] };
                var scroll = control.GetComponentInParent<ScrollRect>();
                if (scroll != null) (control.GetComponent<StateKeeperScrollFocus>() ?? control.gameObject.AddComponent<StateKeeperScrollFocus>()).Scroll = scroll;
            }
        }
        private static void RenderQualityReport()
        {
            var summary = new StringBuilder(); AppendQuality(summary, _displayedAnalysis.quality); AddDashboardText(summary.ToString(), 20);
            AddDashboardText(Text("Inventory snapshots processed: ", "已处理库存快照：") + _displayedAnalysis.processedInventoryCount + " / " + _displayedAnalysis.sourceInventorySnapshotCount, 18);
            AddDashboardText(Text("Analysis time (before cache write): ", "分析耗时（不含缓存写入）：") + _displayedAnalysis.analysisMilliseconds.ToString("0") + "ms | " +
                Text("Observed process managed-memory maximum: ", "观测到的进程托管内存最大值：") + (_displayedAnalysis.observedManagedBytes / 1048576.0).ToString("0.0") + "MB", 18);
            AddDashboardText(Text("Risk thresholds: regular <=10% and extra <=0.05 for 3s; capacity <=0.25 for 10s; status +0.2 in 5s; isolation >100m for 10s, ends below 80m. Nearby events do not prove cause.",
                "风险口径：普通体力≤容量10%且额外≤0.05持续3秒；容量≤0.25持续10秒；状态5秒净增0.2；最近队友超过100米持续10秒，低于80米结束。相邻事件不证明因果。"), 18);
            foreach (var p in FilterPlayers()) AddDashboardText(LifecycleSummary(p), 18);
            var modes = DashboardRow("QualityModes", 44);
            string[] labels = { Text("ANOMALIES", "异常记录"), Text("LIFE INTERVALS", "生命周期区间"), Text("RAW LIFE EVENTS", "原始生命事件") };
            for (int i = 0; i < labels.Length; i++)
            {
                int selected = i; var button = CreateButton(modes, "QualityMode" + i, ""); button.SetCompactText(labels[i]);
                Anchor(button.RectTransform, 0, .5f, 0, .5f, i * 265, 0, 250, 40); button.SetInteractable(_qualityMode != i);
                button.AddListener(() => { _qualityMode = selected; _qualityPage = 0; RenderDetails(); });
            }
            GetTimeRange(out float start, out float end);
            bool PlayerMatches(int id) => GetSelectedPlayer() == null || id == GetSelectedPlayer().playerIndex;
            if (_qualityMode == 1)
            {
                var intervals = _displayedAnalysis.lifecycleIntervals.Where(i => i.end >= start && i.start <= end && EpochMatches(i.evidence.epoch) && PlayerMatches(i.playerIndex)).ToList();
                _qualityPage = Math.Min(_qualityPage, Math.Max(0, (intervals.Count - 1) / 50));
                Pager(_qualityPage, intervals.Count, page => { _qualityPage = page; RenderDetails(); });
                foreach (var interval in intervals.Skip(_qualityPage * 50).Take(50))
                    AddEventRow(new ReportEvent { evidence = interval.evidence, playerIndex = interval.playerIndex, time = interval.start, endTime = interval.end,
                        kind = interval.state == "Alive" ? "AliveStateObserved" : interval.state + "StateObserved", confidence = "Likely" });
                return;
            }
            if (_qualityMode == 2)
            {
                var events = _displayedAnalysis.lifecycle.Where(e => InSelectedRange(e.time) && EpochMatches(e.evidence.epoch) && PlayerMatches(e.playerIndex)).ToList();
                _qualityPage = Math.Min(_qualityPage, Math.Max(0, (events.Count - 1) / 50));
                Pager(_qualityPage, events.Count, page => { _qualityPage = page; RenderDetails(); });
                foreach (var e in events.Skip(_qualityPage * 50).Take(50))
                {
                    string verdict = e.verdict == "Confirmed" ? Text("Confirmed", "已确认") : e.verdict == "Duplicate" ? Text("Duplicate", "重复观察") : Text("Unconfirmed", "未确认");
                    AddDashboardText(PlayerName(e.playerIndex) + " | " + FormatDuration(e.time) + " | " + verdict + " | " + Text("duplicates ", "重复 ") + e.duplicateCount + " | " + QualityReason(e.reason) + "\n" + SourceText(e.evidence), 18);
                    AddEventRow(new ReportEvent { evidence = e.evidence, playerIndex = e.playerIndex, time = e.time, endTime = e.time,
                        kind = e.kind == "Death" ? "RawDeathObserved" : "RawPassedOutObserved", confidence = e.verdict == "Confirmed" ? "Certain" : "Ambiguous" });
                }
                return;
            }
            var diagnostics = _displayedAnalysis.diagnostics.Where(d => InSelectedRange(d.time) && EpochMatches(d.evidence?.epoch ?? -1) && (GetSelectedPlayer() == null || d.playerIndex == GetSelectedPlayer().playerIndex)).ToList();
            _qualityPage = Math.Min(_qualityPage, Math.Max(0, (diagnostics.Count - 1) / 50));
            Pager(_qualityPage, diagnostics.Count, page => { _qualityPage = page; RenderDetails(); });
            foreach (var d in diagnostics.Skip(_qualityPage * 50).Take(50))
            {
                AddDashboardText(FormatDuration(d.time) + " | " + PlayerName(d.playerIndex) + " | " + QualityReason(d.reason) + " x" + d.count +
                    "\n" + "(" + d.x.ToString("0.###") + ", " + d.y.ToString("0.###") + ", " + d.z.ToString("0.###") + ")" + (d.distance.HasValue ? " | " + d.distance.Value.ToString("0.###") + "m" : "") + "\n" + SourceText(d.evidence), 18);
            }
        }
        private static string QualityReason(string key)
        {
            string[] en = { "InvalidPosition", "PositionDiscontinuity", "PositionRecovery", "DistanceMismatch", "ClockAmbiguous", "ObservationInterrupted", "StateCorroborated", "AlreadyInState", "StateNotCorroborated" };
            string[] zh = { "无效观测或死亡暂存点", "位置发生不连续跳变", "位置恢复确认中", "保存距离与坐标不一致", "时间回退重叠，无法唯一定位", "同步或传送观察中断", "存在状态佐证", "同一状态中的重复观察", "缺少有效状态佐证" };
            int i = Array.IndexOf(en, key); return i < 0 ? key : Text(en[i], zh[i]);
        }
        private static void ResetReports()
        {
            _searchTerms.Clear(); _modalDisabled.Clear(); _focusedEvent = null; _rangeStart = _rangeEnd = null; _evidenceEpoch = null;
            _compareService?.Cancel(); if (_comparePage != null) UnityEngine.Object.Destroy(_comparePage.gameObject); _comparePage = null; _compareButton = _compareBack = _compareAction = null;
            _comparisonSelection.Clear(); _compareEntries.Clear(); _compareResults.Clear();
        }
    }
}
