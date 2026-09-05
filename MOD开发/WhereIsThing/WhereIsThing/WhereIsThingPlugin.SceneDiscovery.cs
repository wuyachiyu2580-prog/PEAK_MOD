using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace WhereIsThing
{
    public sealed partial class WhereIsThingPlugin
    {
        private void RefreshRegisteredTargets()
        {
            _scanSeenBuffer.Clear();
            if (_selectedLuggageTypes.Count > 0)
            {
                foreach (Luggage luggage in Luggage.ALL_LUGGAGE)
                {
                    if (luggage == null || !luggage.gameObject.activeInHierarchy || luggage.IsOpen)
                    {
                        continue;
                    }

                    ThingLuggageType luggageType = ThingCatalog.GetLuggageType(luggage);
                    if (!_selectedLuggageTypes.Contains(luggageType))
                    {
                        continue;
                    }

                    string key = "luggage:" + luggage.GetInstanceID();
                    _scanSeenBuffer.Add(key);
                    Luggage captured = luggage;
                    AddLabel(key, captured.transform,
                        delegate { return ThingCatalog.GetLuggageLabelName(captured, _nameLanguage.Value); },
                        delegate
                        {
                            return captured != null && captured.gameObject.activeInHierarchy && !captured.IsOpen &&
                                _selectedLuggageTypes.Contains(luggageType);
                        }, delegate { return captured.Center(); });
                }
            }

            if (_selectedSceneTargetTypes.Contains(ThingSceneTargetType.MushroomZombie))
            {
                ZombieManager zombieManager = ZombieManager.Instance;
                if (zombieManager != null && zombieManager.zombies != null)
                {
                    foreach (MushroomZombie zombie in zombieManager.zombies)
                    {
                        if (zombie == null || !zombie.gameObject.activeInHierarchy ||
                            zombie.currentState == MushroomZombie.State.Dead)
                        {
                            continue;
                        }

                        MushroomZombie captured = zombie;
                        Character zombieCharacter = captured.GetComponent<Character>();
                        AddSceneLabel(_scanSeenBuffer, ThingSceneTargetType.MushroomZombie, captured,
                            delegate { return ThingCatalog.GetSceneTargetDisplayName(ThingSceneTargetType.MushroomZombie, _nameLanguage.Value); },
                            delegate
                            {
                                return captured != null && captured.gameObject.activeInHierarchy &&
                                    captured.currentState != MushroomZombie.State.Dead &&
                                    _selectedSceneTargetTypes.Contains(ThingSceneTargetType.MushroomZombie);
                            }, delegate { return zombieCharacter == null ? captured.transform.position : zombieCharacter.Center; });
                    }
                }
            }

            RefreshMobLabels(_scanSeenBuffer);

            if (_selectedSceneTargetTypes.Contains(ThingSceneTargetType.GhostBall))
            {
                Peak.GhostBallSpawner spawner = Peak.GhostBallSpawner.Instance;
                Peak.GhostBall ghostBall = spawner == null ? null : spawner.currentGhostBall;
                if (ghostBall != null && ghostBall.gameObject.activeInHierarchy)
                {
                    Peak.GhostBall captured = ghostBall;
                    AddSceneLabel(_scanSeenBuffer, ThingSceneTargetType.GhostBall, captured,
                        delegate { return ThingCatalog.GetSceneTargetDisplayName(ThingSceneTargetType.GhostBall, _nameLanguage.Value); },
                        delegate
                        {
                            return captured != null && captured.gameObject.activeInHierarchy &&
                                _selectedSceneTargetTypes.Contains(ThingSceneTargetType.GhostBall);
                        });
                }
            }
        }

        private void ProcessSceneDiscovery()
        {
            if (_sceneDiscoveryStep >= SceneDiscoveryStepCount)
            {
                return;
            }

            _scanSeenBuffer.Clear();
            switch (_sceneDiscoveryStep++)
            {
                case 0:
                    RefreshAmuletStatueLabels(_scanSeenBuffer);
                    break;
                case 1:
                    RefreshActiveSceneTargets<TumbleWeed>(_scanSeenBuffer, ThingSceneTargetType.TumbleWeed);
                    break;
                case 2:
                    if (_selectedSceneTargetTypes.Contains(ThingSceneTargetType.GloomBellTower))
                    {
                        foreach (GhostFire ghostFire in Peak.GloomSafeZone.ALL_GLOOM_SAFE_ZONES.OfType<GhostFire>())
                        {
                            if (ghostFire == null || !ghostFire.gameObject.activeInHierarchy)
                            {
                                continue;
                            }
                            GhostFire captured = ghostFire;
                            AddSceneLabel(_scanSeenBuffer, ThingSceneTargetType.GloomBellTower, captured,
                                delegate { return ThingCatalog.GetGloomBellTowerLabelName(captured, _nameLanguage.Value); },
                                delegate
                                {
                                    return captured != null && captured.gameObject.activeInHierarchy &&
                                        _selectedSceneTargetTypes.Contains(ThingSceneTargetType.GloomBellTower);
                                });
                        }
                    }
                    break;
                case 3:
                    RefreshActiveSceneTargets<Spider>(_scanSeenBuffer, ThingSceneTargetType.Spider);
                    break;
                case 4:
                    RefreshActiveSceneTargets<BeeSwarm>(_scanSeenBuffer, ThingSceneTargetType.BeeSwarm);
                    break;
                case 5:
                    RefreshActiveSceneTargets<Scoutmaster>(_scanSeenBuffer, ThingSceneTargetType.Scoutmaster);
                    break;
                case 6:
                    RefreshActiveSceneTargets<Peak.SpikeTrap>(_scanSeenBuffer, ThingSceneTargetType.SpikeTrap);
                    break;
                case 7:
                    RefreshActiveSceneTargets<Antlion>(_scanSeenBuffer, ThingSceneTargetType.Antlion);
                    break;
                case 8:
                    RefreshActiveSceneTargets<VenusFlyTrap>(_scanSeenBuffer, ThingSceneTargetType.VenusFlyTrap);
                    break;
                case 9:
                    RefreshActiveSceneTargets<Tornado>(_scanSeenBuffer, ThingSceneTargetType.Tornado);
                    break;
                case 10:
                    RefreshActiveSceneTargets<OrbThatMakesYouSleepy>(_scanSeenBuffer, ThingSceneTargetType.NapberryHypnoOrb);
                    break;
                case 11:
                    RefreshActiveSceneTargets<ArrowShooter>(_scanSeenBuffer, ThingSceneTargetType.ArrowShooter);
                    break;
                case 12:
                    RefreshActiveSceneTargets<Peak.MovingSawBlade>(_scanSeenBuffer, ThingSceneTargetType.MovingSawBlade);
                    break;
                case 13:
                    RefreshActiveSceneTargets<Peak.SpikeRoller>(_scanSeenBuffer, ThingSceneTargetType.SpikeRoller);
                    break;
                case 14:
                    RefreshActiveSceneTargets<SwingingAxe>(_scanSeenBuffer, ThingSceneTargetType.SwingingAxe);
                    break;
                case 15:
                    RefreshActiveSceneTargets<SlipperyJellyfish>(_scanSeenBuffer, ThingSceneTargetType.SlipperyJellyfish,
                        delegate(SlipperyJellyfish target)
                        {
                            return HasRunSetting(target, RunSettings.SETTINGTYPE.Hazard_Jellyfish);
                        });
                    break;
                case 16:
                    RefreshActiveSceneTargets<WindAffectedStatusEmitter>(_scanSeenBuffer, ThingSceneTargetType.SporeCloud,
                        delegate(WindAffectedStatusEmitter target)
                        {
                            return HasRunSetting(target, RunSettings.SETTINGTYPE.Hazard_SporeClouds);
                        });
                    break;
                case 17:
                    RefreshRunSettingHazards(_scanSeenBuffer);
                    break;
            }
        }

        private void RefreshRunSettingHazards(HashSet<string> seen)
        {
            bool scanUrch = _selectedSceneTargetTypes.Contains(ThingSceneTargetType.Urch);
            bool scanExploding = _selectedSceneTargetTypes.Contains(ThingSceneTargetType.ExplodingMushroom);
            bool scanGeyser = _selectedSceneTargetTypes.Contains(ThingSceneTargetType.Geyser);
            bool scanTrapChest = _selectedSceneTargetTypes.Contains(ThingSceneTargetType.TrapChest);
            if (!scanUrch && !scanExploding && !scanGeyser && !scanTrapChest)
            {
                return;
            }

            foreach (DisableBasedOnRunSettings target in FindObjectsByType<DisableBasedOnRunSettings>(FindObjectsSortMode.None))
            {
                if (target == null || !target.gameObject.activeInHierarchy)
                {
                    continue;
                }

                ThingSceneTargetType targetType;
                Vector3? fixedPosition = null;
                if (scanUrch && target.disableIfSettingDisabled == RunSettings.SETTINGTYPE.Hazard_Urchins &&
                    HasObjectNameInHierarchy(target, "Urch"))
                {
                    targetType = ThingSceneTargetType.Urch;
                }
                else if (scanExploding &&
                    target.disableIfSettingDisabled == RunSettings.SETTINGTYPE.Hazard_ExplodingMushrooms &&
                    HasObjectNameInHierarchy(target, "Forest_SporeFungus", "Jungle_SporeMushroom", "Jungle_SporeMushroomExplo"))
                {
                    targetType = ThingSceneTargetType.ExplodingMushroom;
                }
                else if (scanGeyser && target.disableIfSettingDisabled == RunSettings.SETTINGTYPE.Hazard_Geysers &&
                    HasObjectNameInHierarchy(target, "Geyser") && !HasObjectNameInHierarchy(target, "Eruption") &&
                    HasComponentInHierarchy<TriggerEvent>(target) && HasComponentInHierarchy<TimeEvent>(target) &&
                    HasComponentInHierarchy<MultipleGroundPoints>(target))
                {
                    targetType = ThingSceneTargetType.Geyser;
                    fixedPosition = GetMultipleGroundPointsPosition(target);
                }
                else if (scanTrapChest && target.disableIfSettingDisabled == RunSettings.SETTINGTYPE.Hazard_TrapChest &&
                    HasObjectNameInHierarchy(target, "LuggageTrick") && HasComponentInHierarchy<Luggage>(target) &&
                    HasComponentInHierarchy<Peak.TrickLuggage>(target) && HasComponentInHierarchy<SpineCheck>(target))
                {
                    targetType = ThingSceneTargetType.TrapChest;
                }
                else
                {
                    continue;
                }

                DisableBasedOnRunSettings captured = target;
                ThingSceneTargetType capturedType = targetType;
                Vector3 capturedPosition = fixedPosition ?? captured.transform.position;
                AddSceneLabel(seen, capturedType, captured,
                    delegate { return ThingCatalog.GetSceneTargetDisplayName(capturedType, _nameLanguage.Value); },
                    delegate
                    {
                        return captured != null && captured.gameObject.activeInHierarchy &&
                            _selectedSceneTargetTypes.Contains(capturedType);
                    }, fixedPosition.HasValue ? (Func<Vector3>)(() => capturedPosition) : null);
            }
        }

        private void RefreshAmuletStatueLabels(HashSet<string> seen)
        {
            if ((_effectiveScopes & ThingLocationScope.Statue) == 0 || _selectedIds.Count == 0)
            {
                return;
            }

            foreach (Peak.PropSpawner_AmuletStatues statue in Resources.FindObjectsOfTypeAll<Peak.PropSpawner_AmuletStatues>())
            {
                if (statue == null || !statue.gameObject.scene.IsValid() || !statue.gameObject.activeInHierarchy ||
                    statue.transform.childCount == 0)
                {
                    continue;
                }

                GameObject statueObject = statue.transform.GetChild(0).gameObject;
                if (statueObject == null || !statueObject.activeInHierarchy)
                {
                    continue;
                }

                FakeItem statueFragment = FindAmuletStatueFakeItem(statue, statueObject);
                if (statueFragment == null)
                {
                    continue;
                }

                ushort itemId;
                Item definition;
                if (!TryGetAmuletStatueItem(statue, statueObject, statueFragment, out itemId, out definition) || !_selectedIds.Contains(itemId))
                {
                    continue;
                }

                Peak.PropSpawner_AmuletStatues capturedStatue = statue;
                GameObject capturedStatueObject = statueObject;
                FakeItem capturedStatueFragment = statueFragment;
                Item capturedDefinition = definition;
                ushort capturedItemId = itemId;
                string key = "statue:" + capturedStatue.GetInstanceID();
                seen.Add(key);
                Transform target = capturedStatueFragment.transform;
                AddLabel(key, target,
                    delegate { return GetAmuletStatueLabelName(capturedDefinition); },
                    delegate
                    {
                        return capturedStatue != null && capturedStatue.gameObject.activeInHierarchy &&
                            capturedStatueObject != null && capturedStatueObject.activeInHierarchy &&
                            capturedStatueFragment != null && capturedStatueFragment.gameObject.activeInHierarchy &&
                            !capturedStatueFragment.pickedUp &&
                            _selectedIds.Contains(capturedItemId) &&
                            (_effectiveScopes & ThingLocationScope.Statue) != 0;
                    });
            }
        }

        private static FakeItem FindAmuletStatueFakeItem(Peak.PropSpawner_AmuletStatues statue, GameObject statueObject)
        {
            if (statueObject == null)
            {
                return null;
            }

            int statueAmuletIndex;
            bool hasStatueAmuletIndex = TryGetAmuletIndexFromStatue(statue, statueObject, out statueAmuletIndex);
            FakeItem fallback = null;
            FakeItem[] fakeItems = statueObject.GetComponentsInChildren<FakeItem>(true);
            foreach (FakeItem fakeItem in fakeItems)
            {
                if (fakeItem == null)
                {
                    continue;
                }

                if (fallback == null)
                {
                    fallback = fakeItem;
                }

                int fakeAmuletIndex;
                if (hasStatueAmuletIndex && fakeItem.realItemPrefab != null &&
                    TryGetAmuletIndex(fakeItem.realItemPrefab, out fakeAmuletIndex) &&
                    fakeAmuletIndex == statueAmuletIndex)
                {
                    return fakeItem;
                }
            }

            return fakeItems.Length == 1 ? fallback : null;
        }

        private bool TryGetAmuletStatueItem(Peak.PropSpawner_AmuletStatues statue, GameObject statueObject, FakeItem statueFragment, out ushort itemId, out Item definition)
        {
            itemId = ushort.MaxValue;
            definition = null;

            Item fragmentPrefab = statueFragment != null ? statueFragment.realItemPrefab : null;
            int fragmentIndex;
            if (fragmentPrefab != null && TryGetAmuletIndex(fragmentPrefab, out fragmentIndex))
            {
                definition = fragmentPrefab;
                itemId = definition.itemID;
                return true;
            }

            int amuletIndex;
            if (!TryGetAmuletIndexFromStatue(statue, statueObject, out amuletIndex))
            {
                return false;
            }

            definition = FindAmuletDefinition(amuletIndex);
            if (definition == null)
            {
                return false;
            }

            itemId = definition.itemID;
            return true;
        }

        private static bool TryGetAmuletIndexFromStatue(Peak.PropSpawner_AmuletStatues statue, GameObject statueObject, out int index)
        {
            index = -1;
            if (TryGetAmuletIndexFromName(statueObject == null ? null : statueObject.name, out index))
            {
                return true;
            }

            if (statue != null && statue.props != null && statue.props.Length == 4 && statue.statueIndex >= 0 && statue.statueIndex < 4)
            {
                index = statue.statueIndex;
                return true;
            }

            return false;
        }

        private static bool TryGetAmuletIndexFromName(string value, out int index)
        {
            index = -1;
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            string name = value.ToLowerInvariant();
            if (name.Contains("doublejump") || name.Contains("double_jump") || name.Contains("superjump") || name.Contains("initiative"))
            {
                index = 0;
                return true;
            }
            if (name.Contains("infinitestam") || name.Contains("infinite_stam") || name.Contains("stamina") || name.Contains("ambition"))
            {
                index = 1;
                return true;
            }
            if (name.Contains("healing") || name.Contains("heal") || name.Contains("tenacity"))
            {
                index = 2;
                return true;
            }
            if (name.Contains("clone") || name.Contains("generosity"))
            {
                index = 3;
                return true;
            }

            return false;
        }

        private static bool TryGetAmuletIndex(Item item, out int index)
        {
            index = -1;
            if (item == null || !item.TryGetComponent<Peak.AmuletBase>(out Peak.AmuletBase amulet))
            {
                return false;
            }

            index = amulet.amuletIndex;
            return index >= 0 && index < 4;
        }

        private Item FindAmuletDefinition(int amuletIndex)
        {
            foreach (ThingTargetDefinition definition in _catalog)
            {
                if (definition == null || definition.IsLuggage || definition.IsSceneTarget)
                {
                    continue;
                }

                foreach (Item item in definition.Prefabs)
                {
                    int index;
                    if (item != null && TryGetAmuletIndex(item, out index) && index == amuletIndex)
                    {
                        return item;
                    }
                }
            }

            return null;
        }

        private string GetAmuletStatueLabelName(Item definition)
        {
            string suffix = ThingCatalog.GetStatueSuffix(_nameLanguage.Value);
            if (definition == null)
            {
                return suffix;
            }

            return ThingCatalog.GetDisplayName(definition, _nameLanguage.Value) + "\n" + suffix;
        }


















        private string GetPlacedItemName(Item definition, string fallbackName)
        {
            return definition == null ? fallbackName : ThingCatalog.GetDisplayName(definition, _nameLanguage.Value);
        }


        private Item FindCatalogItemPrefab(params string[] itemPrefabNames)
        {
            if (itemPrefabNames == null || itemPrefabNames.Length == 0)
            {
                return null;
            }

            foreach (string itemPrefabName in itemPrefabNames)
            {
                if (string.IsNullOrWhiteSpace(itemPrefabName))
                {
                    continue;
                }
                Item definition;
                if (_itemDefinitionsByPrefabName.TryGetValue(NormalizeObjectName(itemPrefabName), out definition))
                {
                    return definition;
                }
            }

            return null;
        }


        private void BuildCatalogIndexes()
        {
            _itemDefinitionsByPrefabName.Clear();
            foreach (ThingTargetDefinition targetDefinition in _catalog)
            {
                if (targetDefinition == null || targetDefinition.IsLuggage || targetDefinition.IsSceneTarget)
                {
                    continue;
                }

                foreach (Item prefab in targetDefinition.Prefabs)
                {
                    if (prefab == null || prefab.gameObject == null)
                    {
                        continue;
                    }

                    _itemDefinitionsByPrefabName[NormalizeObjectName(prefab.gameObject.name)] = prefab;
                }
            }
            BuildPlacedSourceIndexes();
        }
    }
}
