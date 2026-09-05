using System;
using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace WhereIsThing
{
    public sealed partial class WhereIsThingPlugin
    {
        private void StartPhotonDiscoveryCycle()
        {
            if (_photonDiscoveryActive)
            {
                return;
            }

            _photonViewIterator = PhotonNetwork.PhotonViewCollection;
            _photonViewIterator.Reset();
            _photonDiscoveryGeneration++;
            _photonDiscoveryActive = true;
            _nextNetworkDiscovery = Time.unscaledTime + NetworkDiscoveryInterval;
        }

        private void ProcessPhotonDiscovery()
        {
            ProcessClassificationRetries();
            if (!_photonDiscoveryActive)
            {
                if (PhotonNetwork.ViewCount != _lastPhotonViewCount || Time.unscaledTime >= _nextNetworkDiscovery)
                {
                    StartPhotonDiscoveryCycle();
                }
                else
                {
                    return;
                }
            }

            long startedAt = Stopwatch.GetTimestamp();
            int processed = 0;
            while (processed < NetworkObjectsPerFrame)
            {
                if (!_photonViewIterator.MoveNext())
                {
                    _photonDiscoveryActive = false;
                    _lastPhotonViewCount = PhotonNetwork.ViewCount;
                    CleanupNetworkObjectsIfDue();
                    break;
                }

                PhotonView view = _photonViewIterator.Current;
                processed++;
                if (view != null && view.gameObject != null)
                {
                    IndexAndSyncPhotonView(view);
                }

                double elapsedMilliseconds = (Stopwatch.GetTimestamp() - startedAt) * 1000.0 / Stopwatch.Frequency;
                if (elapsedMilliseconds >= DiscoveryBudgetMilliseconds)
                {
                    break;
                }
            }
        }

        private void IndexAndSyncPhotonView(PhotonView view)
        {
            int viewId = view.ViewID;
            if (viewId == 0)
            {
                viewId = -view.GetInstanceID();
            }

            NetworkObjectRecord record;
            if (!_networkObjects.TryGetValue(viewId, out record) || record.View != view)
            {
                if (record != null)
                {
                    RemoveNetworkRecordLabels(record);
                    RemoveClassificationRetry(record);
                }

                record = BuildNetworkObjectRecord(view);
                _networkObjects[viewId] = record;
                if (record.NeedsLateClassification)
                {
                    _classificationRetries.Add(record);
                }
            }
            record.LastSeenGeneration = _photonDiscoveryGeneration;
            SyncNetworkObject(record);
        }

        private NetworkObjectRecord BuildNetworkObjectRecord(PhotonView view)
        {
            Item hierarchyItem = view.GetComponentInParent<Item>(true);
            bool playerCreated = IsPlayerCreatedView(view);
            NetworkObjectRecord record = new NetworkObjectRecord
            {
                View = view,
                Item = hierarchyItem != null && GetNearestNetworkView(hierarchyItem) == view ? hierarchyItem : null,
                FirstSeenAt = Time.unscaledTime
            };

            PlacedPrefabSource source;
            Transform sourceRoot;
            bool awaitingThrownOwner = false;

            MushroomBounceBadgeTracker bounceShroom = FindComponentInViewHierarchy<MushroomBounceBadgeTracker>(view);
            if (bounceShroom != null && TryGetPlacedSource(view, PlacedSourceKind.BounceShroom, out source, out sourceRoot))
            {
                int ownerActorNumber = MatchThrownOwner(source, bounceShroom.transform.position);
                record.PreferPlacedItemLabel = AddPlacedTarget(record, "bounce-shroom", bounceShroom, view,
                    source.SourceItem, "Bounce Shroom", ThingSceneTargetType.BounceShroomPlaced, null,
                    ownerActorNumber, false) != null;
                awaitingThrownOwner |= !view.IsRoomView && ownerActorNumber <= 0;
            }

            if (TryGetPlacedSource(view, PlacedSourceKind.ShelfShroom, out source, out sourceRoot))
            {
                Component shelfTarget = sourceRoot.GetComponentInChildren<ShelfShroom>(true) ?? (Component)sourceRoot;
                int ownerActorNumber = MatchThrownOwner(source, shelfTarget.transform.position);
                record.PreferPlacedItemLabel = AddPlacedTarget(record, "shelf-shroom", shelfTarget, view,
                    source.SourceItem, "Shelf Shroom", null, null,
                    ownerActorNumber, false) != null ||
                    record.PreferPlacedItemLabel;
                awaitingThrownOwner |= !view.IsRoomView && ownerActorNumber <= 0;
            }

            if (TryGetPlacedSource(view, PlacedSourceKind.CloudFungus, out source, out sourceRoot))
            {
                Component cloudTarget = sourceRoot.GetComponentInChildren<CloudFungus>(true) ?? (Component)sourceRoot;
                int ownerActorNumber = MatchThrownOwner(source, cloudTarget.transform.position);
                record.PreferPlacedItemLabel = AddPlacedTarget(record, "cloud-fungus", cloudTarget, view,
                    source.SourceItem, "Cloud Fungus", null, null,
                    ownerActorNumber, false) != null ||
                    record.PreferPlacedItemLabel;
                awaitingThrownOwner |= !view.IsRoomView && ownerActorNumber <= 0;
            }

            if (playerCreated)
            {
                CheckpointFlag checkpoint = FindComponentInViewHierarchy<CheckpointFlag>(view);
                if (checkpoint != null && TryGetPlacedSource(view, PlacedSourceKind.CheckpointFlag, out source, out sourceRoot))
                {
                    AddPlacedTarget(record, "checkpoint", checkpoint, view, source.SourceItem,
                        "Checkpoint Flag", ThingSceneTargetType.CheckpointFlagPlaced);
                }

                ScoutCannon scoutCannon = FindComponentInViewHierarchy<ScoutCannon>(view);
                if (scoutCannon != null && TryGetPlacedSource(view, PlacedSourceKind.ScoutCannon, out source, out sourceRoot))
                {
                    AddPlacedTarget(record, "scout-cannon", scoutCannon, view, source.SourceItem, "Scout Cannon", null);
                }

                JungleVine chainVine = FindComponentInViewHierarchy<JungleVine>(view);
                if (chainVine != null && !IsBridgeVine(chainVine) &&
                    TryGetPlacedSource(view, PlacedSourceKind.ChainLauncher, out source, out sourceRoot))
                {
                    AddPlacedTarget(record, "chain-shooter", chainVine, view, source.SourceItem, "Chain Launcher", null,
                        () => chainVine.hangCenter == null ? chainVine.transform.position : chainVine.hangCenter.position);
                }

                Rope rope = FindComponentInViewHierarchy<Rope>(view);
                RopeAnchor ropeAnchor = GetAttachedRopeAnchor(rope);
                PhotonView ropeView = ResolveOwnerView(rope, view);
                PhotonView anchorView = ropeAnchor == null ? null : ResolveOwnerView(ropeAnchor, ropeAnchor.photonView);
                if (rope != null && rope.attachmenState == Rope.ATTACHMENT.anchored && !IsExcludedRope(rope, ropeAnchor) &&
                    ropeView == view && anchorView != null &&
                    TryGetPlacedSource(anchorView, PlacedSourceKind.Rope, out source, out sourceRoot))
                {
                    PlacedTargetRecord ropeRecord = AddPlacedTarget(record, "rope-spool-" + source.SpawnPrefabName,
                        rope, anchorView, source.SourceItem, "Rope", ThingSceneTargetType.RopePlaced);
                    if (ropeRecord != null)
                    {
                        ropeRecord.OwnerView = anchorView;
                        ropeRecord.OwnerActorNumber = GetActorNumber(anchorView);
                        ropeRecord.ShowOwner = ropeRecord.OwnerActorNumber > 0;
                        ropeRecord.Rope = rope;
                        ropeRecord.IsRope = true;
                        ropeRecord.RopeAnchor = ropeAnchor;
                        ropeRecord.PositionProvider = delegate
                        {
                            return ropeRecord.RopeAnchor != null && ropeRecord.RopeAnchor.anchorPoint != null
                                ? ropeRecord.RopeAnchor.anchorPoint.position
                                : rope.transform.position;
                        };
                    }
                }

                ClimbHandle pitonHandle = FindComponentInViewHierarchy<ClimbHandle>(view);
                ShittyPiton piton = FindComponentInViewHierarchy<ShittyPiton>(view);
                Component pitonTarget = (Component)pitonHandle ?? piton;
                PlacedTargetRecord pitonRecord = null;
                if (pitonTarget != null && TryGetPlacedSource(view, PlacedSourceKind.Piton, out source, out sourceRoot))
                {
                    pitonRecord = AddPlacedTarget(record, "piton", pitonTarget, view,
                        source.SourceItem, "Piton", ThingSceneTargetType.PitonPlaced);
                }
                if (pitonRecord != null)
                {
                    pitonRecord.PitonHandle = pitonHandle;
                }
            }

            MagicBeanVine magicBeanVine = FindComponentInViewHierarchy<MagicBeanVine>(view);
            PlacedTargetRecord beanRecord = AddPlacedTarget(record, "magic-bean-vine", magicBeanVine, view,
                FindCatalogItemPrefab("MagicBean"), "Magic Bean Vine", ThingSceneTargetType.MagicBeanVine);
            if (beanRecord != null)
            {
                beanRecord.ShowOwner = false;
            }

            record.NeedsLateClassification = !view.IsRoomView &&
                (record.PlacedTargets.Count == 0 || awaitingThrownOwner);

            return record;
        }

        private void ProcessClassificationRetries()
        {
            float now = Time.unscaledTime;
            long startedAt = Stopwatch.GetTimestamp();
            int processed = 0;
            int inspected = 0;
            int inspectionLimit = Math.Min(_classificationRetries.Count, NetworkObjectsPerFrame);
            while (_classificationRetries.Count > 0 && inspected < inspectionLimit &&
                processed < ClassificationRetriesPerFrame)
            {
                if (_classificationRetryCursor >= _classificationRetries.Count)
                {
                    _classificationRetryCursor = 0;
                }
                int index = _classificationRetryCursor;
                NetworkObjectRecord record = _classificationRetries[index];
                inspected++;
                if (record == null || !record.NeedsLateClassification || record.View == null ||
                    record.NextRetryIndex >= ClassificationRetryDelays.Length || now - record.FirstSeenAt > 2.1f)
                {
                    _classificationRetries.RemoveAt(index);
                    continue;
                }
                if (now - record.FirstSeenAt < ClassificationRetryDelays[record.NextRetryIndex])
                {
                    _classificationRetryCursor++;
                    continue;
                }

                processed++;
                record.NextRetryIndex++;
                int viewId = record.View.ViewID == 0 ? -record.View.GetInstanceID() : record.View.ViewID;
                RemoveNetworkRecordLabels(record);
                NetworkObjectRecord replacement = BuildNetworkObjectRecord(record.View);
                replacement.FirstSeenAt = record.FirstSeenAt;
                replacement.NextRetryIndex = record.NextRetryIndex;
                replacement.LastSeenGeneration = record.LastSeenGeneration;
                _networkObjects[viewId] = replacement;
                SyncNetworkObject(replacement);
                if (!replacement.NeedsLateClassification || replacement.NextRetryIndex >= ClassificationRetryDelays.Length)
                {
                    _classificationRetries.RemoveAt(index);
                }
                else
                {
                    _classificationRetries[index] = replacement;
                    _classificationRetryCursor++;
                }

                double elapsedMilliseconds = (Stopwatch.GetTimestamp() - startedAt) * 1000.0 / Stopwatch.Frequency;
                if (elapsedMilliseconds >= ClassificationRetryBudgetMilliseconds)
                {
                    break;
                }
            }
        }

        private static bool IsBridgeVine(JungleVine vine)
        {
            if (vine == null)
            {
                return false;
            }

            return vine.GetComponentInParent<BreakableBridge>(true) != null ||
                vine.GetComponentInChildren<BreakableBridge>(true) != null;
        }

        private PlacedTargetRecord AddPlacedTarget(NetworkObjectRecord record, string keyPrefix, Component target,
            PhotonView view, Item definition, string fallbackName, ThingSceneTargetType? legacySceneTargetType,
            Func<Vector3> positionProvider = null, int ownerActorNumber = 0,
            bool allowViewOwner = true)
        {
            if (target == null || view == null)
            {
                return null;
            }

            bool magicBean = legacySceneTargetType == ThingSceneTargetType.MagicBeanVine;
            if ((!magicBean && allowViewOwner && !IsPlayerCreatedView(view)) ||
                (magicBean && GetNearestNetworkView(target) != view))
            {
                return null;
            }

            PlacedTargetRecord placed = new PlacedTargetRecord
            {
                Key = "placed:" + keyPrefix + ":" + view.ViewID,
                FallbackName = fallbackName,
                Target = target,
                Definition = definition,
                View = view,
                OwnerView = allowViewOwner ? ResolveOwnerView(target, view) : null,
                OwnerActorNumber = ownerActorNumber,
                LegacySceneTargetType = legacySceneTargetType,
                PositionProvider = positionProvider
            };
            if (placed.OwnerActorNumber <= 0 && allowViewOwner)
            {
                placed.OwnerActorNumber = GetActorNumber(placed.OwnerView);
            }
            placed.ShowOwner = placed.OwnerActorNumber > 0;
            record.PlacedTargets.Add(placed);
            return placed;
        }

        private static T FindComponentInViewHierarchy<T>(PhotonView view) where T : Component
        {
            if (view == null)
            {
                return null;
            }

            T target = view.GetComponentInParent<T>(true);
            return target != null ? target : view.GetComponentInChildren<T>(true);
        }

        private static PhotonView GetNearestNetworkView(Component target)
        {
            if (target == null)
            {
                return null;
            }

            PhotonView view = target.GetComponentInParent<PhotonView>(true);
            return view != null ? view : target.GetComponentInChildren<PhotonView>(true);
        }

        private void SyncNetworkObject(NetworkObjectRecord record)
        {
            record.DesiredLabelKeys.Clear();
            SyncNetworkItem(record);
            foreach (PlacedTargetRecord placed in record.PlacedTargets)
            {
                SyncPlacedTarget(record, placed);
            }
            ReconcileNetworkRecordLabels(record);
        }

        private void SyncNetworkItem(NetworkObjectRecord record)
        {
            Item item = record.Item;
            if (item == null || item.gameObject == null || !item.gameObject.activeInHierarchy ||
                record.PreferPlacedItemLabel || ShouldPreferMobSceneLabel(item))
            {
                return;
            }

            if (_selectedIds.Contains(item.itemID))
            {
                if ((_effectiveScopes & ThingLocationScope.Ground) != 0 && item.itemState == ItemState.Ground)
                {
                    string key = "item:" + item.GetInstanceID();
                    AddNetworkLabel(record, key, item.transform,
                        delegate { return ThingCatalog.GetDisplayName(item, _nameLanguage.Value); });
                }
                else if ((_effectiveScopes & ThingLocationScope.Held) != 0 && item.itemState == ItemState.Held &&
                    ShouldShowHeldItem(item))
                {
                    string key = "held:" + item.GetInstanceID();
                    AddNetworkLabel(record, key, item.transform,
                        delegate { return ThingCatalog.GetDisplayName(item, _nameLanguage.Value); });
                }
            }

            Backpack backpack = item as Backpack;
            BackpackData backpackData;
            if ((_effectiveScopes & ThingLocationScope.Backpack) == 0 || backpack == null ||
                backpack.itemState != ItemState.Ground || !TryGetBackpackData(backpack.data, out backpackData))
            {
                return;
            }

            foreach (ItemSlot slot in backpackData.itemSlots)
            {
                if (slot == null || slot.IsEmpty() || slot.prefab == null || !_selectedIds.Contains(slot.prefab.itemID))
                {
                    continue;
                }

                Item contentPrefab = slot.prefab;
                string key = "backpack:" + backpack.GetInstanceID() + ":" + slot.itemSlotID;
                AddNetworkLabel(record, key, backpack.transform, delegate
                {
                    return ThingCatalog.GetDisplayName(contentPrefab, _nameLanguage.Value) + "\n" +
                        ThingCatalog.GetContainerSuffix(_nameLanguage.Value);
                });
            }
        }

        private void AddNetworkLabel(NetworkObjectRecord record, string key, Transform target, Func<string> titleProvider)
        {
            record.DesiredLabelKeys.Add(key);
            record.LabelKeys.Add(key);
            AddLabel(key, target, titleProvider,
                delegate { return target != null && target.gameObject != null && target.gameObject.activeInHierarchy; });
        }

        private void SyncPlacedTarget(NetworkObjectRecord record, PlacedTargetRecord placed)
        {
            RefreshPlacedRecordState(placed);
            if (!IsPlacedRecordValid(placed) || !IsPlacedRecordSelected(placed))
            {
                return;
            }

            record.DesiredLabelKeys.Add(placed.Key);
            record.LabelKeys.Add(placed.Key);
            Func<bool> isValid = delegate
            {
                return placed.Target != null && placed.Target.gameObject != null &&
                    placed.Target.gameObject.activeInHierarchy && placed.View != null &&
                    placed.View.gameObject != null && placed.View.gameObject.activeInHierarchy;
            };
            Func<string> titleProvider = delegate { return GetPlacedItemName(placed.Definition, placed.FallbackName); };
            if (placed.ShowOwner)
            {
                AddLabel(placed.Key, placed.Target.transform, titleProvider, isValid, placed.PositionProvider,
                    delegate { return GetOwnerName(placed.OwnerActorNumber, placed.OwnerView); });
            }
            else
            {
                AddLabel(placed.Key, placed.Target.transform, titleProvider, isValid, placed.PositionProvider);
            }
        }

        private void RefreshPlacedRecordState(PlacedTargetRecord placed)
        {
            if (placed == null || placed.Rope == null)
            {
                return;
            }

            RopeAnchor anchor = GetAttachedRopeAnchor(placed.Rope);
            if (anchor == null || anchor.anchorPoint == null)
            {
                placed.RopeAnchor = anchor;
                return;
            }

            if (placed.RopeAnchor == anchor)
            {
                return;
            }

            placed.RopeAnchor = anchor;
            placed.Target = anchor;
        }

        private bool IsPlacedRecordSelected(PlacedTargetRecord placed)
        {
            if (placed.Definition != null && _selectedIds.Contains(placed.Definition.itemID))
            {
                return true;
            }

            return placed.LegacySceneTargetType.HasValue &&
                _selectedSceneTargetTypes.Contains(placed.LegacySceneTargetType.Value);
        }

        private bool IsPlacedRecordValid(PlacedTargetRecord placed)
        {
            if (placed == null || placed.Target == null || placed.Target.gameObject == null ||
                !placed.Target.gameObject.activeInHierarchy || placed.View == null || placed.View.gameObject == null ||
                !placed.View.gameObject.activeInHierarchy)
            {
                return false;
            }

            if (placed.IsRope)
            {
                return placed.Rope != null && placed.RopeAnchor != null && placed.RopeAnchor.anchorPoint != null &&
                    placed.Rope.attachmenState == Rope.ATTACHMENT.anchored &&
                    GetAttachedRopeAnchor(placed.Rope) == placed.RopeAnchor &&
                    !IsExcludedRope(placed.Rope, placed.RopeAnchor);
            }

            return placed.PitonHandle == null || IsPitonActive(placed.PitonHandle);
        }

        private void ReconcileNetworkRecordLabels(NetworkObjectRecord record)
        {
            _labelKeyRemovalBuffer.Clear();
            foreach (string key in record.LabelKeys)
            {
                if (!record.DesiredLabelKeys.Contains(key))
                {
                    _labelKeyRemovalBuffer.Add(key);
                }
            }
            foreach (string key in _labelKeyRemovalBuffer)
            {
                RemoveLabel(key);
                record.LabelKeys.Remove(key);
            }
        }

        private void CleanupNetworkObjectsIfDue()
        {
            if (Time.unscaledTime < _nextNetworkCleanup)
            {
                return;
            }

            _nextNetworkCleanup = Time.unscaledTime + FullValidationInterval;
            _networkRecordRemovalBuffer.Clear();
            foreach (KeyValuePair<int, NetworkObjectRecord> entry in _networkObjects)
            {
                NetworkObjectRecord record = entry.Value;
                if (record == null || record.LastSeenGeneration != _photonDiscoveryGeneration ||
                    record.View == null || record.View.gameObject == null)
                {
                    _networkRecordRemovalBuffer.Add(entry.Key);
                }
            }

            foreach (int viewId in _networkRecordRemovalBuffer)
            {
                NetworkObjectRecord record;
                if (_networkObjects.TryGetValue(viewId, out record))
                {
                    RemoveNetworkRecordLabels(record);
                    RemoveClassificationRetry(record);
                }
                _networkObjects.Remove(viewId);
            }
        }

        private void RemoveClassificationRetry(NetworkObjectRecord record)
        {
            for (int i = _classificationRetries.Count - 1; i >= 0; i--)
            {
                if (ReferenceEquals(_classificationRetries[i], record))
                {
                    _classificationRetries.RemoveAt(i);
                    if (_classificationRetryCursor > i)
                    {
                        _classificationRetryCursor--;
                    }
                }
            }
        }

        private void RemoveNetworkRecordLabels(NetworkObjectRecord record)
        {
            if (record == null)
            {
                return;
            }
            foreach (string key in record.LabelKeys)
            {
                RemoveLabel(key);
            }
            record.LabelKeys.Clear();
            record.DesiredLabelKeys.Clear();
        }

        private void ClearDiscoveryCaches()
        {
            _photonDiscoveryActive = false;
            _networkObjects.Clear();
            _classificationRetries.Clear();
            _classificationRetryCursor = 0;
            _thrownItemEvidence.Clear();
            _peakSequences.Clear();
            _sceneDiscoveryStep = SceneDiscoveryStepCount;
            _nextNetworkDiscovery = 0f;
            _lastPhotonViewCount = -1;
            _nextNetworkCleanup = 0f;
            _nextSceneDiscovery = 0f;
        }
    }
}
