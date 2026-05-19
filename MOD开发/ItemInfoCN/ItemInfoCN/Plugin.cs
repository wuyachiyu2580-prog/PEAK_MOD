using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using ItemInfoCN.Helpers;
using ItemInfoCN.Patches;
using Peak.Afflictions;
using TMPro;
using UnityEngine;

namespace ItemInfoCN
{
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    public partial class Plugin : BaseUnityPlugin
    {
        public const string PluginGUID = "com.wuyachiyu.ItemInfoCN";
        public const string PluginName = "ItemInfoCN";
        public const string PluginVersion = "1.0.0";

        internal static ManualLogSource Log;
        internal static GUIManager guiManager;
        internal static TextMeshProUGUI itemInfoDisplayTextMesh;
        internal static Dictionary<string, string> effectColors = new Dictionary<string, string>();
        internal static float lastKnownSinceItemAttach;
        internal static bool hasChanged;
        internal static bool EasyBackpack;

        internal static ConfigEntry<float> configFontSize;
        internal static ConfigEntry<float> configOutlineWidth;
        internal static ConfigEntry<float> configLineSpacing;
        internal static ConfigEntry<float> configSizeDeltaX;
        internal static ConfigEntry<float> configForceUpdateTime;

        private void Awake()
        {
            Log = Logger;
            InitEffectColors(effectColors);
            lastKnownSinceItemAttach = 0f;
            hasChanged = true;

            configFontSize = Config.Bind("ItemInfoDisplay", "Font Size", 20f,
                "Customize the Font Size for description text.");
            configOutlineWidth = Config.Bind("ItemInfoDisplay", "Outline Width", 0.08f,
                "Customize the Outline Width for item description text.");
            configLineSpacing = Config.Bind("ItemInfoDisplay", "Line Spacing", -35f,
                "Customize the Line Spacing for item description text.");
            configSizeDeltaX = Config.Bind("ItemInfoDisplay", "Size Delta X", 550f,
                "Customize the horizontal length of the container for the mod.");
            configForceUpdateTime = Config.Bind("ItemInfoDisplay", "Force Update Time", 1f,
                "Customize the time in seconds until the mod forces an update for the item.");

            Harmony.CreateAndPatchAll(typeof(ItemInfoUpdatePatch), null);
            Harmony.CreateAndPatchAll(typeof(ItemInfoEquipPatch), null);
            Harmony.CreateAndPatchAll(typeof(ItemInfoFinishCookingPatch), null);
            Harmony.CreateAndPatchAll(typeof(ItemInfoReduceUsesRPCPatch), null);

            Log.LogInfo($"Plugin {PluginName} v{PluginVersion} is loaded!");
        }

        internal static string GetEffectChineseName(string effect)
        {
            switch (effect)
            {
                case "Hunger":        return "饥饿值";
                case "Injury":        return "伤害";
                case "Curse":         return "诅咒";
                case "Cold":          return "寒冷";
                case "Hot":           return "烧伤";
                case "Heat":          return "烧伤";
                case "Shield":        return "护盾";
                case "Extra Stamina": return "额外体力";
                case "Thorns":        return "刺伤";
                case "Spores":        return "真菌感染";
                case "Poison":        return "中毒";
                case "Drowsy":        return "困倦";
                default:              return effect.ToUpper();
            }
        }

        internal static ComponentEffectInfo GetComponentEffectInfo(Component component)
        {
            Type componentType = component.GetType();
            var effectInfo = new ComponentEffectInfo { Component = component };

            if (componentType == typeof(Action_RestoreHunger))
            {
                var effect = (Action_RestoreHunger)component;
                effectInfo.Value = Mathf.Abs(effect.restorationAmount);
                effectInfo.EffectKey = "Action_RestoreHunger_Hunger";
            }
            else if (componentType == typeof(Action_GiveExtraStamina))
            {
                var effect = (Action_GiveExtraStamina)component;
                effectInfo.Value = Mathf.Abs(effect.amount);
                effectInfo.EffectKey = "Action_GiveExtraStamina_ExtraStamina";
            }
            else if (componentType == typeof(Action_InflictPoison))
            {
                var effect = (Action_InflictPoison)component;
                effectInfo.Value = Mathf.Abs(effect.poisonPerSecond * effect.inflictionTime);
                effectInfo.EffectKey = "Action_InflictPoison_Poison";
            }
            else if (componentType == typeof(Action_AddOrRemoveThorns))
            {
                var effect = (Action_AddOrRemoveThorns)component;
                effectInfo.Value = (float)effect.thornCount;
                effectInfo.EffectKey = "Action_AddOrRemoveThorns_Thorns";
            }
            else if (componentType == typeof(Action_ModifyStatus))
            {
                var effect = (Action_ModifyStatus)component;
                effectInfo.Value = Mathf.Abs(effect.changeAmount);
                effectInfo.EffectKey = $"Action_ModifyStatus_{effect.statusType}";
            }
            else
            {
                effectInfo.Value = 0f;
                effectInfo.EffectKey = componentType.Name;
            }

            return effectInfo;
        }

        internal static void AddDisplayObject()
        {
            EasyBackpack = Chainloader.PluginInfos.ContainsKey("nickklmao.easybackpack");

            // 前置判空：场景未就绪直接返回，不要中途报错。
            // 之前写成直接 GetComponent 会导致 NRE 被外层 try/catch 接住，
            // 每帧重试、每帧刷 LogError。
            GameObject guiManagerGameObj = GameObject.Find("GAME/GUIManager");
            if (guiManagerGameObj == null) return;

            var gm = guiManagerGameObj.GetComponent<GUIManager>();
            if (gm == null) return;
            if (gm.heroDayText == null || gm.heroDayText.font == null) return;

            Transform promptLayout = guiManagerGameObj.transform.Find("Canvas_HUD/Prompts/ItemPromptLayout");
            if (promptLayout == null) return;

            // 字体走四级兜底：AscentUI → heroDayText → 全场TMP → defaultFontAsset。
            // 规范见 memory/common/06_UI与字体规范.md。禁返 null、禁直接用 defaultFontAsset。
            TMP_FontAsset font = FontHelper.GetChineseCapable();
            if (font == null) font = gm.heroDayText.font;
            GameObject invGameObj = promptLayout.gameObject;
            GameObject itemInfoDisplayGameObj = new GameObject("ItemInfoDisplay");
            itemInfoDisplayGameObj.transform.SetParent(invGameObj.transform);
            var tm = itemInfoDisplayGameObj.AddComponent<TextMeshProUGUI>();
            RectTransform itemInfoDisplayRect = itemInfoDisplayGameObj.GetComponent<RectTransform>();
            itemInfoDisplayRect.sizeDelta = new Vector2(configSizeDeltaX.Value, 0f);
            tm.font = font;
            tm.fontSize = configFontSize.Value;
            tm.alignment = (TextAlignmentOptions)1025;
            tm.lineSpacing = configLineSpacing.Value;
            tm.text = "";
            tm.outlineWidth = configOutlineWidth.Value;

            // 所有实例化步骤都完成后再赋值 static 字段，
            // 保证“要么全有、要么全没”，不会留孤儿。
            itemInfoDisplayTextMesh = tm;
            guiManager = gm;
        }

        private static void InitEffectColors(Dictionary<string, string> dict)
        {
            dict.Add("Spores", "<#A45B62>");
            dict.Add("Hunger", "<#FFBD16>");
            dict.Add("Extra Stamina", "<#BFEC1B>");
            dict.Add("Injury", "<#FF5300>");
            dict.Add("Crab", "<#E13542>");
            dict.Add("Poison", "<#A139FF>");
            dict.Add("Cold", "<#00BCFF>");
            dict.Add("Heat", "<#C80918>");
            dict.Add("Hot", "<#C80918>");
            dict.Add("Sleepy", "<#FF5CA4>");
            dict.Add("Drowsy", "<#FF5CA4>");
            dict.Add("Curse", "<#1B0043>");
            dict.Add("Weight", "<#A65A1C>");
            dict.Add("Thorns", "<#768E00>");
            dict.Add("Shield", "<#D48E00>");
            dict.Add("ItemInfoDisplayPositive", "<#DDFFDD>");
            dict.Add("ItemInfoDisplayNegative", "<#FFCCCC>");
        }

        internal class ComponentEffectInfo
        {
            public Component Component { get; set; }
            public float Value { get; set; }
            public string EffectKey { get; set; }
        }
    }
}
