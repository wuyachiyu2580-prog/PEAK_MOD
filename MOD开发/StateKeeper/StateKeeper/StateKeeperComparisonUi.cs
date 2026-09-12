using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace StateKeeper
{
    internal static partial class StateKeeperUi
    {
        private static readonly HashSet<string> _comparisonSelection = new HashSet<string>();
        private static readonly Dictionary<string, RunIndexEntry> _compareEntries = new Dictionary<string, RunIndexEntry>();
        private static readonly Dictionary<string, AnalysisResult> _compareResults = new Dictionary<string, AnalysisResult>();
        private static StateKeeperButton _compareButton, _compareAction, _compareBack;
        private static StateKeeperDetailsPage _comparePage;
        private static RunAnalysisService _compareService;
        private static TextMeshProUGUI _compareStatus, _compareTitle;
        private static Transform _compareContent;
        private static List<string> _compareQueue;
        private static int _compareCursor;
        private static bool _compareFinished;

        private static IEnumerable<RunIndexEntry> AllHistoryEntries()
        {
            return StateKeeperPlugin.Store.GetRecentEntries().Concat(StateKeeperPlugin.Store.GetFavoriteEntries()).GroupBy(e => e.runId).Select(g => g.First());
        }
        private static void AddCompareSelection(Transform row, RunIndexEntry entry)
        {
            if (_compareButton == null || !_compareButton.IsAlive)
            {
                _compareButton = CreateButton(_historyPage.transform, "CompareRuns", "");
                Anchor(_compareButton.RectTransform, .5f, 1, .5f, 1, 510, -180, 220, 42);
                _compareButton.AddListener(StartComparison);
            }
            var toggleObject = new GameObject("CompareSelection", typeof(RectTransform), typeof(Image), typeof(Toggle));
            toggleObject.transform.SetParent(row, false); Anchor(toggleObject.GetComponent<RectTransform>(), 0, .5f, 0, .5f, 12, 0, 26, 26);
            toggleObject.GetComponent<Image>().color = new Color(.32f, .32f, .32f);
            var check = new GameObject("Check", typeof(RectTransform), typeof(Image)); check.transform.SetParent(toggleObject.transform, false);
            Stretch(check.GetComponent<RectTransform>()); check.GetComponent<RectTransform>().offsetMin = new Vector2(5, 5); check.GetComponent<RectTransform>().offsetMax = new Vector2(-5, -5);
            check.GetComponent<Image>().color = new Color(.3f, .85f, .7f);
            Toggle toggle = toggleObject.GetComponent<Toggle>(); toggle.targetGraphic = toggleObject.GetComponent<Image>(); toggle.graphic = check.GetComponent<Image>();
            toggle.SetIsOnWithoutNotify(_comparisonSelection.Contains(entry.runId));
            toggle.interactable = entry.schemaVersion == 3 && entry.status != "Active";
            toggle.onValueChanged.AddListener(value =>
            {
                if (value && _comparisonSelection.Count >= 4) { toggle.SetIsOnWithoutNotify(false); return; }
                if (value) _comparisonSelection.Add(entry.runId); else _comparisonSelection.Remove(entry.runId);
                RefreshCompareLabel();
            });
            RefreshCompareLabel();
        }
        private static void RefreshCompareLabel()
        {
            if (_compareButton == null || !_compareButton.IsAlive) return;
            var ids = new HashSet<string>(AllHistoryEntries().Select(e => e.runId));
            _comparisonSelection.RemoveWhere(id => !ids.Contains(id));
            foreach (string id in _compareEntries.Keys.Where(id => !ids.Contains(id)).ToArray()) { _compareEntries.Remove(id); _compareResults.Remove(id); }
            _compareButton.SetCompactText(Text("COMPARE", "对比选中局") + " (" + _comparisonSelection.Count + "/4)");
            _compareButton.SetInteractable(_comparisonSelection.Count >= 2);
        }
        private static void StartComparison()
        {
            if (_comparisonSelection.Count < 2) return;
            EnsureComparisonPage();
            _compareEntries.Clear(); _compareResults.Clear();
            foreach (var entry in AllHistoryEntries().Where(e => _comparisonSelection.Contains(e.runId))) _compareEntries[entry.runId] = entry;
            _compareQueue = _compareEntries.Values.OrderBy(e => e.startedUtc).Select(e => e.runId).ToList();
            _compareCursor = 0; _compareFinished = false;
            _compareAction.GameObject.SetActive(true);
            _compareService = new RunAnalysisService(StateKeeperPlugin.Store);
            _compareService.Start(_compareQueue[0]);
            Transition(_comparePage);
        }
        private static void EnsureComparisonPage()
        {
            if (_comparePage != null) return;
            var page = new GameObject("StateKeeperComparisonPage", typeof(RectTransform)); page.transform.SetParent(_historyPage.transform.parent, false); Stretch(page.GetComponent<RectTransform>());
            _comparePage = page.AddComponent<StateKeeperDetailsPage>(); _comparePage.ParentPage = _historyPage;
            _comparePage.OnTick = PollComparison; _comparePage.OnClosed = () => _compareService?.Cancel();
            AddImage(page.transform, "Background", new Color(.02f, .02f, .02f, .98f), true);
            _compareTitle = CreateText(page.transform, "Title", 32, TextAlignmentOptions.Center); Anchor(_compareTitle.rectTransform, .5f, 1, .5f, 1, 0, -28, 1000, 50);
            _compareStatus = CreateText(page.transform, "Status", 18, TextAlignmentOptions.Left); Anchor(_compareStatus.rectTransform, .5f, 1, .5f, 1, -90, -90, 1000, 55);
            _compareAction = CreateButton(page.transform, "ComparisonAction", ""); Anchor(_compareAction.RectTransform, .5f, 1, .5f, 1, 550, -100, 150, 40);
            _compareAction.AddListener(() =>
            {
                if (_compareService.IsRunning) _compareService.Cancel();
                else if (_compareQueue != null && _compareCursor < _compareQueue.Count) _compareService.Start(_compareQueue[_compareCursor], true);
            });
            var scroll = new GameObject("ComparisonScroll", typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect)); scroll.transform.SetParent(page.transform, false);
            Stretch(scroll.GetComponent<RectTransform>()); scroll.GetComponent<RectTransform>().offsetMin = new Vector2(75, 110); scroll.GetComponent<RectTransform>().offsetMax = new Vector2(-75, -165); scroll.GetComponent<Image>().color = Color.clear;
            var content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter)); content.transform.SetParent(scroll.transform, false);
            RectTransform rect = content.GetComponent<RectTransform>(); rect.anchorMin = new Vector2(0, 1); rect.anchorMax = Vector2.one; rect.pivot = new Vector2(.5f, 1); rect.sizeDelta = Vector2.zero;
            var layout = content.GetComponent<VerticalLayoutGroup>(); layout.childControlHeight = true; layout.childForceExpandHeight = false; layout.spacing = 22;
            content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.GetComponent<ScrollRect>().content = rect; scroll.GetComponent<ScrollRect>().viewport = scroll.GetComponent<RectTransform>(); scroll.GetComponent<ScrollRect>().horizontal = false;
            _compareContent = content.transform;
            _compareBack = CreateButton(page.transform, "Back", Text("BACK", "返回")); Anchor(_compareBack.RectTransform, .5f, 0, .5f, 0, 0, 30, 220, 54); _compareBack.AddListener(() => Transition(_historyPage));
            page.SetActive(false);
        }
        private static void PollComparison()
        {
            if (_compareService == null || _compareQueue == null) return;
            _compareTitle.text = Text("SAME PLAYER ACROSS RUNS", "同玩家跨局对比");
            if (_compareFinished) { _compareStatus.text = Text("Comparison complete", "对比完成"); return; }
            string name = _compareCursor < _compareQueue.Count ? RunName(_compareEntries[_compareQueue[_compareCursor]]) : "";
            if (_compareService.IsRunning)
            {
                int total = _compareService.TotalChunks, current = _compareService.CurrentChunk;
                _compareStatus.text = name + " | " + (_compareCursor + 1) + "/" + _compareQueue.Count + " | " + current + "/" + total +
                    (total > 0 ? " (" + (current * 100 / total) + "%)" : Text(" | validating cache", " | 正在校验缓存"));
                _compareAction.SetCompactText(Text("CANCEL", "取消")); return;
            }
            if (_compareService.Error != null || _compareService.Status == "Cancelled")
            {
                _compareStatus.text = name + " | " + (_compareService.Error != null ? Text("Failed: ", "失败：") + _compareService.Error.Message : Text("Cancelled", "已取消"));
                _compareAction.SetCompactText(Text("RETRY", "重试此局")); return;
            }
            if (_compareService.Result == null) return;
            _compareResults[_compareQueue[_compareCursor]] = ReportComparison.Summary(_compareService.Result); _compareService.ReleaseResult(); _compareCursor++;
            RenderComparison();
            if (_compareCursor < _compareQueue.Count) _compareService.Start(_compareQueue[_compareCursor]);
            else { _compareFinished = true; _compareStatus.text = Text("Comparison complete", "对比完成"); _compareAction.GameObject.SetActive(false); }
        }
        private static void ComparisonText(string value, float size = 18)
        {
            var text = CreateText(_compareContent, "Text", size, TextAlignmentOptions.Left); text.text = value; text.richText = false; text.raycastTarget = false;
            text.gameObject.AddComponent<LayoutElement>().minHeight = size * 1.5f;
        }
        private static void RenderComparison()
        {
            if (_compareContent == null) return;
            for (int i = _compareContent.childCount - 1; i >= 0; i--) { var child = _compareContent.GetChild(i).gameObject; child.SetActive(false); UnityEngine.Object.Destroy(child); }
            var runs = _compareQueue.Where(id => _compareEntries.ContainsKey(id) && _compareResults.ContainsKey(id)).Select(id => _compareResults[id]).ToList();
            foreach (var r in runs)
                ComparisonText(RunName(_compareEntries[r.runId]) + " | " + FormatDate(_compareEntries[r.runId].startedUtc) + " | " + r.gameVersion +
                    " | " + Text("Ascent ", "难度 ") + (r.hasAscentLevel ? r.ascentLevel.ToString() : Text("unknown", "未知")) +
                    " | " + Text("Players ", "人数 ") + r.players.Count + " | " + Text("Progress points ", "进度点 ") + string.Join(", ", r.mountainSegments.Select(s => Text(s.titleKey, s.capturedTitle))) +
                    "\n" + Text("Collected capabilities: ", "采集能力：") + string.Join(", ", r.collectionCapabilities));
            ComparisonText(Text("Counts and per-10-minute rates use valid observation time for life events, valid alive time for actions. Different conditions are not a skill rating.",
                "生命事件按有效观察时长、行动按有效存活时长计算每十分钟频率。条件不同不代表能力变化。"));
            var identities = runs.SelectMany(r => r.players.Where(p => ReportComparison.ValidSteamId(p.stableUserId)).Select(p => p.stableUserId)).Distinct().ToList();
            foreach (string identity in identities)
            {
                var rows = runs.Select(r => new { run = r, player = r.players.FirstOrDefault(p => p.stableUserId == identity) }).Where(p => p.player != null).ToList();
                if (rows.Count < 2) continue;
                ComparisonText(rows.Last().player.displayName, 24);
                bool comparableHelp = rows.All(r => ReportComparison.ComparableHelp(rows[0].run, r.run));
                foreach (var row in rows)
                {
                    var p = row.player; var r = row.run;
                    int consumed = p.consumedCount;
                    int help = p.possibleHelpCount;
                    string FormatMetric(float count, float denominator) => count.ToString("0.##") + " / " + (ReportComparison.Rate(count, denominator)?.ToString("0.##") ?? Text("unavailable", "不可用"));
                    ComparisonText(RunName(_compareEntries[r.runId]) + " | " + Text("observed / alive ", "有效观察／存活 ") + FormatDuration(p.observedSeconds) + " / " + FormatDuration(p.aliveSeconds) +
                        "\n" + Text("Deaths count / rate: ", "确认死亡 次数／频率：") + FormatMetric(p.deathCount, p.observedSeconds) + " | " + Text("Passed out: ", "昏迷：") + FormatMetric(p.passedOutCount, p.observedSeconds) +
                        "\n" + Text("Jumps: ", "跳跃：") + FormatMetric(p.jumpCount, p.aliveSeconds) + " | " + Text("Consumed: ", "明确消耗：") + FormatMetric(consumed, p.aliveSeconds) +
                        "\n" + Text("Low stamina / capacity / isolation: ", "低体力／容量受限／单独活动：") + (p.aliveSeconds > 0 ? Percent(p.lowStaminaSeconds / p.aliveSeconds) + " / " + Percent(p.lowCapacitySeconds / p.aliveSeconds) + " / " + Percent(p.isolatedSeconds / p.aliveSeconds) : Text("unavailable", "不可用")) +
                        "\n" + Text("Possible help: ", "可能帮助：") + FormatMetric(help, p.aliveSeconds) + " | " + (comparableHelp && r.collectionCapabilities.Contains("BroadcastRescuePull") ? Text("Observed pull starts: ", "观察到拉回启动：") + p.rescuePullCount : Text("Direct assistance missing or coverage differs; not comparable", "直接帮助未采集或覆盖不同，不可比较")) +
                        (!ReportComparison.Reliable(r, p) ? "\n" + Text("Insufficient observation duration or coverage; no change conclusion", "观察不足10分钟或覆盖低于80%，不输出增减结论") : ""));
                }
                if (rows.All(r => ReportComparison.Reliable(r.run, r.player)))
                {
                    var first = rows.First(); var last = rows.Last();
                    float? change = ReportComparison.Rate(last.player.deathCount, last.player.observedSeconds) - ReportComparison.Rate(first.player.deathCount, first.player.observedSeconds);
                    ComparisonText(Text("Last vs first, confirmed deaths per 10 minutes: ", "末局相对首局，确认死亡每十分钟差值：") + change?.ToString("+0.##;-0.##;0") + Text(" (descriptive only)", "（仅描述差异）"));
                }
            }
            foreach (var r in runs)
                foreach (var p in r.players.Where(p => !ReportComparison.ValidSteamId(p.stableUserId) || runs.Count(other => other.players.Any(q => q.stableUserId == p.stableUserId)) < 2))
                    ComparisonText(RunName(_compareEntries[r.runId]) + " | " + p.displayName + " | " + Text("Single-run only; no reliable matching record", "仅单局展示，无可靠匹配记录") + " | " + Text("Deaths ", "确认死亡 ") + p.deathCount);
            Selectable[] controls = _comparePage.GetComponentsInChildren<Selectable>(false);
            for (int i = 0; i < controls.Length; i++) controls[i].navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = controls[(i + controls.Length - 1) % controls.Length], selectOnDown = controls[(i + 1) % controls.Length] };
        }
    }
}
