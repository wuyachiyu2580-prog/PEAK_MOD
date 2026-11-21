using System;

namespace WhereIsThing
{
    internal static class ThingItemTraits
    {
        internal static bool TryGetMushroomVariant(Item item, out bool poisonous, out string variant)
        {
            poisonous = false;
            variant = item == null || item.gameObject == null ? string.Empty : item.gameObject.name;
            if (variant.EndsWith("(Clone)", StringComparison.OrdinalIgnoreCase))
                variant = variant.Substring(0, variant.Length - 7).TrimEnd();
            switch (variant.ToLowerInvariant())
            {
                case "mushroom cluster":
                case "mushroom lace":
                case "mushroom normie": return true;
                case "mushroom cluster poison":
                case "mushroom lace poison":
                case "mushroom normie poison": poisonous = true; return true;
                default: return false;
            }
        }

        internal static bool IsReusableGroundItem(Item item)
        {
            return item != null && !item.blockInteraction &&
                (item.GetComponent<Flare>() == null || item.UIData == null || item.UIData.canPocket || item.UIData.canBackpack);
        }
    }
}
