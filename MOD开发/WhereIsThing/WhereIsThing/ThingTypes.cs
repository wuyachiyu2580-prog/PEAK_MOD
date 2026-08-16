using System;
using System.Collections.Generic;
using System.Linq;
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
        Luggage
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

    internal sealed class ThingTargetDefinition
    {
        private readonly List<Item> _prefabs;
        private readonly List<ushort> _itemIds;
        private readonly ThingLuggageType _luggageType;

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

        public static ThingTargetDefinition CreateLuggage(ThingLuggageType luggageType)
        {
            return new ThingTargetDefinition(luggageType);
        }

        public ThingTargetKind Kind { get; private set; }
        public bool IsLuggage { get { return Kind == ThingTargetKind.Luggage; } }
        public Item Prefab { get { return _prefabs.FirstOrDefault(); } }
        public ushort ItemId { get { return _itemIds.Count == 0 ? ushort.MaxValue : _itemIds[0]; } }
        public IReadOnlyList<ushort> ItemIds { get { return _itemIds; } }
        public ThingLuggageType LuggageType { get { return _luggageType; } }
        public string PrefabName { get { return IsLuggage ? _luggageType.ToString() : string.Join(" / ", _prefabs.Select(item => item.gameObject.name).Distinct().ToArray()); } }
        public string Category { get; private set; }
        public int VariantCount { get { return _itemIds.Count; } }

        public string GetDisplayName(ThingNameLanguage language)
        {
            return IsLuggage ? ThingCatalog.GetLuggageDisplayName(_luggageType, language) : ThingCatalog.GetDisplayName(Prefab, language);
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

        public bool IsSelected(HashSet<ushort> selectedIds, HashSet<ThingLuggageType> selectedLuggageTypes)
        {
            if (IsLuggage)
            {
                return selectedLuggageTypes.Contains(_luggageType);
            }
            return _itemIds.Any(selectedIds.Contains);
        }

        public void SetSelected(HashSet<ushort> selectedIds, HashSet<ThingLuggageType> selectedLuggageTypes, bool selected)
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

        private static readonly string[] MedicineWords = { "bandage", "medkit", "medicine", "medic", "antidote", "cure", "remedy", "gauze", "sunscreen" };
        private static readonly string[] ClimbingWords = { "rope", "piton", "climbing", "grip", "spike", "hook", "grapple" };
        private static readonly string[] MobilityWords = { "parachute", "parasol", "glider", "rocketpack", "jetpack", "balloon", "spring" };
        private static readonly string[] LightWords = { "lantern", "torch", "candle", "flare", "flashlight" };
        private static readonly string[] NavigationWords = { "compass", "binocular", "bugle", "guidebook", "passport", "map" };
        private static readonly string[] CombatWords = { "dagger", "dart", "cannon", "dynamite", "gun", "weapon", "sword", "bomb", "spear", "blowgun" };
        private static readonly string[] ContainerWords = { "backpack", "back pack", "bag", "pack", "luggage", "chest", "case" };
        private static readonly string[] CreatureWords = { "bird", "beetle", "scorpion", "spider", "frog", "bug", "egg", "moth", "snake" };
        private static readonly string[] ToyWords = { "basketball", "ball", "toy", "bingbong", "boombox", "record" };

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

            List<ThingTargetDefinition> result = items
                .GroupBy(GetMergeKey, StringComparer.OrdinalIgnoreCase)
                .Select(group => new ThingTargetDefinition(group))
                .Where(definition => definition.Prefab != null)
                .ToList();
            result.AddRange(LuggageTypes.Select(ThingTargetDefinition.CreateLuggage));

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
            if (UseChineseLuggageNames(language))
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
            if (luggage is LuggageCursed || luggage.spawnPool.HasFlag(SpawnPool.LuggageCursed))
            {
                return ThingLuggageType.Cursed;
            }
            if (luggage.gameObject.CompareTag("ClownLuggage") || luggage.spawnPool.HasFlag(SpawnPool.LuggageClown))
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
                if (luggage.spawnPool.HasFlag(pools[i]))
                {
                    return orderedTypes[i];
                }
            }
            return ThingLuggageType.Other;
        }

        public static string GetLuggageDisplayName(ThingNameLanguage language)
        {
            return UseChineseLuggageNames(language) ? "行李箱" : "Luggage";
        }

        private static bool UseChineseLuggageNames(ThingNameLanguage language)
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
            if ((tags & (Item.ItemTags.PackagedFood | Item.ItemTags.Berry | Item.ItemTags.Mushroom | Item.ItemTags.GourmandRequirement)) != 0)
            {
                return "Food";
            }

            string name = (item.gameObject.name + " " + item.UIData.itemName + " " + GetDisplayName(item, ThingNameLanguage.English)).ToLowerInvariant();
            if (ContainsAny(name, MedicineWords)) return "Medicine";
            if (ContainsAny(name, ContainerWords)) return "Containers";
            if (ContainsAny(name, ClimbingWords)) return "Climbing Gear";
            if (ContainsAny(name, MobilityWords)) return "Mobility";
            if (ContainsAny(name, LightWords)) return "Lighting";
            if (ContainsAny(name, NavigationWords)) return "Navigation";
            if (ContainsAny(name, CombatWords)) return "Weapons and Explosives";
            if ((tags & Item.ItemTags.Bird) != 0 || ContainsAny(name, CreatureWords)) return "Creatures";
            if (ContainsAny(name, ToyWords)) return "Toys and Sports";
            return "Misc";
        }

        public static string GetCategoryDisplay(string category, ThingNameLanguage language)
        {
            if (category == "Luggage") return language == ThingNameLanguage.English ? "Luggage" : "行李箱";
            if (category == "All") return language == ThingNameLanguage.English ? "All" : "全部";
            if (language == ThingNameLanguage.English) return category;

            switch (category)
            {
                case "Special": return "特殊物品";
                case "Mystical": return "神秘物品";
                case "Food": return "食物";
                case "Medicine": return "医疗与状态";
                case "Containers": return "容器与背包";
                case "Climbing Gear": return "攀爬装备";
                case "Mobility": return "移动装备";
                case "Lighting": return "照明";
                case "Navigation": return "导航与观测";
                case "Weapons and Explosives": return "武器与爆炸物";
                case "Creatures": return "生物";
                case "Toys and Sports": return "玩具与运动";
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
                case "Luggage": return 5;
                case "Climbing Gear": return 6;
                case "Mobility": return 7;
                case "Lighting": return 8;
                case "Navigation": return 9;
                case "Weapons and Explosives": return 10;
                case "Creatures": return 11;
                case "Toys and Sports": return 12;
                default: return 13;
            }
        }

        private static bool ContainsAny(string value, IEnumerable<string> words)
        {
            return words.Any(value.Contains);
        }
    }
}
