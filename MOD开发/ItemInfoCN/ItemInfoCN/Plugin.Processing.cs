using System;
using System.Reflection;
using HarmonyLib;
using Peak.Afflictions;
using TMPro;
using UnityEngine;

namespace ItemInfoCN
{
    public partial class Plugin
    {
        // ── Cached reflection for private members ──
        private static readonly FieldInfo _itemCarryWeight = AccessTools.Field(typeof(Item), "carryWeight");
        private static readonly FieldInfo _lanternFuel = AccessTools.Field(typeof(Lantern), "fuel");
        private static readonly PropertyInfo _mobState = AccessTools.Property(typeof(Mob), "mobState");
        private static readonly PropertyInfo _cookingHasExplosion = AccessTools.Property(typeof(ItemCooking), "hasExplosion");

        internal static void ProcessItemGameObject()
        {
            Item item = Character.observedCharacter.data.currentItem;
            GameObject itemGameObj = item.gameObject;
            Component[] itemComponents = itemGameObj.GetComponents(typeof(Component));
            bool isConsumable = false;
            string prefixStatus = "";
            string suffixWeight = "";
            string suffixUses = "";
            string suffixCooked = "";
            string suffixAfflictions = "";
            var tm = itemInfoDisplayTextMesh;
            tm.text = "";

            // ── Weight display ──
            if (item.name == "Backpack(Clone)")
            {
                suffixWeight += effectColors["Weight"] + "0 重量</color>";
            }
            else if (Ascents.itemWeightModifier > 0)
            {
                int cw = (int)_itemCarryWeight.GetValue(item);
                suffixWeight += effectColors["Weight"] +
                    ((float)(cw + Ascents.itemWeightModifier) * 2.5f).ToString("F1").Replace(".0", "") +
                    " 重量</color>";
            }
            else
            {
                int cw = (int)_itemCarryWeight.GetValue(item);
                suffixWeight += effectColors["Weight"] +
                    ((float)cw * 2.5f).ToString("F1").Replace(".0", "") +
                    " 重量</color>";
            }

            // ── Special item descriptions ──
            if (itemGameObj.name.Equals("Bugle(Clone)"))
                tm.text += "使用 " + effectColors["Hunger"] + "喇叭</color> 发出一些美妙的声音\n";
            else if (itemGameObj.name.Equals("Pirate Compass(Clone)"))
                tm.text += effectColors["Injury"] + "指向</color>最近的行李\n";
            else if (itemGameObj.name.Equals("Compass(Clone)"))
                tm.text += effectColors["Injury"] + "指向</color>北方的山峰\n";
            else if (itemGameObj.name.Equals("Shell Big(Clone)"))
                tm.text += effectColors["Hunger"] + "开</color>椰子的好工具\n";

            // ── Component processing loop ──
            bool isBecomeSkeleton = false;
            for (int i = 0; i < itemComponents.Length; i++)
            {
                Type componentType = itemComponents[i].GetType();
                Component component = itemComponents[i];
                Behaviour behaviour = component as Behaviour;
                if (behaviour != null && !behaviour.enabled)
                    continue;

                if (componentType == typeof(ItemUseFeedback))
                {
                    var feedback = (ItemUseFeedback)component;
                    if (feedback.useAnimation.Equals("Eat") || feedback.tag == "BookOfBones" ||
                        feedback.useAnimation.Equals("Drink") || feedback.useAnimation.Equals("Heal"))
                        isConsumable = true;
                }
                else if (componentType == typeof(Action_Consume))
                {
                    isConsumable = true;
                }
                else if (componentType == typeof(Action_RestoreHunger))
                {
                    var effect = (Action_RestoreHunger)component;
                    prefixStatus += ProcessEffect(effect.restorationAmount * -1f, "Hunger", true);
                }
                else if (componentType == typeof(Action_GiveExtraStamina))
                {
                    var effect = (Action_GiveExtraStamina)component;
                    prefixStatus += ProcessEffect(effect.amount, "Extra Stamina", true);
                }
                else if (componentType == typeof(Action_InflictPoison))
                {
                    var effect = (Action_InflictPoison)component;
                    prefixStatus += effect.delay.ToString() + "秒后, " +
                        ProcessEffectOverTime(effect.poisonPerSecond, 1f, effect.inflictionTime, "Poison", true);
                }
                else if (componentType == typeof(Action_AddOrRemoveThorns))
                {
                    var effect = (Action_AddOrRemoveThorns)component;
                    prefixStatus += ProcessEffect((float)effect.thornCount * 0.05f, "Thorns", true);
                }
                else if (componentType == typeof(Action_ModifyStatus))
                {
                    Character player = Character.localCharacter;
                    if (player.data.fullyPassedOut)
                        player = MainCameraMovement.specCharacter;
                    var effect = (Action_ModifyStatus)component;
                    if ((effect.ifSkeleton && player.data.isSkeleton) || isBecomeSkeleton)
                        prefixStatus += ProcessEffect(effect.changeAmount, effect.statusType.ToString(), true);
                    else if (!effect.ifSkeleton)
                        prefixStatus += ProcessEffect(effect.changeAmount, effect.statusType.ToString(), true);
                }
                else if (componentType == typeof(Action_ApplyMassAffliction))
                {
                    var effect = (Action_ApplyMassAffliction)component;
                    suffixAfflictions += "<#CCCCCC>附近玩家将获得:</color>\n";
                    suffixAfflictions += ProcessAffliction(effect.affliction);
                    if (effect.extraAfflictions.Length != 0)
                    {
                        for (int j = 0; j < effect.extraAfflictions.Length; j++)
                        {
                            if (suffixAfflictions.EndsWith("\n"))
                                suffixAfflictions = suffixAfflictions.Remove(suffixAfflictions.Length - 1);
                            suffixAfflictions += ",\n" + ProcessAffliction(effect.extraAfflictions[j]);
                        }
                    }
                }
                else if (componentType == typeof(Action_ApplyAffliction))
                {
                    var effect = (Action_ApplyAffliction)component;
                    suffixAfflictions += ProcessAffliction(effect.affliction);
                }
                else if (componentType == typeof(Mandrake))
                {
                    var effect = (Mandrake)component;
                    tm.text += string.Format(
                        "当你唤醒 {0} 曼德拉草</color> 后，它会给你唱晚安曲\n\n对附近 {1} 米内的玩家施加 {2}{3}</color> 效果\n\n",
                        effectColors["ItemInfoDisplayNegative"], effect.aoe.range,
                        effectColors[effect.aoe.statusType.ToString()],
                        GetEffectChineseName(effect.aoe.statusType.ToString()));
                }
                else if (componentType == typeof(Action_Numb))
                {
                    var effect = (Action_Numb)component;
                    prefixStatus += "获得 " + effectColors["ItemInfoDisplayNegative"] +
                        "麻木</color> 效果，持续 " + effect.numbAmount.ToString("F1").Replace(".0", "") + " 秒</color>\n\n";
                }
                else if (componentType == typeof(Action_BecomeSkeleton))
                {
                    tm.text += "使用后你将会变成 骷髅人\n\n";
                    isBecomeSkeleton = true;
                }
                else if (componentType == typeof(Action_ClearAllStatus))
                {
                    var effect = (Action_ClearAllStatus)component;
                    tm.text += effectColors["ItemInfoDisplayPositive"] + "清除所有状态</color>";
                    if (effect.excludeCurse)
                        tm.text += " 除了 " + effectColors["Curse"] + "诅咒</color>";
                    if (effect.otherExclusions.Count > 0)
                    {
                        foreach (CharacterAfflictions.STATUSTYPE exclusion in effect.otherExclusions)
                            tm.text += ", " + effectColors[exclusion.ToString()] + exclusion.ToString().ToUpper() + "</color>";
                    }
                    tm.text = tm.text.Replace(", <#E13542>CRAB</color>", "") + "\n";
                }
                else if (componentType == typeof(Action_ConsumeAndSpawn))
                {
                    var effect = (Action_ConsumeAndSpawn)component;
                    if (effect.itemToSpawn.ToString().Contains("Peel"))
                        tm.text += "<#CCCCCC>食用后获得果皮</color>\n";
                }
                else if (componentType == typeof(Action_ReduceUses))
                {
                    if (item.data.data.ContainsKey(DataEntryKey.ItemUses))
                    {
                        var uses = (OptionableIntItemData)item.data.data[DataEntryKey.ItemUses];
                        if (uses.HasData)
                        {
                            suffixUses += uses.Value > 1
                                ? "   还可以使用" + uses.Value.ToString() + " 次"
                                : "   只能使用 1 次";
                        }
                    }
                }
                else if (componentType == typeof(Lantern))
                {
                    var lantern = (Lantern)component;
                    float fuel = (float)_lanternFuel.GetValue(lantern);
                    if (itemGameObj.name.Equals("Torch(Clone)"))
                    {
                        tm.text += "可以点燃\n";
                    }
                    else
                    {
                        suffixAfflictions += "<#CCCCCC>点燃时，附近玩家获得:</color>\n\n";
                    }
                    if (itemGameObj.name.Equals("Lantern_Faerie(Clone)"))
                    {
                        StatusField sf = itemGameObj.transform.Find("FaerieLantern/Light/Heat").GetComponent<StatusField>();
                        suffixAfflictions += "<#CCCCCC>还可以使用 " + fuel.ToString("F1").Replace(".0", "") + "秒</color>\n\n";
                        suffixAfflictions += ProcessEffectPerSecond(sf.statusAmountPerSecond, sf.statusType.ToString(), true);
                        foreach (var status in sf.additionalStatuses)
                            suffixAfflictions += ProcessEffectPerSecond(status.statusAmountPerSecond, status.statusType.ToString(), true);
                    }
                    else if (itemGameObj.name.Equals("Lantern(Clone)"))
                    {
                        Transform t = itemGameObj.transform.Find("GasLantern/Light/Heat");
                        StatusField sf = t != null ? t.GetComponent<StatusField>() : null;
                        suffixAfflictions += "<#CCCCCC>还可以使用 " + fuel.ToString("F1").Replace(".0", "") + "秒</color>\n\n";
                        if (sf != null)
                            suffixAfflictions += ProcessEffectPerSecond(sf.statusAmountPerSecond, sf.statusType.ToString(), true);
                    }
                }
                else if (componentType == typeof(Action_RaycastDart))
                {
                    var effect = (Action_RaycastDart)component;
                    isConsumable = true;
                    suffixAfflictions += "<#CCCCCC>发射飞镖，将对被命中的玩家施加以下效果:</color>\n\n";
                    for (int k = 0; k < effect.afflictionsOnHit.Length; k++)
                        suffixAfflictions += ProcessAffliction(effect.afflictionsOnHit[k]);
                }
                else if (componentType == typeof(MagicBugle))
                {
                    tm.text += "当你吹奏喇叭时，";
                }
                else if (componentType == typeof(ClimbingSpikeComponent))
                {
                    tm.text += "放置一个可抓住的岩钉，可以在岩钉上" + effectColors["Extra Stamina"] + "恢复体力</color>\n";
                }
                else if (componentType == typeof(Action_Flare))
                {
                    tm.text += "可以点燃\n";
                }
                else if (componentType == typeof(Backpack))
                {
                    tm.text += EasyBackpack ? "按下 B 键可以打开背包，并存入物品\n" : "放下背包才可以存入物品\n";
                }
                else if (componentType == typeof(BananaPeel))
                {
                    tm.text += effectColors["Hunger"] + "踩上去会滑倒</color>\n";
                }
                else if (componentType == typeof(Constructable))
                {
                    var effect = (Constructable)component;
                    if (effect.constructedPrefab.name.Equals("PortableStovetop_Placed"))
                        tm.text += "放置一个 便携火炉 可以提供 " + effectColors["Injury"] + "烹饪</color> 功能 ，持续 " +
                            effect.constructedPrefab.GetComponent<Campfire>().burnsFor.ToString() + "秒\n";
                    else
                        tm.text += "可以放置\n";
                }
                else if (componentType == typeof(RopeSpool))
                {
                    var effect = (RopeSpool)component;
                    tm.text += effect.isAntiRope ? "放置一条向上的反重力绳子\n\n" : "放一条绳子\n\n";
                    tm.text += "长度从 " + (effect.minSegments / 4f).ToString("F2").Replace(".0", "") +
                        " 米到 " + ((float)Rope.MaxSegments / 4f).ToString("F1").Replace(".0", "") + " 米\n";
                    if (configForceUpdateTime.Value <= 1f)
                        suffixUses += "   剩余 " + (effect.RopeFuel / 4f).ToString("F2").Replace(".00", "") + " 米";
                }
                else if (componentType == typeof(RopeShooter))
                {
                    var effect = (RopeShooter)component;
                    tm.text += "发射一个绳索锚点，可生成\n\n一条";
                    tm.text += effect.ropeAnchorWithRopePref.name.Equals("RopeAnchorForRopeShooterAnti")
                        ? "向上漂浮 " : "向下垂落 ";
                    tm.text += (effect.maxLength / 4f).ToString("F1").Replace(".0", "") + "米的绳子\n";
                }
                else if (componentType == typeof(Antigrav))
                {
                    var effect = (Antigrav)component;
                    if (effect.intensity != 0f)
                        suffixAfflictions += effectColors["Injury"] + "警告:</color> <#CCCCCC>丢弃时会飞走</color>\n\n";
                }
                else if (componentType == typeof(Action_Balloon))
                {
                    suffixAfflictions += "可以绑在玩家身上\n";
                }
                else if (componentType == typeof(VineShooter))
                {
                    var effect = (VineShooter)component;
                    tm.text += "从你的位置发射一条锁链\n\n连接到射击点，最远可达\n\n" +
                        (effect.maxLength / 1.6666666f).ToString("F1").Replace(".0", "") + " 米距离\n";
                }
                else if (componentType == typeof(CloudFungus))
                {
                    suffixAfflictions += effectColors["Hunger"] + "丢下</color>以部署一个云朵(可在空中生成)\n";
                }
                else if (componentType == typeof(ShelfShroom))
                {
                    var effect = (ShelfShroom)component;
                    if (effect.instantiateOnBreak.name.Equals("HealingPuffShroomSpawn"))
                    {
                        GameObject sporeExplo = effect.instantiateOnBreak.transform.Find("VFX_SporeHealingExplo").gameObject;
                        if (sporeExplo != null)
                        {
                            tm.text += effectColors["Hunger"] + "丢下</color>以释放范围效果\n\n";
                            RemoveAfterSeconds ras = sporeExplo.GetComponent<RemoveAfterSeconds>();
                            float duration = ras != null ? ras.seconds : 0f;
                            tm.text += ProcessGameObjectAndChildrenAOE(sporeExplo, duration, false);
                        }
                    }
                }
                else if (componentType == typeof(ScoutEffigy))
                {
                    tm.text += effectColors["Extra Stamina"] + "复活</color>死去的玩家\n";
                }
                else if (componentType == typeof(Action_Die))
                {
                    tm.text += "使用时你会" + effectColors["Curse"] + "死亡</color>\n";
                }
                else if (componentType == typeof(Action_SpawnGuidebookPage))
                {
                    isConsumable = true;
                    tm.text += "可以打开\n";
                }
                else if (componentType == typeof(Action_Guidebook))
                {
                    tm.text += "可以阅读\n";
                }
                else if (componentType == typeof(Action_CallScoutmaster))
                {
                    tm.text += effectColors["Injury"] + "使用时违反规则0</color>\n";
                }
                else if (componentType == typeof(Action_MoraleBoost))
                {
                    var effect = (Action_MoraleBoost)component;
                    if (effect.boostRadius < 0f)
                    {
                        tm.text += effectColors["ItemInfoDisplayPositive"] + "获得</color> " +
                            effectColors["Extra Stamina"] +
                            (effect.baselineStaminaBoost * 100f).ToString("F1").Replace(".0", "") + " 额外体力</color>\n";
                    }
                    else if (effect.boostRadius > 0f)
                    {
                        tm.text += "<#CCCCCC>附近玩家</color>" +
                            effectColors["ItemInfoDisplayPositive"] + " 获得</color> " +
                            effectColors["Extra Stamina"] +
                            (effect.baselineStaminaBoost * 100f).ToString("F1").Replace(".0", "") + " 额外体力</color>\n";
                    }
                }
                else if (componentType == typeof(Breakable))
                {
                    tm.text += effectColors["Hunger"] + " 丢出去</color>将它砸开\n";
                }
                else if (componentType == typeof(Bonkable))
                {
                    tm.text += effectColors["Hunger"] + " 瞄准队友的脑瓜</color> " + effectColors["Injury"] + "将他砸晕\n";
                }
                else if (componentType == typeof(MagicBean))
                {
                    var effect = (MagicBean)component;
                    tm.text += effectColors["Hunger"] + "丢下</color>会种下藤蔓，\n藤蔓会垂直于地形生长，最长可达 " +
                        (effect.plantPrefab.maxLength / 2f).ToString("F1").Replace(".0", "") + " 米，或直到碰到障碍物\n";
                }
                else if (componentType == typeof(BingBong))
                {
                    tm.text += "航空公司的吉祥物：" + effectColors["Extra Stamina"] + "BingBong</color>\n";
                }
                else if (componentType == typeof(Action_Passport))
                {
                    tm.text += "使用 " + effectColors["Hunger"] + "护照</color> 可以自定义外观\n";
                }
                else if (componentType == typeof(Actions_Binoculars))
                {
                    tm.text += "使用 " + effectColors["Hunger"] + "望远镜</color> 观察远处的物体\n";
                }
                else if (componentType == typeof(Action_WarpToRandomPlayer))
                {
                    tm.text += "传送到随机玩家\n";
                }
                else if (componentType == typeof(Action_WarpToBiome))
                {
                    var effect = (Action_WarpToBiome)component;
                    tm.text += "传送到" + effect.segmentToWarpTo.ToString().ToUpper() + "\n";
                }
                else if (componentType == typeof(Parasol))
                {
                    tm.text += "使用 " + effectColors["Hunger"] + "太阳伞</color> 防止你自由落体\n";
                }
                else if (componentType == typeof(RescueHook))
                {
                    var effect = (RescueHook)component;
                    tm.text += "<#CCCCCC>发射一条钩爪来:</color>\n\n";
                    tm.text += effectColors["ItemInfoDisplayPositive"] + "救援其他玩家</color>\n\n";
                    tm.text += effectColors["ItemInfoDisplayPositive"] + "拉动自己到墙壁</color>\n\n";
                    tm.text += effectColors["Extra Stamina"] + "距离 " +
                        effect.range.ToString("F1").Replace(".0", "") + "米</color>\n";
                }
                else if (componentType == typeof(Action_ConstructableScoutCannonScroll))
                {
                    tm.text += "\n<#CCCCCC>放置后点燃引线：</color>\n\n将桶内童兵发射出去\n";
                }
                else if (componentType == typeof(Action_RandomMushroomEffect))
                {
                    ProcessMushroomEffect(component, tm);
                }
                else if (componentType == typeof(Dynamite))
                {
                    var effect = (Dynamite)component;
                    tm.text += effectColors["Injury"] + "爆炸</color>造成最多" + effectColors["Injury"] +
                        (effect.explosionPrefab.GetComponent<AOE>().statusAmount * 100f).ToString("F1").Replace(".0", "") +
                        " 伤害</color>\n\n<#CCCCCC>持有时受到额外伤害</color>\n";
                }
                else if (componentType == typeof(Scorpion))
                {
                    ProcessScorpion((Scorpion)component, item, tm);
                }
                else if (componentType == typeof(Action_Spawn))
                {
                    var effect = (Action_Spawn)component;
                    if (effect.objectToSpawn.name.Equals("VFX_Sunscreen"))
                    {
                        AOE aoe = effect.objectToSpawn.transform.Find("AOE").GetComponent<AOE>();
                        RemoveAfterSeconds ras = effect.objectToSpawn.transform.Find("AOE").GetComponent<RemoveAfterSeconds>();
                        tm.text += "<#CCCCCC>喷洒持续" + ras.seconds.ToString("F1").Replace(".0", "") +
                            "秒的雾气，施加:</color>\n" + ProcessAffliction(aoe.affliction);
                    }
                }
                else if (componentType == typeof(CactusBall))
                {
                    var effect = (CactusBall)component;
                    tm.text += effectColors["Thorns"] + "尖刺</color> 会附在身体上\n\n可以通过 " +
                        effectColors["Hunger"] + "投掷</color> 扔出\n\n至少需要蓄力 " +
                        (effect.throwChargeRequirement * 100f).ToString("F1").Replace(".0", "") + "%\n";
                }
                else if (componentType == typeof(BingBongShieldWhileHolding))
                {
                    tm.text += "<#CCCCCC>持有时获得：</color>\n\n" + effectColors["Shield"] + "无敌状态</color>\n";
                }
                else if (componentType == typeof(CheckpointConstructable))
                {
                    tm.text += "选择一处地方进行放置，为你提供一次" + effectColors["Extra Stamina"] + "复活</color>效果\n";
                }
                else if (componentType == typeof(ItemCooking))
                {
                    suffixCooked = ProcessCooking((ItemCooking)component);
                }
            }

            // ── Assemble final text ──
            if (prefixStatus.Length > 0 && isConsumable)
                tm.text = prefixStatus + "\n" + tm.text;
            if (suffixAfflictions.Length > 0)
                tm.text += "\n" + suffixAfflictions;
            tm.text += "\n" + suffixWeight + suffixUses + suffixCooked;
            tm.text = tm.text.Replace("\n\n\n", "\n\n");
        }

        private static void ProcessMushroomEffect(Component component, TextMeshProUGUI tm)
        {
            var effect = (Action_RandomMushroomEffect)component;
            int mushroomEffect = -1;
            if (effect.useDebugEffect)
                mushroomEffect = effect.debugEffect;
            else if (MushroomManager.instance != null)
            {
                int idx = effect.mushroomTypeIndex % MushroomManager.instance.mushroomEffects.Length;
                mushroomEffect = MushroomManager.instance.mushroomEffects[idx];
            }
            int staminaBonus = 0;
            if (!effect.useDebugEffect && MushroomManager.instance != null)
            {
                int idx = effect.mushroomTypeIndex % MushroomManager.instance.mushroomStamAmt.Length;
                staminaBonus = MushroomManager.instance.mushroomStamAmt[idx];
            }
            tm.text += effectColors["ItemInfoDisplayPositive"] + "食用后获得随机效果:</color>\n\n";
            if (staminaBonus > 0)
                tm.text += effectColors["Extra Stamina"] + "+" +
                    ((float)staminaBonus * 0.05f * 100f).ToString("F1").Replace(".0", "") + " 额外体力</color>\n";
            if (mushroomEffect >= 0)
            {
                tm.text += "\n<#CCCCCC>本次效果: ";
                switch (mushroomEffect)
                {
                    case 0:  tm.text += effectColors["Extra Stamina"] + "无限体力 4 秒</color>"; break;
                    case 1:  tm.text += effectColors["Extra Stamina"] + "加速效果: +50% 奔跑速度, +150% 攀爬速度, 持续 5 秒</color>"; break;
                    case 2:  tm.text += effectColors["ItemInfoDisplayPositive"] + "低重力效果, 持续 15 秒</color>"; break;
                    case 3:  tm.text += effectColors["Shield"] + "无敌效果, 持续10秒</color>"; break;
                    case 4:  tm.text += effectColors["ItemInfoDisplayPositive"] + "治疗: -15 饥饿值, -15 伤害, -15 中毒, 清除中毒效果</color>"; break;
                    case 5:  tm.text += effectColors["Injury"] + "产生爆炸并击飞附近队友</color>"; break;
                    case 6:  tm.text += effectColors["ItemInfoDisplayNegative"] + "失明效果, 持续 60 秒</color>"; break;
                    case 7:  tm.text += effectColors["Injury"] + "强制晕倒 8 秒</color>"; break;
                    case 8:  tm.text += effectColors["Poison"] + "+25 真菌感染</color>"; break;
                    case 9:  tm.text += effectColors["ItemInfoDisplayNegative"] + "麻木效果, 持续 60 秒</color>"; break;
                    default: tm.text += effectColors["ItemInfoDisplayNegative"] + "未知效果</color>"; break;
                }
                tm.text += "</color>\n\n";
            }
            else
            {
                tm.text += "<#CCCCCC>可能效果:</color>\n";
                tm.text += effectColors["ItemInfoDisplayPositive"] + "正面: 无限体力, 加速, 低重力, 无敌, 治疗</color>\n";
                tm.text += effectColors["ItemInfoDisplayNegative"] + "负面: 爆炸, 失明, 坠落, 真菌感染, 麻木</color>\n";
            }
            tm.text += effectColors["Hunger"] + "3秒后生效</color>\n";
        }

        private static void ProcessScorpion(Scorpion effect, Item item, TextMeshProUGUI tm)
        {
            if ((int)_mobState.GetValue(effect) == 3) return;
            tm.text += "持有时会对你造成" + effectColors["Poison"] + "伤害</color>:\n\n";
            tm.text += effectColors["Heat"] + "烹饪</color>会让其" + effectColors["Curse"] + "死亡</color>\n\n";
            if (configForceUpdateTime.Value <= 1f)
            {
                float ep = Mathf.Max(0.5f, 1f - item.holderCharacter.refs.afflictions.statusSum + 0.05f) * 100f;
                tm.text += "<#CCCCCC>下一次蜇伤将造成:</color> " + effectColors["Poison"] +
                    ep.ToString("F1").Replace(".0", "") + " </color>中毒</color>持续 " +
                    effect.totalPoisonTime.ToString("F1").Replace(".0", "") + " 秒\n\n";
                tm.text += "<#CCCCCC>(健康时伤害更高)</color>\n\n";
            }
            else
            {
                tm.text += "<#CCCCCC>下一次蜇伤将造成:</color>至少 " + effectColors["Poison"] +
                    "50 </color>中毒</color>持续 " + effect.totalPoisonTime.ToString("F1").Replace(".0", "") + " 秒\n\n";
                tm.text += "最多 " + effectColors["Poison"] + "105 中毒</color>持续 " +
                    effect.totalPoisonTime.ToString("F1").Replace(".0", "") + " 秒\n\n";
                tm.text += "<#CCCCCC>(健康时伤害更高)</color>\n\n";
            }
        }

        private static string ProcessCooking(ItemCooking cooking)
        {
            string result = "";
            if (cooking.wreckWhenCooked && cooking.timesCookedLocal >= 1)
                result += "\n\n" + effectColors["Curse"] + "因烹饪而损坏</color>";
            else if (cooking.wreckWhenCooked)
                result += "\n\n" + effectColors["Curse"] + "烹饪时会损坏</color>";
            else if (cooking.timesCookedLocal >= 12)
                result += "   " + effectColors["Curse"] + cooking.timesCookedLocal + "次烹饪\n\n无法再烹饪</color>";
            else if (cooking.timesCookedLocal == 0 && cooking.canBeCooked)
                result += "\n\n" + effectColors["Extra Stamina"] + "可以烹饪</color>";
            else if (cooking.timesCookedLocal == 1)
                result += "   " + effectColors["Extra Stamina"] + cooking.timesCookedLocal + "次烹饪</color>\n\n" +
                    effectColors["Hunger"] + "可以烹饪</color>";
            else if (cooking.timesCookedLocal == 2)
                result += "   " + effectColors["Hunger"] + cooking.timesCookedLocal + "次烹饪</color>\n\n" +
                    effectColors["Injury"] + "可以烹饪</color>";
            else if (cooking.timesCookedLocal == 3)
                result += "   " + effectColors["Injury"] + cooking.timesCookedLocal + "次烹饪</color>\n\n" +
                    effectColors["Poison"] + "可以烹饪</color>";
            else if (cooking.timesCookedLocal >= 4)
                result += "   " + effectColors["Poison"] + cooking.timesCookedLocal + "次烹饪\n\n可以烹饪</color>";
            if ((bool)_cookingHasExplosion.GetValue(cooking))
                result += "\n\n烹饪后会" + effectColors["Injury"] + "爆炸</color>";
            return result;
        }

        // ── AOE Processing ──

        private static string ProcessSingleGameObjectAOE(GameObject targetObject, float globalDuration = 0f, bool addTips = true)
        {
            string result = "";
            float baseIncrement = 0.025f;
            if (targetObject == null) return result;
            AOE aoe = targetObject.GetComponent<AOE>();
            if (aoe == null || Mathf.Abs(aoe.statusAmount) == 0f) return result;
            if (!addTips)
            {
                addTips = true;
                result += "<#CCCCCC>范围: " + aoe.range.ToString("F1").Replace(".0", "") + "米</color>，持续 " +
                    globalDuration.ToString() + " 秒\n\n";
                result += "<#CCCCCC>效果随距离衰减 (最小 " + (aoe.minFactor * 100f).ToString("F0") + "%)</color>\n\n";
            }
            TimeEvent timeEvent = targetObject.GetComponent<TimeEvent>();
            if (timeEvent != null && globalDuration > 0f)
            {
                baseIncrement = aoe.statusAmount < 0f ? Mathf.Abs(baseIncrement) * -1f : Mathf.Abs(baseIncrement);
                float newAmountPerSecond = Mathf.Floor(aoe.statusAmount * (1f / timeEvent.rate) / baseIncrement) * baseIncrement;
                result += ProcessEffectPerSecond(newAmountPerSecond, aoe.statusType.ToString(), true);
                if (aoe.addtlStatus != null && aoe.addtlStatus.Length != 0)
                {
                    foreach (CharacterAfflictions.STATUSTYPE addtl in aoe.addtlStatus)
                        result += ProcessEffectPerSecond(newAmountPerSecond, addtl.ToString(), true);
                }
            }
            else
            {
                result += "立刻" + ProcessEffect(aoe.statusAmount, aoe.statusType.ToString(), true);
                if (aoe.addtlStatus != null && aoe.addtlStatus.Length != 0)
                {
                    foreach (CharacterAfflictions.STATUSTYPE addtl in aoe.addtlStatus)
                        result += ProcessEffect(aoe.statusAmount, addtl.ToString(), true);
                }
            }
            if (aoe.hasAffliction && aoe.affliction != null)
                result += ProcessAffliction(aoe.affliction);
            if (!result.EndsWith("\n\n"))
                result += "\n\n";
            return result;
        }

        private static string ProcessGameObjectAndChildrenAOE(GameObject targetObject, float globalDuration = 0f, bool addTips = true)
        {
            string result = "";
            if (targetObject == null) return result;
            string current = ProcessSingleGameObjectAOE(targetObject, globalDuration, addTips);
            if (!string.IsNullOrEmpty(current))
                result += current;
            for (int i = 0; i < targetObject.transform.childCount; i++)
            {
                string child = ProcessGameObjectAndChildrenAOE(targetObject.transform.GetChild(i).gameObject, globalDuration, true);
                if (!string.IsNullOrEmpty(child))
                    result += child;
            }
            return result;
        }

        // ── Effect text helpers ──

        internal static string ProcessEffect(float amount, string effect, bool newLine = true)
        {
            string result = "";
            if (amount < 0f && effect == "Poison")
                result += ProcessEffect(amount, "Spores", true);
            if (amount == 0f) return result;
            if (amount > 0f)
            {
                result += effect.Equals("Extra Stamina") ? effectColors["ItemInfoDisplayPositive"] : effectColors["ItemInfoDisplayNegative"];
                result += "获得</color> ";
            }
            else
            {
                result += effect.Equals("Extra Stamina") ? effectColors["ItemInfoDisplayNegative"] : effectColors["ItemInfoDisplayPositive"];
                result += "移除</color> ";
            }
            result += effectColors[effect] + (Mathf.Abs(amount) * 100f).ToString("F1").Replace(".0", "") +
                " " + GetEffectChineseName(effect) + "</color>" + (newLine ? "\n\n" : "");
            return result;
        }

        internal static string ProcessEffectPerSecond(float amountPerSecond, string effect, bool newLine = true)
        {
            string result = "";
            if (amountPerSecond < 0f && effect == "Poison")
                result += ProcessEffectPerSecond(amountPerSecond, "Spores", true);
            if (amountPerSecond == 0f) return result;
            if (amountPerSecond > 0f)
            {
                result += effect.Equals("Extra Stamina") ? effectColors["ItemInfoDisplayPositive"] : effectColors["ItemInfoDisplayNegative"];
                result += "每秒获得</color> ";
            }
            else
            {
                result += effect.Equals("Extra Stamina") ? effectColors["ItemInfoDisplayNegative"] : effectColors["ItemInfoDisplayPositive"];
                result += "每秒移除</color> ";
            }
            result += effectColors[effect] + (Mathf.Abs(amountPerSecond) * 100f).ToString("F1").Replace(".0", "") +
                " " + GetEffectChineseName(effect) + "</color>" + (newLine ? "\n\n" : "");
            return result;
        }

        internal static string ProcessEffectOverTime(float amountPerSecond, float rate, float time, string effect, bool newLine = true)
        {
            string result = "";
            if (amountPerSecond < 0f && effect == "Poison")
                result += ProcessEffectOverTime(amountPerSecond, rate, time, "Spores", true);
            if (amountPerSecond == 0f || time == 0f) return result;
            if (amountPerSecond > 0f)
            {
                result += effect.Equals("Extra Stamina") ? effectColors["ItemInfoDisplayPositive"] : effectColors["ItemInfoDisplayNegative"];
                result += "获得</color> ";
            }
            else
            {
                result += effect.Equals("Extra Stamina") ? effectColors["ItemInfoDisplayNegative"] : effectColors["ItemInfoDisplayPositive"];
                result += "移除</color> ";
            }
            result += effectColors[effect] +
                (Mathf.Abs(amountPerSecond) * time * (1f / rate) * 100f).ToString("F1").Replace(".0", "") +
                " " + GetEffectChineseName(effect) + "</color> 持续 " +
                time.ToString("F1").Replace(".0", "") + "秒" + (newLine ? "\n" : "");
            return result;
        }

        // ── Affliction processing ──

        internal static string ProcessAffliction(Affliction affliction)
        {
            string result = "";
            int type = (int)affliction.GetAfflictionType();

            if (type == 2)
            {
                var e = (Affliction_FasterBoi)affliction;
                result += effectColors["ItemInfoDisplayPositive"] + "获得</color> " +
                    (e.totalTime + e.climbDelay).ToString("F1").Replace(".0", "") + " 秒的" +
                    effectColors["Extra Stamina"] + " " +
                    Mathf.Round(e.moveSpeedMod * 100f).ToString("F1").Replace(".0", "") + "% 额外奔跑速度</color> 或\n" +
                    effectColors["ItemInfoDisplayPositive"] + "获得</color> " +
                    e.totalTime.ToString("F1").Replace(".0", "") + " 秒的" +
                    effectColors["Extra Stamina"] + " " +
                    Mathf.Round(e.climbSpeedMod * 100f).ToString("F1").Replace(".0", "") + "% 额外攀爬速度</color>\n之后，" +
                    effectColors["ItemInfoDisplayNegative"] + "获得</color> " +
                    effectColors["Drowsy"] + (e.drowsyOnEnd * 100f).ToString("F1").Replace(".0", "") + " 困倦</color>\n";
            }
            else if (type == 8)
            {
                var e = (Affliction_ClearAllStatus)affliction;
                result += effectColors["ItemInfoDisplayPositive"] + "清除所有状态</color>";
                if (e.excludeCurse)
                    result += " 除了 " + effectColors["Curse"] + "诅咒</color>";
                result += "\n\n";
            }
            else if (type == 10)
            {
                var e = (Affliction_AddBonusStamina)affliction;
                result += effectColors["ItemInfoDisplayPositive"] + "获得</color> " +
                    effectColors["Extra Stamina"] + (e.staminaAmount * 100f).ToString("F1").Replace(".0", "") + " 额外体力</color>\n";
            }
            else if (type == 1)
            {
                var e = (Affliction_InfiniteStamina)affliction;
                if (e.climbDelay > 0f)
                {
                    result += effectColors["ItemInfoDisplayPositive"] + "获得</color> " +
                        (e.totalTime + e.climbDelay).ToString("F1").Replace(".0", "") + "秒的" +
                        effectColors["Extra Stamina"] + " 无限奔跑</color> 或\n" +
                        effectColors["ItemInfoDisplayPositive"] + "获得</color> " +
                        e.totalTime.ToString("F1").Replace(".0", "") + "秒的" +
                        effectColors["Extra Stamina"] + " 无限攀爬</color>\n";
                }
                else
                {
                    result += effectColors["ItemInfoDisplayPositive"] + "获得</color> " +
                        e.totalTime.ToString("F1").Replace(".0", "") + "秒的" +
                        effectColors["Extra Stamina"] + "无限体力</color>\n";
                }
                if (e.drowsyAffliction != null && e.drowsyAffliction.totalTime > 0f)
                    result += "之后，" + ProcessAffliction(e.drowsyAffliction);
            }
            else if (type == 7)
            {
                var e = (Affliction_AdjustStatus)affliction;
                if (e.statusAmount > 0f)
                {
                    result += e.statusType.ToString().Equals("Extra Stamina")
                        ? effectColors["ItemInfoDisplayPositive"] : effectColors["ItemInfoDisplayNegative"];
                    result += "获得</color> ";
                }
                else
                {
                    result += e.statusType.ToString().Equals("Extra Stamina")
                        ? effectColors["ItemInfoDisplayNegative"] : effectColors["ItemInfoDisplayPositive"];
                    result += "移除</color> ";
                }
                result += effectColors[e.statusType.ToString()] +
                    (Mathf.Abs(e.statusAmount) * 100f).ToString("F1").Replace(".0", "") +
                    " " + GetEffectChineseName(e.statusType.ToString()) + "</color>\n";
            }
            else if (type == 11)
            {
                var e = (Affliction_AdjustDrowsyOverTime)affliction;
                result += e.statusPerSecond > 0f
                    ? effectColors["ItemInfoDisplayNegative"] + "获得</color> "
                    : effectColors["ItemInfoDisplayPositive"] + "移除</color> ";
                result += effectColors["Drowsy"] +
                    (Mathf.Round(Mathf.Abs(e.statusPerSecond) * e.totalTime * 100f * 0.4f) / 0.4f).ToString("F1").Replace(".0", "") +
                    " 困倦</color> 持续 " + e.totalTime.ToString("F1").Replace(".0", "") + "秒\n";
            }
            else if (type == 5)
            {
                var e = (Affliction_AdjustColdOverTime)affliction;
                result += e.statusPerSecond > 0f
                    ? effectColors["ItemInfoDisplayNegative"] + "获得</color> "
                    : effectColors["ItemInfoDisplayPositive"] + "移除</color> ";
                result += effectColors["Cold"] +
                    (Mathf.Abs(e.statusPerSecond) * e.totalTime * 100f).ToString("F1").Replace(".0", "") +
                    " 寒冷</color> 持续 " + e.totalTime.ToString("F1").Replace(".0", "") + "秒\n";
            }
            else if (type == 6)
            {
                result += effectColors["ItemInfoDisplayPositive"] + "清除所有状态</color>\n\n";
                result += effectColors["ItemInfoDisplayNegative"] + "随机获得负面状态组合</color>\n\n";
                result += effectColors["ItemInfoDisplayPositive"] + "随机获得额外耐力</color>\n\n";
                result += "<#CCCCCC>效果完全随机</color>\n\n";
            }
            else if (type == 13)
            {
                var e = (Affliction_Sunscreen)affliction;
                result += "在 方山 的阳光下防止" + effectColors["Heat"] + "烧伤</color> " +
                    e.totalTime.ToString("F1").Replace(".0", "") + "秒\n";
            }
            else if (type == 16)
            {
                var e = (Affliction_Invincibility)affliction;
                result += effectColors["ItemInfoDisplayPositive"] + "获得</color> 持续 " +
                    effectColors["Shield"] + e.totalTime.ToString("F1").Replace(".0", "") + "</color> 秒的" +
                    effectColors["Shield"] + "无敌</color>\n";
            }
            return result;
        }
    }
}
