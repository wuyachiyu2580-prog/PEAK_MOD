using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace StateKeeper
{
    // Only recorded data is interpreted here. No runtime prefab, Action or Unity API is invoked.
    internal sealed class RecordedItemRules
    {
        private readonly RunRecord _record;
        internal static readonly string[] HealingOrder = { "Injury", "Spores", "Poison", "Cold", "Hot", "Drowsy" };
        internal RecordedItemRules(RunRecord record) { _record = record; }

        internal List<AnalysisEffectRule> Compile(AnalysisItemObservation row, List<string> reasons)
        {
            ItemDefinition definition = RunAnalysisEngine.Definition(_record.definitions ?? new List<ItemDefinition>(), row.itemId, row.prefabName);
            var rules = new List<AnalysisEffectRule>();
            if (definition == null) { reasons.Add("DefinitionNotRecorded"); return rules; }
            int actionIndex = 0;
            foreach (ItemDefinitionAction action in definition.actions ?? new List<ItemDefinitionAction>())
            {
                if (action == null) continue;
                var p = new Parameters(action.parameters, action.parameterSummary);
                string source = action.typeName + "#" + actionIndex++;
                int first = rules.Count;
                switch (action.typeName)
                {
                    case "Action_RestoreHunger": Add(rules, "Hunger", Negative(p.Number("restorationAmount")), -1, source); break;
                    case "Action_GiveExtraStamina": Add(rules, "ExtraStamina", p.Number("amount"), 1, source); break;
                    case "Action_ApplyInfiniteStamina": Add(rules, "Affliction:InfiniteStamina", 1, 1, source); break;
                    case "Action_ModifyStatus":
                        string status = p.String("statusType"); float? amount = p.Number("changeAmount");
                        if (!string.IsNullOrEmpty(status) && amount.HasValue) Add(rules, status, amount, Math.Sign(amount.Value), source);
                        else reasons.Add("StatusTypeNotRecorded");
                        if (p.Boolean("ifSkeleton") == true || !p.Boolean("ifSkeleton").HasValue)
                            foreach (var rule in rules.Skip(first)) rule.uncertain = true;
                        break;
                    case "Action_ClearAllStatus":
                        foreach (string channel in _record.statusTypeOrder ?? new string[0])
                            if (!new[] { "Weight", "Petrify", "Arrow", "Thorns" }.Contains(channel) && (channel != "Curse" || p.Boolean("excludeCurse") == false) &&
                                !(p.Entry("otherExclusions")?.children ?? new List<ItemDefinitionParameter>()).Any(v => v.value == channel))
                            { var clear = Add(rules, channel, -5, -1, source); clear.uncertain = p.Entry("otherExclusions") == null; }
                        break;
                    case "Action_InflictPoison":
                        Add(rules, "Affliction:PoisonOverTime", 1, 1, source);
                        var poison = Add(rules, "Poison", p.Number("poisonPerSecond"), 1, source);
                        poison.delay = p.Number("delay") ?? 0; poison.duration = p.Number("inflictionTime") ?? 0;
                        poison.continuous = true; poison.requiredAffliction = "PoisonOverTime";
                        poison.uncertain = !p.Number("delay").HasValue || poison.duration <= 0;
                        break;
                    case "Action_ApplyAffliction":
                    case "Action_ApplyMassAffliction":
                        CompileAffliction(p.Entry("affliction"), rules, source, 0, reasons);
                        foreach (var child in p.Entry("extraAfflictions")?.children ?? new List<ItemDefinitionParameter>()) CompileAffliction(child, rules, source, 0, reasons);
                        if (action.typeName == "Action_ApplyMassAffliction") foreach (var rule in rules.Skip(first))
                        { rule.scope = "Area"; rule.radius = p.Number("radius"); rule.ignoreCaster = p.Boolean("ignoreCaster") ?? false; rule.uncertain |= !rule.radius.HasValue; }
                        break;
                    case "Peak.Action_HealingGem":
                        CompileAffliction(p.Entry("healingAffliction"), rules, source, 0, reasons);
                        CompileAffliction(p.Entry("invincibilityAffliction"), rules, source, 0, reasons);
                        if (rules.Count == first) foreach (string channel in HealingOrder) { var rule = Add(rules, channel, null, -1, source); rule.uncertain = true; }
                        var petrify = Add(rules, "Petrify", null, 1, source); petrify.scope = "Actor"; petrify.uncertain = true;
                        break;
                    case "Action_RandomMushroomEffect": Mushroom(p, rules, row.time, source, reasons); break;
                    default:
                        // Unknown actions are not guessed from name substrings or English item labels.
                        if (!NonEffectAction(action.typeName)) reasons.Add("UnsupportedAction:" + action.typeName);
                        break;
                }
                foreach (var rule in rules.Skip(first)) { rule.trigger = action.trigger; if (!RunAnalysisEngine.Finite(rule.delay) || !RunAnalysisEngine.Finite(rule.duration) || rule.delay < 0 || rule.duration < 0) rule.uncertain = true; }
            }
            Cook(definition, row.cookedAmount, rules, reasons);
            // Multiple actions on the same channel are additive, not competing uses of the same item.
            return rules.GroupBy(r => r.channel + "|" + r.delay + "|" + r.duration + "|" + r.scope + "|" + r.trigger + "|" + r.budgetGroup + "|" + r.continuous + "|" + r.requiredAffliction + "|" + r.afterAfflictionEnds + "|" + r.direction + "|" + r.radius + "|" + r.ignoreCaster + "|" + r.priority)
                .Select(group =>
                {
                    AnalysisEffectRule rule = group.First();
                    if (group.Count() > 1 && group.All(r => r.amount.HasValue)) rule.amount = group.Sum(r => r.amount.Value);
                    rule.uncertain |= group.Any(r => r.uncertain); return rule;
                }).ToList();
        }

        internal List<AnalysisEffectRule> Passive(ItemDefinition definition)
        {
            var rules = new List<AnalysisEffectRule>();
            foreach (var component in definition?.components ?? new List<ItemDefinitionComponent>())
            {
                var parameters = new Parameters(component.parameters, component.parameterSummary);
                if (component.typeName == "Peak.InfiniteStamAmulet")
                {
                    var aura = Add(rules, "Affliction:InfiniteStamina", 1, 1, "PEAK2.4.b:InfiniteStamAmulet");
                    aura.scope = "PlayerArea"; aura.radius = parameters.Number("radius"); aura.continuous = true; aura.uncertain = !aura.radius.HasValue;
                    var petrify = Add(rules, "Petrify", parameters.Number("petrifyPerSecond") / 100f, 1, "PEAK2.4.b:InfiniteStamAmulet");
                    petrify.continuous = true; petrify.scope = "Actor";
                }
                if (component.typeName == "Peak.DoubleJumpAmulet")
                {
                    var doubleJump = Add(rules, "Affliction:DoubleJumpAmulet", 1, 1, "PEAK2.4.b:DoubleJumpAmulet"); doubleJump.continuous = true;
                    var petrify = Add(rules, "Petrify", parameters.Number("petrifyPerJump") / 100f, 1, "PEAK2.4.b:DoubleJumpAmulet");
                    petrify.continuous = true; petrify.uncertain = true;
                }
            }
            if (!(definition?.components ?? new List<ItemDefinitionComponent>()).Any(c => c.typeName == "Peak.HealingAmulet")) return rules;
            string[] order = { "Injury", "Poison", "Spores", "Cold", "Drowsy", "Hot" };
            for (int i = 0; i < order.Length; i++)
            {
                var r = Add(rules, order[i], -.05f, -1, "PEAK2.4.b:HealingAmulet");
                r.continuous = true; r.budgetGroup = "HealingAmuletTick"; r.priority = i; r.budget = .05f;
            }
            var stone = Add(rules, "Petrify", .02f, 1, "PEAK2.4.b:HealingAmulet"); stone.continuous = true;
            return rules;
        }

        private static AnalysisEffectRule Add(List<AnalysisEffectRule> rules, string channel, float? amount, int direction, string source)
        {
            var rule = new AnalysisEffectRule { channel = channel, amount = amount, direction = amount.HasValue ? Math.Sign(amount.Value) : direction, source = source };
            rules.Add(rule); return rule;
        }
        private static float? Negative(float? number) { return number.HasValue ? -Math.Abs(number.Value) : (float?)null; }

        private static void CompileAffliction(ItemDefinitionParameter entry, List<AnalysisEffectRule> rules, string source, float delay, List<string> reasons, int depth = 0)
        {
            if (entry == null) { reasons.Add("NestedParametersNotRecorded"); return; }
            if (depth > 4 || entry.truncated) { reasons.Add("TruncatedParameters"); return; }
            string type = entry.type?.StartsWith("Peak.Afflictions.Affliction_", StringComparison.Ordinal) == true ? entry.type.Substring("Peak.Afflictions.Affliction_".Length) : null;
            // Earlier catalogs stored the declared type in `type` and the actual affliction type in `value`.
            if (type == null && entry.value?.StartsWith("Affliction_", StringComparison.Ordinal) == true) type = entry.value.Substring("Affliction_".Length);
            if (type == null) { reasons.Add("UnknownAfflictionType"); return; }
            var p = new Parameters(entry.children, null);
            var marker = Add(rules, "Affliction:" + (type == "AdjustDrowsyOverTime" ? "DrowsyOverTime" : type == "AdjustColdOverTime" ? "ColdOverTime" : type == "Exhaustion" ? "Exhausted" : type), 1, 1, source);
            marker.delay = delay;
            int first = rules.Count;
            switch (type)
            {
                case "HealAll":
                    for (int i = 0; i < HealingOrder.Length; i++) { var r = Add(rules, HealingOrder[i], Negative(p.Number("maxHealing")), -1, source); r.budgetGroup = source + ":HealAll"; r.budget = p.Number("maxHealing"); r.priority = i; r.uncertain = !r.budget.HasValue; }
                    break;
                case "ClearAllStatus":
                    foreach (string channel in HealingOrder.Concat(new[] { "Hunger" })) Add(rules, channel, -2, -1, source);
                    break;
                case "AdjustStatus":
                    if (p.String("statusType") != null && p.Number("statusAmount").HasValue) Add(rules, p.String("statusType"), p.Number("statusAmount"), 0, source);
                    else reasons.Add("NestedParametersNotRecorded");
                    break;
                case "AddBonusStamina": Add(rules, "ExtraStamina", p.Number("staminaAmount"), 1, source); break;
                case "AdjustDrowsyOverTime":
                case "AdjustColdOverTime":
                case "PoisonOverTime":
                    var rate = Add(rules, type == "AdjustDrowsyOverTime" ? "Drowsy" : type == "AdjustColdOverTime" ? "Cold" : "Poison", p.Number("statusPerSecond"), 0, source);
                    rate.delay = p.Number("delayBeforeEffect") ?? 0; rate.duration = p.Number("totalTime") ?? 0;
                    rate.continuous = true; rate.requiredAffliction = marker.channel.Substring(11); rate.uncertain = !rate.amount.HasValue || rate.duration <= 0;
                    break;
                case "FasterBoi":
                    Add(rules, "Drowsy", -.5f, -1, source);
                    var removal = Add(rules, "Drowsy", p.Number("drowsyOnEnd"), 1, source);
                    removal.afterAfflictionEnds = "FasterBoi";
                    removal.uncertain = !removal.amount.HasValue;
                    break;
                case "InfiniteStamina":
                    if (p.Entry("drowsyAffliction") != null)
                    {
                        int begin = rules.Count;
                        CompileAffliction(p.Entry("drowsyAffliction"), rules, source, 0, reasons, depth + 1);
                        foreach (var endRule in rules.Skip(begin)) endRule.afterAfflictionEnds = "InfiniteStamina";
                    }
                    break;
            }
            foreach (var rule in rules.Skip(first)) rule.delay += delay;
        }

        private void Mushroom(Parameters p, List<AnalysisEffectRule> rules, float time, string source, List<string> reasons)
        {
            int effect = -1;
            if (p.Boolean("useDebugEffect") == true && p.Number("debugEffect").HasValue) effect = (int)p.Number("debugEffect").Value;
            else
            {
                RunEffectContext context = (_record.effectContexts ?? new List<RunEffectContext>()).Where(c => c != null && c.observedTime <= time).OrderBy(c => c.observedTime).LastOrDefault();
                int index = (int)(p.Number("mushroomTypeIndex") ?? -1);
                if (context?.mushroomEffects == null || context.mushroomEffects.Length == 0 || index < 0) { reasons.Add("RandomContextNotRecorded"); return; }
                index %= context.mushroomEffects.Length; effect = context.mushroomEffects[index];
                if (context.mushroomStaminaAmounts != null && index < context.mushroomStaminaAmounts.Length) Add(rules, "ExtraStamina", context.mushroomStaminaAmounts[index] * .05f, 1, source);
            }
            string[] effects = { "InfiniteStamina", "FasterBoi", "LowGravity", "Invincibility" };
            if (effect >= 0 && effect <= 3) { var r = Add(rules, "Affliction:" + effects[effect], 1, 1, source); r.delay = effect == 3 ? 0 : 3; }
            else if (effect == 4) foreach (string channel in new[] { "Hunger", "Injury", "Poison" }) Add(rules, channel, -.15f, -1, source);
            else if (effect == 6 || effect == 9) { var r = Add(rules, "Affliction:" + (effect == 6 ? "Blind" : "Numb"), 1, 1, source); r.delay = 3; }
            else if (effect == 8) Add(rules, "Spores", .25f, 1, source);
            else reasons.Add("EffectNotObservableInTelemetry");
        }

        private static void Cook(ItemDefinition definition, int? cooked, List<AnalysisEffectRule> rules, List<string> reasons)
        {
            if (definition.cookingRules == null || definition.cookingRules.Count == 0) return;
            if (!cooked.HasValue) { reasons.Add("CookingStateUnknown"); foreach (var r in rules) r.uncertain = true; return; }
            if (cooked <= 0) return;
            foreach (var cooking in definition.cookingRules)
            {
                var p = new Parameters(cooking.parameters, cooking.parameterSummary);
                if (cooking.typeName != "ItemCooking" || p.Entry("additionalCookingBehaviors")?.children?.Count > 0)
                { reasons.Add("CustomCookingRules"); foreach (var r in rules) r.uncertain = true; continue; }
                if (p.Boolean("wreckWhenCooked") == true) { rules.Clear(); reasons.Add("WreckedItem"); return; }
                if (p.Boolean("ignoreDefaultCookBehavior") == true) continue;
                if (!p.Boolean("ignoreDefaultCookBehavior").HasValue) { foreach (var r in rules) r.uncertain = true; reasons.Add("CookingRulesNotRecorded"); continue; }
                var hunger = rules.FirstOrDefault(r => r.source.StartsWith("Action_RestoreHunger#", StringComparison.Ordinal));
                if (hunger?.amount != null) hunger.amount = -Math.Max(0, Math.Abs(hunger.amount.Value) * 2 - Math.Max(0, cooked.Value - 2) * .05f);
                var extra = rules.FirstOrDefault(r => r.source.StartsWith("Action_GiveExtraStamina#", StringComparison.Ordinal));
                if (extra == null) { extra = Add(rules, "ExtraStamina", 0, 1, "ItemCooking"); extra.trigger = "OnConsumed"; }
                extra.amount = cooked <= 2 ? Math.Max(.1f, (extra.amount ?? 0) * 1.5f) : 0; extra.direction = extra.amount > 0 ? 1 : 0;
                if (cooked >= 4 && p.Boolean("ignoreDefaultPoisonBehavior") == false)
                { var poison = Add(rules, "Poison", .1f * (cooked.Value - 3), 1, "ItemCooking"); poison.trigger = "OnConsumed"; }
            }
        }

        private static bool NonEffectAction(string type)
        {
            return new[] { "Action_Consume", "Action_ReduceUses", "Action_PlayAnimation", "Action_PlaySound", "Action_PlaySFX", "Action_PlayParticles", "Action_StrangeGem", "Action_RopeShooter", "Action_Shoot", "Action_ApplySuperJump" }.Contains(type);
        }

        internal sealed class Parameters
        {
            private readonly List<ItemDefinitionParameter> _entries;
            private readonly string _legacy;
            internal Parameters(List<ItemDefinitionParameter> entries, string legacy) { _entries = entries ?? new List<ItemDefinitionParameter>(); _legacy = legacy; }
            internal ItemDefinitionParameter Entry(string name) { return _entries.FirstOrDefault(e => e != null && e.name == name); }
            internal string String(string name)
            {
                ItemDefinitionParameter entry = Entry(name); if (entry != null) return entry.truncated ? null : entry.value;
                // Legacy summaries have no enums/nested values. Parse only an exact, unambiguous scalar field.
                if (string.IsNullOrEmpty(_legacy)) return null;
                MatchCollection matches = Regex.Matches(_legacy, "(?:^|,)" + Regex.Escape(name) + "=([^,\\[\\]]+)(?=,|$)");
                return matches.Count == 1 ? matches[0].Groups[1].Value : null;
            }
            internal float? Number(string name) { float value; return float.TryParse(String(name), NumberStyles.Float, CultureInfo.InvariantCulture, out value) && RunAnalysisEngine.Finite(value) ? value : (float?)null; }
            internal bool? Boolean(string name) { bool value; return bool.TryParse(String(name), out value) ? value : (bool?)null; }
        }
    }
}
