using System;

namespace WhereIsThing
{
    // Resolve the identity used by the game's pickup code, without touching the scene carrier.
    internal static class ThingPickupIdentity
    {
        public static Item Resolve(Item carrier)
        {
            return carrier != null && carrier.isSecretlyOtherItemPrefab != null
                ? carrier.isSecretlyOtherItemPrefab : carrier;
        }

        public static Item Resolve(FakeItem carrier)
        {
            return carrier == null ? null : carrier.realItemPrefab;
        }

        public static bool IsRootPlant(Item identity)
        {
            if (identity == null || identity.UIData == null) return false;
            return string.Equals(identity.UIData.itemName, "Mandrake", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(identity.UIData.itemName, "Medicinal Root", StringComparison.OrdinalIgnoreCase);
        }
    }
}
