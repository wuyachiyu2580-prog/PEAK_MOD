using System;
using System.Collections.Generic;
using System.Linq;

namespace StateKeeper
{
    internal static partial class StateKeeperUi
    {
        private static int _useClass;
        private static bool _usePlayerSort;
        private static readonly string[] UseClasses = { "", "CertainUse", "LikelyUse", "PossibleUse", "Transferred", "DroppedOrLost", "Ambiguous" };
        private static string UseClassName(string value)
        {
            string[] zh = { "全部分类", "明确使用", "可能使用", "疑似使用", "移动／转移", "掉落／丢失", "未确认" };
            int index = Array.IndexOf(UseClasses, value);
            return index < 0 ? value : Text(index == 0 ? "ALL CLASSES" : value, zh[index]);
        }
        private static void RenderUseReport(bool flow)
        {
            var controls = DashboardRow("UseControls", 44);
            var filter = CreateButton(controls, "UseClass", ""); filter.SetCompactText(UseClassName(UseClasses[_useClass]));
            Anchor(filter.RectTransform, 0, .5f, 0, .5f, 0, 0, 280, 40);
            filter.AddListener(() => { _useClass = (_useClass + 1) % UseClasses.Length; _contributionPage = 0; RenderDetails(); });
            var sort = CreateButton(controls, "UseSort", ""); sort.SetCompactText(_usePlayerSort ? Text("SORT: PLAYER", "排序：玩家") : Text("SORT: TIME", "排序：时间"));
            Anchor(sort.RectTransform, 0, .5f, 0, .5f, 300, 0, 280, 40);
            sort.AddListener(() => { _usePlayerSort = !_usePlayerSort; _contributionPage = 0; RenderDetails(); });
            var player = GetSelectedPlayer();
            var groups = _displayedAnalysis.items.GroupBy(ItemUseRules.GroupKey).Select(g => g.ToList()).Where(g => {
                var r = ItemUseRules.Representative(g);
                return ItemUseRules.IsUse(r.useClassification) != flow &&
                    (_useClass == 0 || r.useClassification == UseClasses[_useClass]) &&
                    (_confidenceFilter == 0 || ItemUseRules.Confidence(r.useClassification) == ConfidenceName(_confidenceFilter)) &&
                    (player == null || g.Any(i => i.playerIndex == player.playerIndex || i.actorPlayerIndex == player.playerIndex || i.targetPlayerIndex == player.playerIndex)) &&
                    InSelectedRange(r.time) && EpochMatches(r.epoch) && g.Any(SearchItem) &&
                    (_resourceFilter == 0 || g.Any(i => i.resourceKey == ResourceFilters[_resourceFilter]));
            }).OrderBy(g => _usePlayerSort ? PlayerName(ItemUseRules.Representative(g).actorPlayerIndex) : "")
              .ThenBy(g => ItemUseRules.Representative(g).epoch).ThenBy(g => ItemUseRules.Representative(g).time).ToList();
            AddDashboardText(flow ? Text("ITEM FLOW", "物品流转") : Text("ITEM USE IN SELECTED RANGE", "所选范围的物品使用"), 24);
            AddDashboardText(Text("Completed actions and charge reductions are counted. Continuous resource loss shows quantities; action count is unknown. Confidence here concerns use, not effect causality. Scores are rule scores, not probabilities.",
                "完成动作和次数减少可计数；持续资源消耗显示消耗量，动作次数未知。此处置信度针对使用事实，效果归因另列。分数为规则分，不是概率。"), 18);
            _contributionPage = Math.Min(_contributionPage, Math.Max(0, (groups.Count - 1) / 50));
            AddDashboardText(Text("Actions / observations: ", "可计次数／观察组：") + groups.Sum(g => g.Max(r => r.useCount)) + " / " + groups.Count, 18);
            Pager(_contributionPage, groups.Count, page => { _contributionPage = page; RenderDetails(); });
            var pageGroups = groups.Skip(_contributionPage * 50).Take(50).ToList();
            foreach (var group in pageGroups)
            {
                var r = ItemUseRules.Representative(group); var summary = ItemUseRules.Summarize(group);
                AddDashboardText(ItemDisplayName(r.itemName) + " | " + UseClassName(r.useClassification) + " | " +
                    Text("count: ", "次数：") + (summary.useCount > 0 ? summary.useCount.ToString() : ItemUseRules.IsUse(r.useClassification) ? Text("unknown", "未知") : "0") +
                    " | " + Text("rule score: ", "规则分：") + r.attributionScore +
                    "\n" + string.Join(" | ", summary.resourceConsumption.Select(p => ResourceName(p.Key) + " " + p.Value.ToString("0.###"))), 18);
                AddEventRow(new ReportEvent { evidence = r.evidence, supportingEvidence = summary.evidence, itemObservationId = r.observationId,
                    time = r.time, endTime = r.endTime, playerIndex = r.actorInferred && ItemUseRules.Direct(r) ? -1 : r.actorPlayerIndex,
                    otherPlayerIndex = r.targetPlayerIndex, kind = r.kind, detail = r.itemName, confidence = ItemUseRules.Confidence(r.useClassification) });
            }
            if (groups.Count == 0) AddDashboardText(Text("No matching observations", "没有符合条件的观察"), 18);
        }
    }
}
