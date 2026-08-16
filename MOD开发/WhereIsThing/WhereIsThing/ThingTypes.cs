using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Zorro.Core;

namespace WhereIsThing
{
    internal enum ThingScanMode
    {
        Persistent,
        Timed
    }

    internal enum ThingNameLanguage
    {
        Game,
        English,
        SimplifiedChinese
    }

    [Flags]
    internal enum ThingLocationScope
    {
        None = 0,
        Ground = 1,
        Held = 2,
        Backpack = 4,
        Luggage = 8
    }

    internal enum ThingTargetKind
    {
        ItemGroup,
        Luggage,
        SceneTarget
    }

    internal enum ThingLuggageType
    {
        RespawnChest,
        Beach,
        Jungle,
        Tundra,
        Caldera,
        Climber,
        Ancient,
        Cursed,
        Mesa,
        Roots,
        Gloom,
        Citadel,
        Clown,
        Other
    }

    internal enum ThingSceneTargetType
    {
        MushroomZombie,
        Beetle,
        Scorpion,
        Spider,
        BeeSwarm,
        Scoutmaster,
        TumbleWeed,
        GhostBall,
        SpikeTrap,
        Antlion,
        VenusFlyTrap,
        Tornado,
        NapberryHypnoOrb,
        ArrowShooter,
        MovingSawBlade,
        SpikeRoller,
        SwingingAxe,
        GloomBellTower
    }

    internal sealed class ThingTargetDefinition
    {
        private readonly List<Item> _prefabs;
        private readonly List<ushort> _itemIds;
        private readonly ThingLuggageType _luggageType;
        private readonly ThingSceneTargetType _sceneTargetType;

        public ThingTargetDefinition(IEnumerable<Item> prefabs)
        {
            _prefabs = (prefabs ?? Enumerable.Empty<Item>())
                .Where(item => item != null && item.gameObject != null && item.UIData != null)
                .GroupBy(item => item.itemID)
                .Select(group => group.First())
                .ToList();
            _itemIds = _prefabs.Select(item => item.itemID).ToList();
            Kind = ThingTargetKind.ItemGroup;
            Category = ThingCatalog.GetCategory(_prefabs.FirstOrDefault());
        }

        private ThingTargetDefinition(ThingLuggageType luggageType)
        {
            _prefabs = new List<Item>();
            _itemIds = new List<ushort>();
            _luggageType = luggageType;
            Kind = ThingTargetKind.Luggage;
            Category = "Luggage";
        }

        private ThingTargetDefinition(ThingSceneTargetType sceneTargetType, IEnumerable<Item> prefabs)
        {
            _prefabs = (prefabs ?? Enumerable.Empty<Item>())
                .Where(item => item != null && item.gameObject != null && item.UIData != null)
                .GroupBy(item => item.itemID)
                .Select(group => group.First())
                .ToList();
            _itemIds = _prefabs.Select(item => item.itemID).ToList();
            _sceneTargetType = sceneTargetType;
            Kind = ThingTargetKind.SceneTarget;
            Category = ThingCatalog.GetSceneTargetCategory(sceneTargetType);
        }

        public static ThingTargetDefinition CreateLuggage(ThingLuggageType luggageType)
        {
            return new ThingTargetDefinition(luggageType);
        }

        public static ThingTargetDefinition CreateSceneTarget(ThingSceneTargetType sceneTargetType)
        {
            return new ThingTargetDefinition(sceneTargetType, null);
        }

        public static ThingTargetDefinition CreateSceneTarget(ThingSceneTargetType sceneTargetType, IEnumerable<Item> prefabs)
        {
            return new ThingTargetDefinition(sceneTargetType, prefabs);
        }

        public ThingTargetKind Kind { get; private set; }
        public bool IsLuggage { get { return Kind == ThingTargetKind.Luggage; } }
        public bool IsSceneTarget { get { return Kind == ThingTargetKind.SceneTarget; } }
        public Item Prefab { get { return _prefabs.FirstOrDefault(); } }
        public ushort ItemId { get { return _itemIds.Count == 0 ? ushort.MaxValue : _itemIds[0]; } }
        public IReadOnlyList<ushort> ItemIds { get { return _itemIds; } }
        public IReadOnlyList<Item> Prefabs { get { return _prefabs; } }
        public ThingLuggageType LuggageType { get { return _luggageType; } }
        public ThingSceneTargetType SceneTargetType { get { return _sceneTargetType; } }
        public string PrefabName
        {
            get
            {
                if (IsLuggage) return _luggageType.ToString();
                if (IsSceneTarget)
                {
                    return _sceneTargetType + " " + string.Join(" / ", _prefabs.Select(item => item.gameObject.name).Distinct().ToArray());
                }
                return string.Join(" / ", _prefabs.Select(item => item.gameObject.name).Distinct().ToArray());
            }
        }
        public string Category { get; private set; }
        public int VariantCount { get { return _itemIds.Count; } }

        public string GetDisplayName(ThingNameLanguage language)
        {
            if (IsLuggage) return ThingCatalog.GetLuggageDisplayName(_luggageType, language);
            if (IsSceneTarget) return ThingCatalog.GetSceneTargetDisplayName(_sceneTargetType, language);
            return ThingCatalog.GetDisplayName(Prefab, language);
        }

        public string GetSearchText(ThingNameLanguage language)
        {
            string displayName = GetDisplayName(language);
            return displayName + " " + PrefabName + " " + string.Join(" ", _prefabs.Select(item => item.UIData.itemName).ToArray());
        }

        public string GetVariantSuffix(ThingNameLanguage language)
        {
            if (VariantCount <= 1)
            {
                return string.Empty;
            }
            return language == ThingNameLanguage.English
                ? string.Format(" ({0} variants)", VariantCount)
                : string.Format(" ({0} 个变体)", VariantCount);
        }

        public bool IsSelected(HashSet<ushort> selectedIds, HashSet<ThingLuggageType> selectedLuggageTypes,
            HashSet<ThingSceneTargetType> selectedSceneTargetTypes)
        {
            if (IsLuggage)
            {
                return selectedLuggageTypes.Contains(_luggageType);
            }
            if (IsSceneTarget)
            {
                return selectedSceneTargetTypes.Contains(_sceneTargetType) || _itemIds.Any(selectedIds.Contains);
            }
            return _itemIds.Any(selectedIds.Contains);
        }

        public void SetSelected(HashSet<ushort> selectedIds, HashSet<ThingLuggageType> selectedLuggageTypes,
            HashSet<ThingSceneTargetType> selectedSceneTargetTypes, bool selected)
        {
            if (IsLuggage)
            {
                if (selected)
                {
                    selectedLuggageTypes.Add(_luggageType);
                }
                else
                {
                    selectedLuggageTypes.Remove(_luggageType);
                }
                return;
            }

            if (IsSceneTarget)
            {
                if (selected)
                {
                    selectedSceneTargetTypes.Add(_sceneTargetType);
                }
                else
                {
                    selectedSceneTargetTypes.Remove(_sceneTargetType);
                }
            }

            foreach (ushort itemId in _itemIds)
            {
                if (selected)
                {
                    selectedIds.Add(itemId);
                }
                else
                {
                    selectedIds.Remove(itemId);
                }
            }
        }
    }

    internal static class ThingCatalog
    {
        private static readonly ThingLuggageType[] LuggageTypes =
        {
            ThingLuggageType.RespawnChest,
            ThingLuggageType.Beach,
            ThingLuggageType.Jungle,
            ThingLuggageType.Tundra,
            ThingLuggageType.Caldera,
            ThingLuggageType.Climber,
            ThingLuggageType.Ancient,
            ThingLuggageType.Cursed,
            ThingLuggageType.Mesa,
            ThingLuggageType.Roots,
            ThingLuggageType.Gloom,
            ThingLuggageType.Citadel,
            ThingLuggageType.Clown,
            ThingLuggageType.Other
        };

        private static readonly ThingSceneTargetType[] SceneTargetTypes =
        {
            ThingSceneTargetType.MushroomZombie,
            ThingSceneTargetType.Beetle,
            ThingSceneTargetType.Scorpion,
            ThingSceneTargetType.Spider,
            ThingSceneTargetType.BeeSwarm,
            ThingSceneTargetType.Scoutmaster,
            ThingSceneTargetType.TumbleWeed,
            ThingSceneTargetType.GhostBall,
            ThingSceneTargetType.SpikeTrap,
            ThingSceneTargetType.Antlion,
            ThingSceneTargetType.VenusFlyTrap,
            ThingSceneTargetType.Tornado,
            ThingSceneTargetType.NapberryHypnoOrb,
            ThingSceneTargetType.ArrowShooter,
            ThingSceneTargetType.MovingSawBlade,
            ThingSceneTargetType.SpikeRoller,
            ThingSceneTargetType.SwingingAxe,
            ThingSceneTargetType.GloomBellTower
        };

        private static readonly string[] SpecialWords = { "scoutmaster's soul", "scoutmastersoul" };
        private static readonly string[] FoodWords = { "airplane food", "berrynana", "coconut", "crispberry", "fungus", "kingberry", "mandrake", "marshmallow", "hot dog" };
        private static readonly string[] MedicineWords = { "aloe vera", "first aid kit", "bandage", "medkit", "medicine", "medic", "antidote", "cure", "remedy", "gauze", "sunscreen" };
        private static readonly string[] ClimbingWords = { "rope", "piton", "climbing", "grip", "spike", "hook", "grapple" };
        private static readonly string[] MobilityWords = { "parachute", "parasol", "glider", "rocketpack", "jetpack", "balloon", "spring" };
        private static readonly string[] LightWords = { "lantern", "torch", "candle", "flare", "flashlight" };
        private static readonly string[] NavigationWords = { "compass", "binocular", "bugle", "guidebook", "passport", "map" };
        private static readonly string[] CombatWords = { "chain launcher", "chainshooter", "dagger", "dart", "cannon", "dynamite", "gun", "weapon", "sword", "bomb", "spear", "blowgun" };
        private static readonly string[] ContainerWords = { "backpack", "back pack", "bag", "pack", "luggage", "chest", "case" };
        private static readonly string[] CreatureWords = { "beehive", "bird", "beetle", "scorpion", "spider", "frog", "bug", "egg", "moth", "snake" };
        private static readonly string[] SurvivalToolWords = { "checkpoint flag", "conch", "magic bean", "megaphone", "portable stove", "firewood", "stick", "stone" };
        private static readonly string[] ToyWords = { "bishop", "basketball", "ball", "frisbee", "king", "knight", "pawn", "queen", "rook", "toy", "bingbong", "boombox", "record" };
        private static readonly MethodInfo GetSpawnPoolMethod = typeof(Spawner).GetMethod("GetSpawnPool", BindingFlags.Instance | BindingFlags.NonPublic);

        public static List<ThingTargetDefinition> Load()
        {
            List<Item> items = new List<Item>();
            try
            {
                if (SingletonAsset<ItemDatabase>.Instance == null || SingletonAsset<ItemDatabase>.Instance.itemLookup == null)
                {
                    return new List<ThingTargetDefinition>();
                }

                items.AddRange(SingletonAsset<ItemDatabase>.Instance.itemLookup.Values
                    .Where(item => item != null && item.gameObject != null && item.UIData != null));
            }
            catch
            {
                return new List<ThingTargetDefinition>();
            }

            List<ThingTargetDefinition> itemDefinitions = items
                .GroupBy(GetMergeKey, StringComparer.OrdinalIgnoreCase)
                .Select(group => new ThingTargetDefinition(group))
                .Where(definition => definition.Prefab != null)
                .ToList();
            List<ThingTargetDefinition> result = new List<ThingTargetDefinition>(itemDefinitions);
            result.AddRange(LuggageTypes.Select(ThingTargetDefinition.CreateLuggage));
            foreach (ThingSceneTargetType sceneTargetType in SceneTargetTypes)
            {
                string sceneMergeKey = NormalizeMergeKey(GetSceneTargetDisplayName(sceneTargetType, ThingNameLanguage.English));
                ThingTargetDefinition matchingItem = itemDefinitions.FirstOrDefault(definition =>
                    string.Equals(GetMergeKey(definition.Prefab), sceneMergeKey, StringComparison.OrdinalIgnoreCase));
                if (matchingItem != null)
                {
                    result.Remove(matchingItem);
                    result.Add(ThingTargetDefinition.CreateSceneTarget(sceneTargetType, matchingItem.Prefabs));
                }
                else
                {
                    result.Add(ThingTargetDefinition.CreateSceneTarget(sceneTargetType));
                }
            }

            return result
                .OrderBy(item => GetCategoryOrder(item.Category))
                .ThenBy(item => item.GetDisplayName(ThingNameLanguage.Game), StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public static string GetDisplayName(Item item, ThingNameLanguage language)
        {
            if (item == null || item.UIData == null)
            {
                return "Unknown item";
            }

            try
            {
                if (language == ThingNameLanguage.Game)
                {
                    return item.GetName();
                }

                LocalizedText.Language gameLanguage = language == ThingNameLanguage.English
                    ? LocalizedText.Language.English
                    : LocalizedText.Language.SimplifiedChinese;
                string localized = LocalizedText.GetText(LocalizedText.GetNameIndex(item.UIData.itemName), gameLanguage);
                if (!string.IsNullOrEmpty(localized) && !localized.StartsWith("LOC: ", StringComparison.OrdinalIgnoreCase))
                {
                    return localized;
                }
            }
            catch
            {
                // Some item assets are not fully initialized until the first scene loads.
            }

            return string.IsNullOrEmpty(item.UIData.itemName) ? item.gameObject.name : item.UIData.itemName;
        }

        public static string GetLuggageDisplayName(ThingLuggageType luggageType, ThingNameLanguage language)
        {
            if (UseChineseNames(language))
            {
                switch (luggageType)
                {
                    case ThingLuggageType.RespawnChest: return "复活箱";
                    case ThingLuggageType.Beach: return "海滩行李箱";
                    case ThingLuggageType.Jungle: return "丛林行李箱";
                    case ThingLuggageType.Tundra: return "苔原行李箱";
                    case ThingLuggageType.Caldera: return "火山口行李箱";
                    case ThingLuggageType.Climber: return "攀登行李箱";
                    case ThingLuggageType.Ancient: return "远古行李箱";
                    case ThingLuggageType.Cursed: return "诅咒行李箱";
                    case ThingLuggageType.Mesa: return "台地行李箱";
                    case ThingLuggageType.Roots: return "根系行李箱";
                    case ThingLuggageType.Gloom: return "阴郁行李箱";
                    case ThingLuggageType.Citadel: return "城塞行李箱";
                    case ThingLuggageType.Clown: return "小丑行李箱";
                    default: return "其他行李箱";
                }
            }

            switch (luggageType)
            {
                case ThingLuggageType.RespawnChest: return "Respawn Chest";
                case ThingLuggageType.Beach: return "Beach Luggage";
                case ThingLuggageType.Jungle: return "Jungle Luggage";
                case ThingLuggageType.Tundra: return "Tundra Luggage";
                case ThingLuggageType.Caldera: return "Caldera Luggage";
                case ThingLuggageType.Climber: return "Climber Luggage";
                case ThingLuggageType.Ancient: return "Ancient Luggage";
                case ThingLuggageType.Cursed: return "Cursed Luggage";
                case ThingLuggageType.Mesa: return "Mesa Luggage";
                case ThingLuggageType.Roots: return "Roots Luggage";
                case ThingLuggageType.Gloom: return "Gloom Luggage";
                case ThingLuggageType.Citadel: return "Citadel Luggage";
                case ThingLuggageType.Clown: return "Clown Luggage";
                default: return "Other Luggage";
            }
        }

        public static string GetSceneTargetDisplayName(ThingSceneTargetType sceneTargetType, ThingNameLanguage language)
        {
            if (UseChineseNames(language))
            {
                switch (sceneTargetType)
                {
                    case ThingSceneTargetType.MushroomZombie: return "森蕈僵尸";
                    case ThingSceneTargetType.Beetle: return "甲虫";
                    case ThingSceneTargetType.Scorpion: return "蝎子";
                    case ThingSceneTargetType.Spider: return "蜘蛛";
                    case ThingSceneTargetType.BeeSwarm: return "蜂群";
                    case ThingSceneTargetType.Scoutmaster: return "童军领队";
                    case ThingSceneTargetType.TumbleWeed: return "风滚草";
                    case ThingSceneTargetType.GhostBall: return "鬼球";
                    case ThingSceneTargetType.SpikeTrap: return "地刺";
                    case ThingSceneTargetType.Antlion: return "蚁狮";
                    case ThingSceneTargetType.VenusFlyTrap: return "捕蝇草";
                    case ThingSceneTargetType.Tornado: return "龙卷风";
                    case ThingSceneTargetType.NapberryHypnoOrb: return "未摘下的晚安莓";
                    case ThingSceneTargetType.ArrowShooter: return "箭矢发射器";
                    case ThingSceneTargetType.MovingSawBlade: return "移动锯刃";
                    case ThingSceneTargetType.SpikeRoller: return "滚刺机关";
                    case ThingSceneTargetType.SwingingAxe: return "摆斧机关";
                    default: return "雾沼钟塔";
                }
            }

            switch (sceneTargetType)
            {
                case ThingSceneTargetType.MushroomZombie: return "Mushroom Zombie";
                case ThingSceneTargetType.Beetle: return "Beetle";
                case ThingSceneTargetType.Scorpion: return "Scorpion";
                case ThingSceneTargetType.Spider: return "Spider";
                case ThingSceneTargetType.BeeSwarm: return "Bee Swarm";
                case ThingSceneTargetType.Scoutmaster: return "Scoutmaster";
                case ThingSceneTargetType.TumbleWeed: return "Tumbleweed";
                case ThingSceneTargetType.GhostBall: return "Ghost Ball";
                case ThingSceneTargetType.SpikeTrap: return "Spike Trap";
                case ThingSceneTargetType.Antlion: return "Antlion";
                case ThingSceneTargetType.VenusFlyTrap: return "Venus Flytrap";
                case ThingSceneTargetType.Tornado: return "Tornado";
                case ThingSceneTargetType.NapberryHypnoOrb: return "Unpicked Napberry";
                case ThingSceneTargetType.ArrowShooter: return "Arrow Shooter";
                case ThingSceneTargetType.MovingSawBlade: return "Moving Sawblade";
                case ThingSceneTargetType.SpikeRoller: return "Spike Roller";
                case ThingSceneTargetType.SwingingAxe: return "Swinging Axe";
                default: return "Gloom Bell Tower";
            }
        }

        public static string GetGloomBellTowerLabelName(GhostFire ghostFire, ThingNameLanguage language)
        {
            string name = GetSceneTargetDisplayName(ThingSceneTargetType.GloomBellTower, language);
            try
            {
                if (language == ThingNameLanguage.Game)
                {
                    string gameName = ghostFire.GetName();
                    if (!string.IsNullOrEmpty(gameName)) name = gameName;
                }
                else
                {
                    LocalizedText.Language gameLanguage = language == ThingNameLanguage.English
                        ? LocalizedText.Language.English
                        : LocalizedText.Language.SimplifiedChinese;
                    string localized = LocalizedText.GetText(ghostFire.displayNameIndex, gameLanguage);
                    if (!string.IsNullOrEmpty(localized) && !localized.StartsWith("LOC: ", StringComparison.OrdinalIgnoreCase))
                    {
                        name = localized;
                    }
                }
            }
            catch
            {
                // The GhostFire localization table may not be available during early scene loading.
            }

            bool chinese = UseChineseNames(language);
            return name + (ghostFire.isLit ? (chinese ? "\n已点亮" : "\nLit") : (chinese ? "\n未点亮" : "\nUnlit"));
        }

        public static ThingLuggageType GetLuggageType(Luggage luggage)
        {
            if (luggage == null)
            {
                return ThingLuggageType.Other;
            }
            if (luggage is RespawnChest)
            {
                return ThingLuggageType.RespawnChest;
            }

            string objectName = luggage.gameObject == null ? string.Empty : luggage.gameObject.name.ToLowerInvariant();
            if (luggage is LuggageCursed || objectName.Contains("cursed"))
            {
                return ThingLuggageType.Cursed;
            }
            if (objectName.Contains("ancient"))
            {
                return ThingLuggageType.Ancient;
            }

            SpawnPool effectiveSpawnPool = GetEffectiveLuggageSpawnPool(luggage);
            if (luggage.gameObject.CompareTag("ClownLuggage") || objectName.Contains("clown") || effectiveSpawnPool.HasFlag(SpawnPool.LuggageClown))
            {
                return ThingLuggageType.Clown;
            }

            ThingLuggageType[] orderedTypes =
            {
                ThingLuggageType.Beach,
                ThingLuggageType.Jungle,
                ThingLuggageType.Tundra,
                ThingLuggageType.Caldera,
                ThingLuggageType.Climber,
                ThingLuggageType.Ancient,
                ThingLuggageType.Mesa,
                ThingLuggageType.Roots,
                ThingLuggageType.Gloom,
                ThingLuggageType.Citadel
            };
            SpawnPool[] pools =
            {
                SpawnPool.LuggageBeach,
                SpawnPool.LuggageJungle,
                SpawnPool.LuggageTundra,
                SpawnPool.LuggageCaldera,
                SpawnPool.LuggageClimber,
                SpawnPool.LuggageAncient,
                SpawnPool.LuggageMesa,
                SpawnPool.LuggageRoots,
                SpawnPool.LuggageGloom,
                SpawnPool.LuggageCitadel
            };
            for (int i = 0; i < pools.Length; i++)
            {
                if (effectiveSpawnPool.HasFlag(pools[i]))
                {
                    return orderedTypes[i];
                }
            }
            return ThingLuggageType.Other;
        }

        private static SpawnPool GetEffectiveLuggageSpawnPool(Luggage luggage)
        {
            if (GetSpawnPoolMethod != null)
            {
                try
                {
                    object value = GetSpawnPoolMethod.Invoke(luggage, null);
                    if (value is SpawnPool)
                    {
                        return (SpawnPool)value;
                    }
                }
                catch
                {
                    // Fall back to the serialized pool if the game changes this method.
                }
            }

            return luggage.spawnPool;
        }

        public static string GetLuggageDisplayName(ThingNameLanguage language)
        {
            return UseChineseNames(language) ? "行李箱" : "Luggage";
        }

        private static bool UseChineseNames(ThingNameLanguage language)
        {
            if (language == ThingNameLanguage.SimplifiedChinese)
            {
                return true;
            }
            if (language == ThingNameLanguage.English)
            {
                return false;
            }

            return LocalizedText.CURRENT_LANGUAGE == LocalizedText.Language.SimplifiedChinese ||
                LocalizedText.CURRENT_LANGUAGE == LocalizedText.Language.TraditionalChinese;
        }

        public static string GetLuggageLabelName(Luggage luggage, ThingNameLanguage language)
        {
            if (luggage == null)
            {
                return GetLuggageDisplayName(language);
            }

            try
            {
                if (language == ThingNameLanguage.Game)
                {
                    return luggage.GetName();
                }

                LocalizedText.Language gameLanguage = language == ThingNameLanguage.English
                    ? LocalizedText.Language.English
                    : LocalizedText.Language.SimplifiedChinese;
                string localized = LocalizedText.GetText(luggage.displayName, gameLanguage);
                if (!string.IsNullOrEmpty(localized) && !localized.StartsWith("LOC: ", StringComparison.OrdinalIgnoreCase))
                {
                    return localized;
                }
            }
            catch
            {
                // Luggage localization is unavailable during early scene loading.
            }

            return GetLuggageDisplayName(language);
        }

        public static string GetSceneTargetCategory(ThingSceneTargetType sceneTargetType)
        {
            switch (sceneTargetType)
            {
                case ThingSceneTargetType.MushroomZombie:
                case ThingSceneTargetType.Beetle:
                case ThingSceneTargetType.Scorpion:
                case ThingSceneTargetType.Spider:
                case ThingSceneTargetType.BeeSwarm:
                case ThingSceneTargetType.Scoutmaster:
                    return "Hostile Creatures";
                case ThingSceneTargetType.TumbleWeed:
                case ThingSceneTargetType.GhostBall:
                case ThingSceneTargetType.SpikeTrap:
                case ThingSceneTargetType.Antlion:
                case ThingSceneTargetType.VenusFlyTrap:
                case ThingSceneTargetType.Tornado:
                case ThingSceneTargetType.NapberryHypnoOrb:
                    return "Natural Hazards";
                case ThingSceneTargetType.ArrowShooter:
                case ThingSceneTargetType.MovingSawBlade:
                case ThingSceneTargetType.SpikeRoller:
                case ThingSceneTargetType.SwingingAxe:
                    return "Mechanical Traps";
                case ThingSceneTargetType.GloomBellTower:
                    return "Landmarks";
                default:
                    return "Misc";
            }
        }

        public static string GetCategory(Item item)
        {
            if (item == null)
            {
                return "Misc";
            }

            Item.ItemTags tags = item.itemTags;
            if ((tags & (Item.ItemTags.ScoutAmulet | Item.ItemTags.GoldenIdol | Item.ItemTags.BookOfBones | Item.ItemTags.BingBong)) != 0)
            {
                return "Special";
            }
            if ((tags & Item.ItemTags.Mystical) != 0)
            {
                return "Mystical";
            }

            if (item.GetComponent<Peak.EarlyWorm>() != null || ContainsAny(item.gameObject.name.ToLowerInvariant(), new[] { "earlyworm" }))
            {
                return "Creatures";
            }

            if ((tags & (Item.ItemTags.PackagedFood | Item.ItemTags.Berry | Item.ItemTags.Mushroom | Item.ItemTags.GourmandRequirement)) != 0)
            {
                return "Food";
            }

            string name = (item.gameObject.name + " " + item.UIData.itemName + " " + GetDisplayName(item, ThingNameLanguage.English)).ToLowerInvariant();
            if (ContainsAny(name, SpecialWords)) return "Special";
            if (ContainsAny(name, FoodWords)) return "Food";
            if (ContainsAny(name, MedicineWords)) return "Medicine";
            if (ContainsAny(name, ContainerWords)) return "Containers";
            if (ContainsAny(name, ClimbingWords)) return "Climbing Gear";
            if (ContainsAny(name, MobilityWords)) return "Mobility";
            if (ContainsAny(name, LightWords)) return "Lighting";
            if (ContainsAny(name, NavigationWords)) return "Navigation";
            if (IsNamed(item, "AK") || ContainsAny(name, CombatWords)) return "Weapons and Explosives";
            if ((tags & Item.ItemTags.Bird) != 0 || ContainsAny(name, CreatureWords)) return "Creatures";
            if (ContainsAny(name, SurvivalToolWords)) return "Survival Tools";
            if (ContainsAny(name, ToyWords)) return "Toys and Sports";
            return "Misc";
        }

        public static string GetCategoryDisplay(string category, ThingNameLanguage language)
        {
            if (category == "Luggage") return UseChineseNames(language) ? "行李箱" : "Luggage";
            if (category == "All") return UseChineseNames(language) ? "全部" : "All";
            if (!UseChineseNames(language)) return category;

            switch (category)
            {
                case "Special": return "特殊物品";
                case "Mystical": return "神秘物品";
                case "Food": return "食物";
                case "Medicine": return "医疗与状态";
                case "Containers": return "容器与背包";
                case "Survival Tools": return "生存工具";
                case "Climbing Gear": return "攀爬装备";
                case "Mobility": return "移动装备";
                case "Lighting": return "照明";
                case "Navigation": return "导航与观测";
                case "Weapons and Explosives": return "武器与爆炸物";
                case "Creatures": return "生物";
                case "Toys and Sports": return "玩具与运动";
                case "Hostile Creatures": return "危险生物";
                case "Natural Hazards": return "自然危险";
                case "Mechanical Traps": return "机关陷阱";
                case "Hazards": return "危险";
                case "Landmarks": return "地标";
                default: return "其他";
            }
        }

        public static string GetContainerSuffix(ThingNameLanguage language)
        {
            return language == ThingNameLanguage.English ? "In backpack" : "背包内";
        }

        private static string GetMergeKey(Item item)
        {
            string displayName = GetDisplayName(item, ThingNameLanguage.English);
            if (string.IsNullOrEmpty(displayName) || displayName.StartsWith("LOC: ", StringComparison.OrdinalIgnoreCase))
            {
                displayName = item.UIData.itemName;
            }
            return NormalizeMergeKey(displayName);
        }

        private static string NormalizeMergeKey(string value)
        {
            if (string.IsNullOrEmpty(value)) return "UNKNOWN";
            return string.Join(" ", value.Trim().Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)).ToUpperInvariant();
        }

        private static int GetCategoryOrder(string category)
        {
            switch (category)
            {
                case "Special": return 0;
                case "Mystical": return 1;
                case "Food": return 2;
                case "Medicine": return 3;
                case "Containers": return 4;
                case "Survival Tools": return 5;
                case "Luggage": return 6;
                case "Hostile Creatures": return 7;
                case "Natural Hazards": return 8;
                case "Mechanical Traps": return 9;
                case "Hazards": return 10;
                case "Landmarks": return 11;
                case "Climbing Gear": return 12;
                case "Mobility": return 13;
                case "Lighting": return 14;
                case "Navigation": return 15;
                case "Weapons and Explosives": return 16;
                case "Creatures": return 17;
                case "Toys and Sports": return 18;
                default: return 19;
            }
        }

        private static bool ContainsAny(string value, IEnumerable<string> words)
        {
            return words.Any(value.Contains);
        }

        private static bool IsNamed(Item item, string expectedName)
        {
            return string.Equals(item.gameObject.name, expectedName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(item.UIData.itemName, expectedName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(GetDisplayName(item, ThingNameLanguage.English), expectedName, StringComparison.OrdinalIgnoreCase);
        }
    }
}
