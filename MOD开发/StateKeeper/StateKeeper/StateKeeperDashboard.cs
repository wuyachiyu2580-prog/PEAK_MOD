using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Zorro.Core;

namespace StateKeeper
{
    internal static partial class StateKeeperUi
    {
        private static readonly Queue<RectTransform> ChartRows = new Queue<RectTransform>();
        private static readonly Color[] StatusColors = {
            new Color(.92f,.3f,.3f), new Color(.7f,.35f,.75f), new Color(.9f,.65f,.2f),
            new Color(.35f,.7f,.95f), new Color(.65f,.65f,.65f), new Color(.9f,.4f,.2f),
            new Color(.5f,.8f,.4f), new Color(.65f,.5f,.85f)
        };

        private static void AppendAssistanceSummary(StringBuilder body)
        {
            body.AppendLine();
            bool treatment = _displayedAnalysis.collectionCapabilities.Contains("LocalRecipientTreatment");
            bool pulls = _displayedAnalysis.collectionCapabilities.Contains("BroadcastRescuePull");
            if (!treatment && !pulls)
            {
                body.AppendLine(Text("Assistance: not recorded; team totals unknown", "救援专用记录：未采集，全队次数未知"));
                return;
            }
            body.AppendLine(Text("Observed assistance (partial coverage)", "观察到的帮助（非全队完整统计）"));
            foreach (AnalysisPlayer player in FilterPlayers())
            {
                var events = _displayedAnalysis.assistance.Where(e => e.actorPlayerIndex == player.playerIndex && InSelectedRange(e.time)).ToList();
                body.AppendLine(DisplayPlayerName(player) + " | " + Text("Emergency treatment ", "急救越过昏迷阈值 ") +
                    (treatment ? events.Count(e => e.kind == "PlayerLifeSaved").ToString() : Text("unknown", "未知")) + " | " +
                    Text("Pull initiated ", "拉回启动 ") + (pulls ? events.Count(e => e.kind == "PlayerRescuePulled").ToString() : Text("unknown", "未知")));
            }
            body.AppendLine(Text("Treatment scope: recipient is the recording client. Pulls: broadcast observations, not confirmed survival.",
                "治疗覆盖：受益者为录制客户端角色。拉回记录为广播命中，不代表最终获救。"));
        }

        private static RectTransform DashboardRow(string name, float height, Transform parent = null)
        {
            GameObject value = new GameObject(name, typeof(RectTransform), typeof(LayoutElement));
            value.transform.SetParent(parent == null ? _dashboardContent.transform : parent, false);
            value.GetComponent<LayoutElement>().preferredHeight = height;
            return value.GetComponent<RectTransform>();
        }

        private static StateKeeperChart AddAxisChart(float maximum, float start, float end, string unit)
        {
            while (ChartRows.Count > 0)
            {
                RectTransform reused = ChartRows.Dequeue();
                if (reused == null) continue;
                reused.SetParent(_dashboardContent.transform, false); reused.gameObject.SetActive(true);
                for (int i = 0; i <= 4; i++) reused.Find("Y" + i).GetComponent<TextMeshProUGUI>().text = (maximum * i / 4).ToString("0.0") + unit;
                reused.GetComponent<StateKeeperAxes>().ResetRange(start, end);
                return reused.Find("Plot").GetComponent<StateKeeperChart>();
            }
            RectTransform row = DashboardRow("ChartWithAxes", 240f);
            var graph = new GameObject("Plot", typeof(RectTransform), typeof(StateKeeperChart));
            graph.transform.SetParent(row, false);
            RectTransform rect = graph.GetComponent<RectTransform>();
            Stretch(rect); rect.offsetMin = new Vector2(88, 32); rect.offsetMax = new Vector2(-24, -12);
            for (int i = 0; i <= 4; i++)
            {
                TextMeshProUGUI label = CreateText(row, "Y" + i, 16, TextAlignmentOptions.Right);
                label.text = (maximum * i / 4).ToString("0.0") + unit;
                Anchor(label.rectTransform, 0, 0, 0, 0, 0, 21 + i * 49, 78, 24);
                label.raycastTarget = false;
            }
            var axes = row.gameObject.AddComponent<StateKeeperAxes>(); axes.start = start; axes.end = end;
            axes.labels = Enumerable.Range(0, 7).Select(i => CreateText(row, "TimeAxis" + i, 16, TextAlignmentOptions.Center)).ToArray();
            foreach (var label in axes.labels) label.raycastTarget = false;
            return graph.GetComponent<StateKeeperChart>();
        }

        private static void AddStatusSnapshot(List<AnalysisSeriesPoint> points)
        {
            RectTransform row = DashboardRow("StatusSnapshot", 320);
            TextMeshProUGUI label = CreateText(row, "SnapshotValues", 18, TextAlignmentOptions.TopLeft);
            Stretch(label.rectTransform); label.rectTransform.offsetMin = new Vector2(0, 122);
            label.raycastTarget = false;
            RectTransform regular = DashboardRow("RegularBar", 22, row);
            Anchor(regular, 0, 0, 1, 0, 0, 92, 0, 22);
            RectTransform extra = DashboardRow("ExtraBar", 18, row);
            Anchor(extra, 0, 0, 1, 0, 0, 64, 0, 18);
            TextMeshProUGUI legend = CreateText(row, "Legend", 16, TextAlignmentOptions.Left);
            legend.raycastTarget = false;
            Anchor(legend.rectTransform, 0, 0, 1, 0, 0, 34, 0, 28);
            GameObject sliderObject = new GameObject("SnapshotTime", typeof(RectTransform), typeof(Image), typeof(Slider));
            sliderObject.transform.SetParent(row, false);
            Anchor(sliderObject.GetComponent<RectTransform>(), 0, 0, 1, 0, 0, 0, 0, 22);
            sliderObject.GetComponent<Image>().color = new Color(.2f, .2f, .2f);
            var handle = new GameObject("Handle", typeof(RectTransform), typeof(Image)); handle.transform.SetParent(sliderObject.transform, false);
            handle.GetComponent<RectTransform>().sizeDelta = new Vector2(14, 22);
            Slider slider = sliderObject.GetComponent<Slider>(); slider.handleRect = handle.GetComponent<RectTransform>(); slider.targetGraphic = handle.GetComponent<Image>();
            slider.wholeNumbers = true; slider.minValue = 0; slider.maxValue = Math.Max(1, points.Count - 1); slider.interactable = points.Count > 1;
            Action<int> update = index =>
            {
                AnalysisSeriesPoint point = points[Math.Min(index, points.Count - 1)];
                foreach (Transform bar in new[] { regular, extra })
                    for (int i = bar.childCount - 1; i >= 0; i--) bar.GetChild(i).gameObject.SetActive(false);
                label.text = FormatDuration(point.endTime) + " | " + Text("regular ", "普通 ") + point.regularStamina.ToString("0.00") +
                    " / " + point.maxStamina.ToString("0.00") + " | " + Text("extra ", "额外 ") + point.extraStamina.ToString("0.00") +
                    " | " + Text("petrify ", "石化 ") + (point.petrifyAmount.HasValue ? point.petrifyAmount.Value + "%" : Text("unknown", "未采集")) +
                    (point.dead ? Text(" | DEAD", " | 已死亡") : "");
                float[] statuses = point.statuses;
                float extent = Mathf.Max(1f, Mathf.Max(0, point.maxStamina) + (statuses == null ? 0 : statuses.Sum(s => Mathf.Max(0, s))));
                PaintBar(regular, 0, 1, new Color(.18f, .18f, .18f));
                PaintBar(regular, 0, Mathf.Min(point.regularStamina, point.maxStamina) / extent, new Color(.25f, .85f, .35f));
                float offset = Mathf.Max(0, point.maxStamina) / extent;
                StringBuilder states = new StringBuilder();
                if (statuses == null) states.Append(Text("Statuses not recorded", "状态未采集"));
                else for (int i = 0; i < statuses.Length && i < _displayedAnalysis.statusTypeOrder.Length; i++)
                {
                    if (statuses[i] <= 0 || _displayedAnalysis.statusTypeOrder[i] == "Petrify") continue;
                    Color color = StatusColors[i % StatusColors.Length];
                    PaintBar(regular, offset, offset + statuses[i] / extent, color); offset += statuses[i] / extent;
                    states.Append("  <color=#" + ColorUtility.ToHtmlStringRGB(color) + ">" + StatusName(_displayedAnalysis.statusTypeOrder[i]) + " " + statuses[i].ToString("0.00") + "</color>");
                }
                if (point.afflictions != null && point.afflictions.Count > 0)
                    states.Append(" | " + string.Join(", ", point.afflictions.Select(i => i >= 0 && i < _displayedAnalysis.afflictionTypeOrder.Length ? StatusName(_displayedAnalysis.afflictionTypeOrder[i]) : i.ToString())));
                label.text += "\n" + states;
                PaintBar(extra, 0, 1, new Color(.18f, .18f, .18f));
                float extraExtent = Mathf.Max(1, point.extraStamina);
                PaintBar(extra, 0, point.extraStamina / extraExtent, new Color(1f, .75f, .2f));
                if (point.petrifyAmount.HasValue) PaintBar(extra, Mathf.Max(0, 1f - point.petrifyAmount.Value / 100f) / extraExtent, 1f / extraExtent, Color.gray);
                legend.text = Text("Regular + status occupancy | extra + petrify", "普通体力与状态占用 | 额外体力与石化") + (extent > 1 || extraExtent > 1 ? Text(" | overflow", " | 超出正常上限") : "");
            };
            slider.onValueChanged.AddListener(value => update((int)value)); update(0);
        }

        private static void PaintBar(Transform parent, float left, float right, Color tint)
        {
            GameObject part = null;
            for (int i = 0; i < parent.childCount; i++) if (!parent.GetChild(i).gameObject.activeSelf) { part = parent.GetChild(i).gameObject; break; }
            if (part == null) { part = new GameObject("BarPart", typeof(RectTransform), typeof(Image)); part.transform.SetParent(parent, false); }
            part.SetActive(true);
            RectTransform rect = part.GetComponent<RectTransform>(); rect.anchorMin = new Vector2(Mathf.Clamp01(left), 0); rect.anchorMax = new Vector2(Mathf.Clamp01(right), 1); rect.offsetMin = rect.offsetMax = Vector2.zero;
            part.GetComponent<Image>().color = tint; part.GetComponent<Image>().raycastTarget = false;
        }

        private static string StatusName(string name)
        {
            if (name == null) return "";
            if (name.StartsWith("Affliction:", StringComparison.Ordinal)) return StatusName(name.Substring(11));
            string[] en = { "Injury", "Poison", "Hunger", "Cold", "Weight", "Hot", "Drowsy", "Spores", "Petrify", "Crab", "Curse", "Thorns", "Web", "Arrow", "FlyTrap", "RegularStamina", "ExtraStamina", "PoisonOverTime", "InfiniteStamina", "FasterBoi", "Exhausted", "Glowing", "ColdOverTime", "Chaos", "AdjustStatus", "ClearAllStatus", "PreventPoisonHealing", "AddBonusStamina", "DrowsyOverTime", "AdjustStatusOverTime", "Sunscreen", "BingBongShield", "ZombieBite", "Invincibility", "LowGravity", "Blind", "Numb", "ClimbingChalk", "NoHunger", "HealAll", "DoubleJumpAmulet", "RadiateInfiniteStam", "MassSuperJump" };
            string[] zh = { "受伤", "中毒", "饥饿", "寒冷", "负重", "炎热", "困倦", "孢子", "石化", "蟹钳", "诅咒", "荆棘", "蛛网", "箭伤", "捕蝇草", "普通体力", "额外体力", "持续中毒", "无限体力", "加速", "疲惫", "发光", "持续寒冷", "混乱", "状态调整", "清除状态", "阻止毒素恢复", "额外体力增加", "持续困倦", "持续状态调整", "防晒", "防护", "僵尸咬伤", "无敌", "低重力", "失明", "麻木", "攀爬粉", "无饥饿", "全面治疗", "双跳护符", "范围无限体力", "群体超级跳跃" };
            int i = Array.IndexOf(en, name); return i < 0 ? name : Text(name, zh[i]);
        }

        private static void RenderItemList()
        {
            List<AnalysisItemObservation> items = _displayedAnalysis.items.Where(ItemMatches).ToList();
            int pages = Math.Max(1, (items.Count + 49) / 50); _itemPage = Mathf.Clamp(_itemPage, 0, pages - 1);
            AddDashboardText(Text("Item observations: ", "物品观察：") + items.Count + " | " + (_itemPage + 1) + " / " + pages, 20);
            RectTransform pager = DashboardRow("Pagination", 44);
            StateKeeperButton previous = CreateButton(pager, "Previous", Text("PREVIOUS", "上一页"));
            previous.SetCompactText(Text("PREVIOUS", "上一页")); Anchor(previous.RectTransform, 0, .5f, 0, .5f, 0, 0, 160, 40);
            previous.SetInteractable(_itemPage > 0); previous.AddListener(delegate { _itemPage--; RenderDetails(); _dashboardScroll.verticalNormalizedPosition = 1; });
            StateKeeperButton next = CreateButton(pager, "Next", Text("NEXT", "下一页"));
            next.SetCompactText(Text("NEXT", "下一页")); Anchor(next.RectTransform, 1, .5f, 1, .5f, 0, 0, 160, 40);
            next.SetInteractable(_itemPage + 1 < pages); next.AddListener(delegate { _itemPage++; RenderDetails(); _dashboardScroll.verticalNormalizedPosition = 1; });
            foreach (AnalysisItemObservation item in items.Skip(_itemPage * 50).Take(50))
            {
                RectTransform row = DashboardRow("ItemObservation", 104);
                TextMeshProUGUI text = CreateText(row, "ItemSummary", 18, TextAlignmentOptions.Left);
                Stretch(text.rectTransform); text.rectTransform.offsetMin = new Vector2(66, 0); text.rectTransform.offsetMax = new Vector2(-135, 0); text.raycastTarget = false;
                string actor = item.actorPlayerIndex >= 0 ? PlayerName(item.actorPlayerIndex) : PlayerName(item.playerIndex);
                text.text = FormatDuration(item.time) + "  " + ItemDisplayName(item.itemName) + "  |  " + ItemKindName(item.kind) + "\n" +
                    actor + (item.actorInferred ? Text(" (inferred)", "（推断）") : "") + (item.targetPlayerIndex >= 0 ? " -> " + PlayerName(item.targetPlayerIndex) : "") +
                    (string.IsNullOrEmpty(item.resourceKey) ? "" : " | " + item.resourceKey + " " + item.previousValue.ToString("0.###") + " -> " + item.value.ToString("0.###")) +
                    "\n" + Text("Observation: ", "事实：") + ConfidenceText(item.confidence) + " | " + Text("Effect attribution: ", "效果归因：") + ConfidenceText(item.attribution);
                AddItemIcon(row, item);
                StateKeeperButton details = CreateButton(row, "ItemEvidence", Text("EVIDENCE", "证据")); details.SetCompactText(Text("EVIDENCE", "证据"));
                Anchor(details.RectTransform, 1, .5f, 1, .5f, 0, 0, 120, 38);
                details.AddListener(delegate { ShowItemEvidence(item); });
            }
            if (items.Count == 0) AddDashboardText(Text("No matching observations", "没有符合条件的记录"), 20);
        }

        private static string ItemDisplayName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return Text("Unknown item", "未知物品");
            string key = LocalizedText.GetNameIndex(name);
            string translated = LocalizedText.GetText(key, true);
            return string.IsNullOrWhiteSpace(translated) || translated == key || translated.StartsWith("LOC: ", StringComparison.Ordinal) ? name : translated;
        }

        private static string ItemKindName(string kind)
        {
            if (kind == "InventoryUnplaced") return Text("Inventory observed; clock interval uncertain", "库存观察（时钟区间不确定）");
            if (kind == "PassiveEffectObserved") return Text("Enabled passive effect", "启用的持续效果");
            if (kind == "PlayerFriendHealed") return Text("Observed treatment", "直接观察到治疗");
            string[] en = { "ItemAppeared", "ItemRemovedObserved", "ItemMoved", "ItemTransferObserved", "ResourceChanged", "ItemConsumed", "ItemFedToPlayer", "ItemPrimaryCastFinished", "ItemSecondaryCastFinished", "ItemPickedUp" };
            string[] zh = { "进入物品栏", "离开物品栏（原因未知）", "换槽", "可能转交", "资源变化", "消耗", "被喂物品", "主要施放完成", "次要施放完成", "拾取" };
            int i = Array.IndexOf(en, kind); return i < 0 ? kind : Text(kind, zh[i]);
        }

        private static void AddItemIcon(Transform row, AnalysisItemObservation observation)
        {
            ItemDatabase database = SingletonAsset<ItemDatabase>.Instance;
            Item prefab;
            if (database == null || database.itemLookup == null || !database.itemLookup.TryGetValue(observation.itemId, out prefab) || prefab == null || prefab.UIData == null || prefab.UIData.itemName != observation.itemName) return;
            Texture2D texture = prefab.UIData.icon;
            if (texture == null) return;
            var icon = new GameObject("ItemIcon", typeof(RectTransform), typeof(RawImage)); icon.transform.SetParent(row, false);
            Anchor(icon.GetComponent<RectTransform>(), 0, .5f, 0, .5f, 0, 0, 52, 52);
            icon.GetComponent<RawImage>().texture = texture; icon.GetComponent<RawImage>().raycastTarget = false;
        }

        private static void ShowItemEvidence(AnalysisItemObservation item)
        {
            ClearDashboard();
            RectTransform row = DashboardRow("EvidenceBack", 44);
            StateKeeperButton back = CreateButton(row, "EvidenceBack", Text("ITEM LIST", "返回物品列表")); back.SetCompactText(Text("ITEM LIST", "返回物品列表"));
            Anchor(back.RectTransform, 0, .5f, 0, .5f, 0, 0, 210, 40); back.AddListener(RenderDetails);
            AddDashboardText(ItemDisplayName(item.itemName) + " | " + FormatDuration(item.time) + " | " + ItemKindName(item.kind), 24);
            AddDashboardText(Text("Observed effects", "观察结果") + " [" + ConfidenceText(item.attribution) + "]", 20);
            if (item.possibleRescue) AddDashboardText(Text("Possible rescue; excluded from direct rescue counts", "可能帮助脱离昏迷；不计入直接救援次数"), 18);
            else if (item.possibleHelp) AddDashboardText(Text("Evidence of help to another player", "有帮助其他玩家的关联证据"), 18);
            foreach (string reason in item.attributionReasons.Distinct()) AddDashboardText(AttributionReason(reason), 18);
            if (item.observedEffects.Count == 0) AddDashboardText(Text("No linked state change recorded", "没有关联到状态变化"), 18);
            foreach (AnalysisEffectChange effect in item.observedEffects)
            {
                AddDashboardText(PlayerName(effect.playerIndex) + " | " + StatusName(effect.channel) + " " + effect.previousValue.ToString("0.###") + " -> " + effect.value.ToString("0.###") +
                    " | " + FormatDuration(effect.beforeTime) + " -> " + FormatDuration(effect.afterTime) + " | " + ConfidenceText(effect.attribution) +
                    "\n" + string.Join("; ", effect.reasons.Select(AttributionReason)), 18);
                if (effect.candidateGroupIds.Count > 1)
                {
                    IEnumerable<string> candidates = effect.candidateGroupIds.Select(id => _displayedAnalysis.items.FirstOrDefault(i => i.attributionGroupId == id))
                        .Where(i => i != null).Select(i => ItemDisplayName(i.itemName) + " (" + PlayerName(i.actorPlayerIndex) + ", " + FormatDuration(i.time) + ")");
                    AddDashboardText(Text("Candidates: ", "候选：") + string.Join(" / ", candidates), 18);
                }
            }
            AddDashboardText(Text("Applied rules", "匹配规则"), 20);
            foreach (AnalysisEffectRule rule in item.rules)
                AddDashboardText(StatusName(rule.channel) + " | " + (rule.amount.HasValue ? rule.amount.Value.ToString("0.###") : Text("amount unknown", "数值未知")) +
                    " | " + Text("delay ", "时延 ") + rule.delay.ToString("0.##") + "s" + (rule.continuous ? Text(" / continuous", " / 持续") : "") +
                    " | " + rule.source + (rule.uncertain ? Text(" | incomplete parameters", " | 参数不全") : ""), 18);
            AddDashboardText(Text("Recorded item definition", "记录时的物品定义"), 20);
            ItemDefinition definition = RunAnalysisEngine.Definition(_displayedAnalysis.definitions, item.itemId, item.prefabName);
            if (definition == null || !definition.actions.Any(a => a.parameters != null && a.parameters.Count > 0))
                AddDashboardText(Text("Structured parameters were not collected", "结构化参数：记录时未采集"), 18);
            if (definition != null) foreach (ItemDefinitionAction action in definition.actions)
                AddDashboardText(action.typeName + " | " + action.trigger + "\n" + action.parameterSummary, 18);
            AddDashboardText(Text("Instance history", "实例轨迹"), 20);
            if (!string.IsNullOrEmpty(item.itemGuid)) foreach (AnalysisItemObservation evidence in _displayedAnalysis.items.Where(e => e.epoch == item.epoch && e.itemGuid == item.itemGuid).Take(100))
                AddDashboardText(FormatDuration(evidence.time) + " | " + PlayerName(evidence.playerIndex) + " | " + ItemKindName(evidence.kind), 18);
            AddDashboardText("GUID: " + (item.itemGuid ?? Text("not recorded", "未采集")), 16);
            _dashboardScroll.verticalNormalizedPosition = 1;
            RefreshDashboardNavigation();
        }

        private static string AttributionReason(string reason)
        {
            if (reason == "UnknownItemCompetition") return Text("Another item has unknown effects", "同期其他物品的作用未知，不能排除竞争解释");
            if (reason == "SpatialBoundaryUncertain") return Text("Near the uncertain range boundary", "接近作用范围边界，位置/同步误差可能影响判断");
            string[] en = { "CompetingItems", "CompetingRecipients", "NaturalRecoveryPossible", "SharedEnvironmentRecoveryPossible", "CheckpointRecoveryPossible",
                "DisappearanceOnly", "MovedOrTransferred", "ConflictingGuid", "InstanceUnknown", "ObservationInterrupted", "IncompleteWindow",
                "DeathRevivalOrDiscontinuity", "TransformedCharacter", "SpatialEvidenceUnavailable", "ItemOriginNotRecorded", "AmountNotRecorded",
                "AmountOrBudgetMismatch", "HealingPriorityMismatch", "IncompleteRuleParameters", "RequiredAfflictionNotObserved", "DefinitionNotRecorded",
                "StatusTypeNotRecorded", "NestedParametersNotRecorded", "RandomContextNotRecorded", "CookingStateUnknown", "CookingRulesNotRecorded",
                "CustomCookingRules", "WreckedItem", "NoObservedChange", "ObservedWithoutMatchingRule", "NoItemCandidate", "CompatibleObservedChange",
                "DirectStatusObservation", "NoObservableEffectRule", "EffectNotObservableInTelemetry", "TruncatedParameters", "UnknownAfflictionType" };
            string[] zh = { "有多个物品候选，不能唯一归因", "有多个可能受益者", "可能是状态自然恢复", "多人同时恢复，可能有共同环境因素", "靠近山段/营地事件，可能是营地恢复",
                "只有库存消失，不能确认使用", "实例随后换槽或转交", "同一实例出现持有冲突", "缺少物品实例标识", "观察被中断", "观察窗口不完整",
                "附近有死亡、复活或位置断点", "角色处于特殊形态", "空间证据不足", "未采集范围作用的物品中心位置", "未采集理论变化量",
                "变化超过剩余作用量或治疗预算", "不符合治疗状态的优先顺序", "规则参数不完整", "没有观察到所需的持续状态", "未采集物品定义",
                "未采集受影响的状态类型", "未采集嵌套作用参数", "未采集当时的随机蘑菇配置", "烹饪状态未知", "烹饪规则未知",
                "存在自定义烹饪规则", "物品已烹饪损坏", "没有观察到变化", "观察到变化，但未匹配明确规则", "没有匹配的物品候选", "观察结果与规则相符",
                "直接观察到实际状态变化", "没有适用的可观测作用规则", "该效果不在当前遥测通道中", "记录的参数被截断", "未知的持续状态类型" };
            int index = Array.IndexOf(en, reason);
            if (index >= 0) return Text(reason, zh[index]);
            if (reason != null && reason.StartsWith("UnsupportedAction:", StringComparison.Ordinal)) return Text("Uninterpreted action: ", "未解释的 Action：") + reason.Substring(18);
            return reason ?? "";
        }

        private static void RefreshDashboardNavigation()
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(_dashboardContent.GetComponent<RectTransform>());
            Selectable[] controls = _detailsPage.GetComponentsInChildren<Selectable>(false).Where(s => s.IsInteractable()).ToArray();
            for (int i = 0; i < controls.Length; i++)
            {
                Selectable previous = controls[(i + controls.Length - 1) % controls.Length];
                Selectable next = controls[(i + 1) % controls.Length];
                Navigation nav = controls[i].navigation;
                nav.mode = Navigation.Mode.Explicit; nav.selectOnUp = previous; nav.selectOnDown = next;
                nav.selectOnLeft = previous; nav.selectOnRight = next;
                // Left/right adjusts the selected time slider; up/down changes focus.
                if (controls[i] is Slider || controls[i] is TMP_InputField) nav.selectOnLeft = nav.selectOnRight = null;
                controls[i].navigation = nav;
                if (controls[i].transform.IsChildOf(_dashboardContent.transform))
                {
                    StateKeeperScrollFocus focus = controls[i].GetComponent<StateKeeperScrollFocus>() ?? controls[i].gameObject.AddComponent<StateKeeperScrollFocus>();
                    focus.Scroll = _dashboardScroll;
                }
            }
            if (EventSystem.current != null && controls.Length > 0 &&
                (EventSystem.current.currentSelectedGameObject == null || !EventSystem.current.currentSelectedGameObject.activeInHierarchy))
                EventSystem.current.SetSelectedGameObject(controls[0].gameObject);
        }
    }
}
