using UnityEngine;

namespace WhereIsThing
{
    public sealed partial class WhereIsThingPlugin
    {
        private void RefreshLuggageLabel(Luggage luggage)
        {
            if (luggage == null || !luggage.gameObject.activeInHierarchy) return;
            var trap = luggage.GetComponentInChildren<Peak.TrickLuggage>(true);
            ThingLuggageType type = ThingCatalog.GetLuggageType(luggage);
            bool IsSelected() => _selectedLuggageTypes.Contains(type) ||
                (trap != null && _selectedSceneTargetTypes.Contains(ThingSceneTargetType.TrapChest));
            bool IsValid() => luggage != null && luggage.gameObject.activeInHierarchy && IsSelected() &&
                (!luggage.IsOpen || IsTrapActive(trap));
            if (!IsValid()) return;
            string key = "luggage:" + luggage.GetInstanceID();
            AddLabel(key, luggage.transform, delegate
            {
                string name = trap != null
                    ? ThingCatalog.GetSceneTargetDisplayName(ThingSceneTargetType.TrapChest, _nameLanguage.Value)
                    : ThingCatalog.IsOrdinaryLuggageType(type) ? ThingCatalog.GetOrdinaryLuggageName(_nameLanguage.Value)
                    : ThingCatalog.GetLuggageDisplayName(type, _nameLanguage.Value);
                if (trap != null && luggage.IsOpen && IsTrapActive(trap)) name += "\n" + ThingCatalog.GetActiveTrapSuffix(_nameLanguage.Value);
                return name;
            }, IsValid, () => luggage.Center());
        }

        private static bool IsTrapActive(Peak.TrickLuggage trap)
        {
            return trap != null && trap.isActiveAndEnabled && trap.statusField != null &&
                trap.statusField.isActiveAndEnabled;
        }
    }
}
