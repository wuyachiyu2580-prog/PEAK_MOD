using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Zorro.Core;

namespace StateKeeper
{
    internal static class ItemDefinitionCatalog
    {
        internal static List<ItemDefinition> Capture()
        {
            var result = new List<ItemDefinition>();
            try
            {
                if (SingletonAsset<ItemDatabase>.Instance == null || SingletonAsset<ItemDatabase>.Instance.itemLookup == null)
                    return result;
                foreach (KeyValuePair<ushort, Item> pair in SingletonAsset<ItemDatabase>.Instance.itemLookup)
                {
                    if (pair.Value == null) continue;
                    try { result.Add(Capture(pair.Key, pair.Value)); }
                    catch (Exception ex)
                    {
                        if (StateKeeperPlugin.IsDebugLogging) StateKeeperPlugin.LogException("capture item definition " + pair.Key, ex);
                    }
                }
            }
            catch (Exception ex)
            {
                if (StateKeeperPlugin.IsDebugLogging) StateKeeperPlugin.LogException("capture item definitions", ex);
            }
            return result.OrderBy(definition => definition.itemId).ToList();
        }

        private static ItemDefinition Capture(ushort id, Item item)
        {
            var definition = new ItemDefinition
            {
                itemId = id,
                itemName = item.UIData == null ? item.name : item.UIData.itemName,
                prefabName = item.gameObject == null ? item.name : item.gameObject.name,
                totalUses = item.totalUses,
                usingTimePrimary = item.usingTimePrimary,
                itemTags = Enum.GetValues(typeof(Item.ItemTags)).Cast<Item.ItemTags>()
                    .Where(tag => tag != Item.ItemTags.None && item.itemTags.HasFlag(tag))
                    .Select(tag => tag.ToString()).ToList()
            };

            foreach (ItemActionBase action in item.GetComponentsInChildren<ItemActionBase>(false))
            {
                if (action == null) continue;
                List<ItemDefinitionParameter> parameters = DescribePublicParameters(action);
                definition.actions.Add(new ItemDefinitionAction
                {
                    typeName = action.GetType().FullName,
                    trigger = DescribeTriggers(action),
                    parameterSummary = DescribeParameters(parameters),
                    parameters = parameters
                });
                AddEffectHints(definition.effectHints, action);
            }
            foreach (ItemComponent component in item.GetComponentsInChildren<ItemComponent>(false))
            {
                if (component == null) continue;
                List<ItemDefinitionParameter> parameters = DescribePublicParameters(component);
                if (component is ItemCooking cooking)
                {
                    definition.cookingRules.Add(new ItemDefinitionCooking
                    {
                        typeName = cooking.GetType().FullName,
                        parameterSummary = DescribeParameters(parameters),
                        parameters = parameters
                    });
                }
                else
                {
                    definition.components.Add(new ItemDefinitionComponent
                    {
                        typeName = component.GetType().FullName,
                        parameterSummary = DescribeParameters(parameters),
                        parameters = parameters
                    });
                }
            }
            return definition;
        }

        private static string DescribeTriggers(ItemActionBase action)
        {
            if (!(action is ItemAction)) return string.Empty;
            var values = new List<string>();
            foreach (FieldInfo field in typeof(ItemAction).GetFields(BindingFlags.Instance | BindingFlags.Public))
            {
                if (field.FieldType == typeof(bool) && (bool)field.GetValue(action)) values.Add(field.Name);
            }
            return string.Join(",", values);
        }

        private static List<ItemDefinitionParameter> DescribePublicParameters(object value, int depth = 0)
        {
            var result = new List<ItemDefinitionParameter>();
            foreach (FieldInfo field in value.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public))
            {
                try
                {
                    object fieldValue = field.GetValue(value);
                    string text;
                    if (!TryDescribeValue(fieldValue, field.FieldType, out text)) continue;
                    result.Add(new ItemDefinitionParameter
                    {
                        name = field.Name,
                        type = field.FieldType.FullName,
                        value = text,
                        children = DescribeChildren(fieldValue, depth + 1),
                        truncated = depth >= 4
                    });
                }
                catch { }
            }
            return result;
        }

        private static List<ItemDefinitionParameter> DescribeChildren(object value, int depth)
        {
            if (value == null || depth > 4 || value is UnityEngine.Object || value is string) return null;
            if (value is IEnumerable list)
            {
                var children = new List<ItemDefinitionParameter>();
                foreach (object entry in list)
                {
                    if (children.Count >= 64) { children.Add(new ItemDefinitionParameter { name = "truncated", truncated = true }); break; }
                    string text;
                    children.Add(new ItemDefinitionParameter { name = children.Count.ToString(), type = entry?.GetType().FullName, value = entry == null ? null : TryDescribeValue(entry, entry.GetType(), out text) ? text : entry.GetType().Name, children = DescribeChildren(entry, depth + 1) });
                }
                return children;
            }
            if (value is Peak.Afflictions.Affliction || value is AdditionalCookingBehavior) return DescribePublicParameters(value, depth);
            return null;
        }

        private static string DescribeParameters(IEnumerable<ItemDefinitionParameter> parameters)
        {
            return string.Join(",", parameters.Select(parameter => parameter.name + "=" + parameter.value));
        }

        private static bool TryDescribeValue(object value, Type declaredType, out string text)
        {
            text = null;
            if (value == null) return false;
            Type type = value.GetType();
            if (type.IsEnum || type == typeof(bool) || type == typeof(byte) || type == typeof(short) ||
                type == typeof(ushort) || type == typeof(int) || type == typeof(uint) || type == typeof(long) ||
                type == typeof(float) || type == typeof(double) || type == typeof(decimal) || type == typeof(string))
            {
                text = Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture);
                return true;
            }
            if (value is UnityEngine.Object unityObject)
            {
                text = unityObject == null ? declaredType.Name : unityObject.GetType().Name + ":" + unityObject.name;
                return true;
            }
            if (value is IEnumerable enumerable && !(value is string))
            {
                var values = new List<string>();
                foreach (object entry in enumerable)
                {
                    if (values.Count >= 16) { values.Add("..."); break; }
                    if (entry == null) { values.Add("null"); continue; }
                    string entryText;
                    if (TryDescribeValue(entry, entry.GetType(), out entryText)) values.Add(entryText);
                    else values.Add(entry.GetType().Name);
                }
                text = "[" + string.Join("|", values) + "]";
                return true;
            }
            if (type.Namespace != null && type.Namespace.StartsWith("Peak.Afflictions", StringComparison.Ordinal))
            {
                text = type.Name;
                return true;
            }
            if (value is AdditionalCookingBehavior) { text = type.Name; return true; }
            return false;
        }

        private static void AddEffectHints(List<ItemEffectHint> result, ItemActionBase action)
        {
            string trigger = DescribeTriggers(action);
            if (action is Action_RestoreHunger hunger)
                result.Add(new ItemEffectHint { type = "RestoreStatus", target = "Hunger", hasAmount = true, amount = -Mathf.Abs(hunger.restorationAmount), trigger = trigger });
            else if (action is Action_GiveExtraStamina stamina)
                result.Add(new ItemEffectHint { type = "GiveExtraStamina", target = "ExtraStamina", hasAmount = true, amount = stamina.amount, trigger = trigger });
            else if (action is Action_ModifyStatus status)
                result.Add(new ItemEffectHint { type = "ModifyStatus", target = status.statusType.ToString(), hasAmount = true, amount = status.changeAmount, trigger = trigger });
            else if (action is Action_ApplyAffliction affliction)
                result.Add(new ItemEffectHint { type = "ApplyAffliction", target = affliction.affliction == null ? null : affliction.affliction.GetType().Name, trigger = trigger });
            else if (action is Action_ClearAllStatus)
                result.Add(new ItemEffectHint { type = "ClearStatuses", target = "Multiple", trigger = trigger });
            else if (action is Action_ReduceUses)
                result.Add(new ItemEffectHint { type = "ReduceUses", target = "ItemUses", trigger = trigger });
            else if (action.GetType().Name == "Action_Consume")
                result.Add(new ItemEffectHint { type = "ConsumeItem", target = "Item", trigger = trigger });
        }
    }
}
