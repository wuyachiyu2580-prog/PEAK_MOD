using UnityEngine;

namespace WhereIsThing
{
    public sealed partial class WhereIsThingPlugin
    {
        private void RefreshNapberryPlants()
        {
            if ((_effectiveScopes & ThingLocationScope.Ground) == 0 ||
                !_selectedSceneTargetTypes.Contains(ThingSceneTargetType.NapberryHypnoOrb)) return;

            foreach (OrbThatMakesYouSleepy plant in FindObjectsByType<OrbThatMakesYouSleepy>(FindObjectsSortMode.None))
            {
                FakeItem fruit = plant == null ? null : plant.fakeBerry;
                if (!IsSelectedNapberryPlant(plant, fruit)) continue;
                OrbThatMakesYouSleepy capturedPlant = plant;
                FakeItem capturedFruit = fruit;
                // The fruit is hidden on pickup; the plant root remains in the scene.
                AddLabel("plant:" + capturedFruit.GetInstanceID(), capturedFruit.transform,
                    () => ThingCatalog.GetDisplayName(ThingPickupIdentity.Resolve(capturedFruit), _nameLanguage.Value),
                    () => IsSelectedNapberryPlant(capturedPlant, capturedFruit));
            }
        }

        private bool IsSelectedNapberryPlant(OrbThatMakesYouSleepy plant, FakeItem fruit)
        {
            return plant != null && plant.gameObject.activeInHierarchy && plant.fakeBerry == fruit &&
                ThingNapberry.IsAvailable(fruit) && (_effectiveScopes & ThingLocationScope.Ground) != 0 &&
                _selectedSceneTargetTypes.Contains(ThingSceneTargetType.NapberryHypnoOrb);
        }
    }
}
