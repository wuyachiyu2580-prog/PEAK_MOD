using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace WhereIsThing
{
    public sealed partial class WhereIsThingPlugin
    {
        private bool IsSelectedGroundItem(Item carrier, Item identity)
        {
            return carrier != null && identity != null && carrier.gameObject.activeInHierarchy &&
                carrier.itemState == ItemState.Ground && (_effectiveScopes & ThingLocationScope.Ground) != 0 &&
                _selectedIds.Contains(identity.itemID);
        }

        private bool ShouldScanRootPlants()
        {
            return (_effectiveScopes & ThingLocationScope.Ground) != 0 &&
                _catalog.Any(definition => ThingPickupIdentity.IsRootPlant(definition.Prefab) &&
                    definition.ItemIds.Any(_selectedIds.Contains));
        }

        private void RefreshRootPlantItems()
        {
            if (!ShouldScanRootPlants()) return;
            // The registry also includes terrain Items that have no registered PhotonView.
            foreach (Item carrier in Item.ALL_ITEMS)
            {
                Item identity = ThingPickupIdentity.Resolve(carrier);
                if (!ThingPickupIdentity.IsRootPlant(identity) || !IsSelectedGroundItem(carrier, identity) ||
                    !carrier.gameObject.scene.IsValid()) continue;
                Item captured = carrier;
                AddLabel("item:" + captured.GetInstanceID(), captured.transform,
                    () => ThingCatalog.GetDisplayName(identity, _nameLanguage.Value),
                    () => IsSelectedGroundItem(captured, identity));
            }
        }

        private void RefreshRootPlantFakeItems()
        {
            if (!ShouldScanRootPlants()) return;
            // One discovery step per validation cycle, never a per-frame scene-wide search.
            foreach (FakeItem carrier in FindObjectsByType<FakeItem>(FindObjectsSortMode.None))
            {
                Item identity = ThingPickupIdentity.Resolve(carrier);
                if (carrier == null || carrier.pickedUp || !carrier.gameObject.scene.IsValid() ||
                    !ThingPickupIdentity.IsRootPlant(identity) || !_selectedIds.Contains(identity.itemID) ||
                    carrier.GetComponentInParent<Item>() != null) continue;
                FakeItem captured = carrier;
                AddLabel("plant:" + captured.GetInstanceID(), captured.transform,
                    () => ThingCatalog.GetDisplayName(identity, _nameLanguage.Value),
                    () => captured != null && captured.gameObject.activeInHierarchy && !captured.pickedUp &&
                        (_effectiveScopes & ThingLocationScope.Ground) != 0 && _selectedIds.Contains(identity.itemID));
            }
        }

        private void NormalizePickupSelection(HashSet<ushort> selection)
        {
            foreach (ThingTargetDefinition definition in _catalog)
            {
                foreach (Item carrier in definition.Prefabs)
                {
                    Item identity = ThingPickupIdentity.Resolve(carrier);
                    if (identity != null && carrier.itemID != identity.itemID && selection.Remove(carrier.itemID))
                        selection.Add(identity.itemID);
                }
            }
        }

        private void MigratePickupSelections()
        {
            if (_pickupIdentityVersion.Value >= 1) return;
            // Only replace the untouched built-in survival selection. Custom selections keep both identities.
            var legacySurvivalIds = new HashSet<ushort>();
            var survival = ThingPresetFactory.CreateSurvivalPreset();
            ThingPresetFactory.ResolveBuiltInItemTargets(survival, _catalog);
            foreach (ThingTargetDefinition definition in _catalog)
                foreach (Item carrier in definition.Prefabs)
                {
                    Item identity = ThingPickupIdentity.Resolve(carrier);
                    if (survival.SelectedItemIds.Contains(identity.itemID) ||
                        (carrier.UIData != null && string.Equals(carrier.UIData.itemName, "Medicinal Root", StringComparison.OrdinalIgnoreCase)))
                        legacySurvivalIds.Add(carrier.itemID);
                }
            foreach (ThingPresetDefinition preset in _localPresets)
            {
                if (preset.Id == ThingPresetFactory.SurvivalPresetId && preset.SelectedItemIds.SetEquals(legacySurvivalIds))
                {
                    preset.SelectedItemIds.Clear();
                    preset.SelectedItemIds.UnionWith(survival.SelectedItemIds);
                }
                else NormalizePickupSelection(preset.SelectedItemIds);
            }
            NormalizePickupSelection(_selectedIds);
            _pickupIdentityVersion.Value = 1;
            _log.LogInfo("[Selection] Migrated pickup identities; hidden mandrakes are separate from medicinal roots.");
        }
    }
}
