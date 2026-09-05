using System;
using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

namespace WhereIsThing
{
    public sealed partial class WhereIsThingPlugin
    {
        private enum PlacedSourceKind
        {
            Rope,
            ChainLauncher,
            ShelfShroom,
            BounceShroom,
            CloudFungus,
            Piton,
            CheckpointFlag,
            ScoutCannon
        }

        private sealed class PlacedPrefabSource
        {
            public PlacedSourceKind Kind;
            public Item SourceItem;
            public string SpawnPrefabName;
        }

        private sealed class ThrownItemEvidence
        {
            public PlacedSourceKind Kind;
            public ushort ItemId;
            public string SpawnPrefabName;
            public int ActorNumber;
            public Vector3 Position;
            public float ThrownAt;
            public int SceneGeneration;
            public bool Matched;
        }

        private void BuildPlacedSourceIndexes()
        {
            _placedSourcesByPrefabName.Clear();
            _throwableSourcesByItemId.Clear();

            foreach (Item item in _itemDefinitionsByPrefabName.Values)
            {
                if (item == null || item.gameObject == null)
                {
                    continue;
                }

                foreach (VineShooter component in item.GetComponentsInChildren<VineShooter>(true))
                {
                    AddPlacedSource(item, component.vinePrefab, PlacedSourceKind.ChainLauncher, false);
                }
                foreach (RopeShooter component in item.GetComponentsInChildren<RopeShooter>(true))
                {
                    AddPlacedSource(item, component.ropeAnchorWithRopePref, PlacedSourceKind.Rope, false);
                }
                foreach (RopeTier component in item.GetComponentsInChildren<RopeTier>(true))
                {
                    AddPlacedSource(item, component.anchorPrefab, PlacedSourceKind.Rope, false);
                }
                foreach (ShelfShroom component in item.GetComponentsInChildren<ShelfShroom>(true))
                {
                    GameObject spawned = component.instantiateOnBreak;
                    PlacedSourceKind kind = spawned != null &&
                        (spawned.GetComponentInChildren<MushroomBounceBadgeTracker>(true) != null ||
                         NormalizeObjectName(spawned.name).IndexOf("Bounce", StringComparison.OrdinalIgnoreCase) >= 0)
                        ? PlacedSourceKind.BounceShroom
                        : PlacedSourceKind.ShelfShroom;
                    AddPlacedSource(item, spawned, kind, true);
                }
                foreach (CloudFungus component in item.GetComponentsInChildren<CloudFungus>(true))
                {
                    AddPlacedSource(item, component.instantiateOnBreak, PlacedSourceKind.CloudFungus, true);
                }
                foreach (ClimbingSpikeComponent component in item.GetComponentsInChildren<ClimbingSpikeComponent>(true))
                {
                    AddPlacedSource(item, component.hammeredVersionPrefab, PlacedSourceKind.Piton, false);
                }
                foreach (Constructable component in item.GetComponentsInChildren<Constructable>(true))
                {
                    GameObject spawned = component.constructedPrefab;
                    if (spawned != null && spawned.GetComponentInChildren<CheckpointFlag>(true) != null)
                    {
                        AddPlacedSource(item, spawned, PlacedSourceKind.CheckpointFlag, false);
                    }
                    if (spawned != null && spawned.GetComponentInChildren<ScoutCannon>(true) != null)
                    {
                        AddPlacedSource(item, spawned, PlacedSourceKind.ScoutCannon, false);
                    }
                }
            }
        }

        private void AddPlacedSource(Item item, GameObject spawnedPrefab, PlacedSourceKind kind, bool throwable)
        {
            if (item == null || spawnedPrefab == null)
            {
                return;
            }

            string prefabName = NormalizeObjectName(spawnedPrefab.name);
            if (string.IsNullOrEmpty(prefabName))
            {
                return;
            }

            List<PlacedPrefabSource> byPrefab;
            if (!_placedSourcesByPrefabName.TryGetValue(prefabName, out byPrefab))
            {
                byPrefab = new List<PlacedPrefabSource>();
                _placedSourcesByPrefabName.Add(prefabName, byPrefab);
            }

            foreach (PlacedPrefabSource existing in byPrefab)
            {
                if (existing.Kind == kind && existing.SourceItem != null && existing.SourceItem.itemID == item.itemID)
                {
                    return;
                }
            }

            PlacedPrefabSource source = new PlacedPrefabSource
            {
                Kind = kind,
                SourceItem = item,
                SpawnPrefabName = prefabName
            };
            byPrefab.Add(source);

            if (!throwable)
            {
                return;
            }

            List<PlacedPrefabSource> byItem;
            if (!_throwableSourcesByItemId.TryGetValue(item.itemID, out byItem))
            {
                byItem = new List<PlacedPrefabSource>();
                _throwableSourcesByItemId.Add(item.itemID, byItem);
            }
            byItem.Add(source);
        }

        private bool TryGetPlacedSource(PhotonView view, PlacedSourceKind kind,
            out PlacedPrefabSource source, out Transform sourceRoot)
        {
            source = null;
            sourceRoot = null;
            if (view == null || view.gameObject == null)
            {
                return false;
            }

            Transform current = view.transform;
            for (int depth = 0; current != null && depth < 5; depth++, current = current.parent)
            {
                if (TryGetPlacedSource(current, kind, out source))
                {
                    sourceRoot = current;
                    return true;
                }
                PhotonView parentView = current.parent == null ? null : current.parent.GetComponent<PhotonView>();
                if (parentView != null && parentView != view)
                {
                    break;
                }
            }

            foreach (Transform child in view.GetComponentsInChildren<Transform>(true))
            {
                if (TryGetPlacedSource(child, kind, out source))
                {
                    sourceRoot = child;
                    return true;
                }
            }
            return false;
        }

        private bool TryGetPlacedSource(Transform target, PlacedSourceKind kind, out PlacedPrefabSource source)
        {
            source = null;
            if (target == null || target.gameObject == null)
            {
                return false;
            }

            List<PlacedPrefabSource> candidates;
            if (!_placedSourcesByPrefabName.TryGetValue(NormalizeObjectName(target.gameObject.name), out candidates))
            {
                return false;
            }

            foreach (PlacedPrefabSource candidate in candidates)
            {
                if (candidate.Kind == kind)
                {
                    source = candidate;
                    return true;
                }
            }
            return false;
        }

        private static int GetActorNumber(PhotonView view)
        {
            if (!IsPlayerCreatedView(view))
            {
                return 0;
            }
            return view.CreatorActorNr > 0 ? view.CreatorActorNr : view.Owner.ActorNumber;
        }

        private static PhotonView GetDirectPlayerView(Component target)
        {
            if (target == null)
            {
                return null;
            }

            PhotonView direct = target.GetComponent<PhotonView>();
            return IsPlayerCreatedView(direct) ? direct : null;
        }

        private static PhotonView ResolveOwnerView(Component target, PhotonView sourceView)
        {
            PhotonView direct = GetDirectPlayerView(target);
            if (direct != null)
            {
                return direct;
            }
            if (IsPlayerCreatedView(sourceView))
            {
                return sourceView;
            }
            return null;
        }

        private void SubscribeToItemThrown()
        {
            GlobalEvents.OnItemThrown -= OnItemThrown;
            GlobalEvents.OnItemThrown += OnItemThrown;
        }

        private void UnsubscribeFromItemThrown()
        {
            GlobalEvents.OnItemThrown -= OnItemThrown;
        }

        private IEnumerator RefreshSceneEventSubscriptions()
        {
            yield return null;
            SubscribeToItemThrown();
            _peakSequences.Clear();
            _peakSequences.AddRange(FindObjectsByType<PeakSequence>(FindObjectsSortMode.None));
        }

        private void OnItemThrown(Item item)
        {
            if (item == null || item.gameObject == null)
            {
                return;
            }

            if (_throwableSourcesByItemId.Count == 0 && !TryLoadCatalog())
            {
                return;
            }

            List<PlacedPrefabSource> sources;
            if (!_throwableSourcesByItemId.TryGetValue(item.itemID, out sources) || sources.Count == 0)
            {
                return;
            }

            Character thrower = ItemLastThrownCharacterField == null
                ? null
                : ItemLastThrownCharacterField.GetValue(item) as Character;
            PhotonView throwerView = ResolveOwnerView(thrower, null);
            int actorNumber = GetActorNumber(throwerView);
            if (actorNumber <= 0)
            {
                return;
            }

            CleanupThrownItemEvidence();
            foreach (PlacedPrefabSource source in sources)
            {
                _thrownItemEvidence.Add(new ThrownItemEvidence
                {
                    Kind = source.Kind,
                    ItemId = item.itemID,
                    SpawnPrefabName = source.SpawnPrefabName,
                    ActorNumber = actorNumber,
                    Position = item.transform.position,
                    ThrownAt = Time.unscaledTime,
                    SceneGeneration = _sceneGeneration
                });
            }
            if (_thrownItemEvidence.Count > 64)
            {
                _thrownItemEvidence.RemoveRange(0, _thrownItemEvidence.Count - 64);
            }
        }

        private int MatchThrownOwner(PlacedPrefabSource source, Vector3 spawnedPosition)
        {
            if (source == null)
            {
                return 0;
            }

            CleanupThrownItemEvidence();
            ThrownItemEvidence match = null;
            int matches = 0;
            foreach (ThrownItemEvidence evidence in _thrownItemEvidence)
            {
                float age = Time.unscaledTime - evidence.ThrownAt;
                if (evidence.Matched || evidence.SceneGeneration != _sceneGeneration || age < 0f || age > 2f ||
                    evidence.Kind != source.Kind || evidence.ItemId != source.SourceItem.itemID ||
                    !string.Equals(evidence.SpawnPrefabName, source.SpawnPrefabName, StringComparison.OrdinalIgnoreCase) ||
                    Vector3.Distance(evidence.Position, spawnedPosition) > 12f)
                {
                    continue;
                }
                match = evidence;
                matches++;
                if (matches > 1)
                {
                    return 0;
                }
            }

            if (match == null)
            {
                return 0;
            }
            match.Matched = true;
            return match.ActorNumber;
        }

        private void CleanupThrownItemEvidence()
        {
            float now = Time.unscaledTime;
            for (int i = _thrownItemEvidence.Count - 1; i >= 0; i--)
            {
                ThrownItemEvidence evidence = _thrownItemEvidence[i];
                if (evidence == null || evidence.SceneGeneration != _sceneGeneration ||
                    now - evidence.ThrownAt > 2.5f)
                {
                    _thrownItemEvidence.RemoveAt(i);
                }
            }
        }

        private bool IsExcludedRope(Rope rope, RopeAnchor anchor)
        {
            if (rope == null || rope.isHelicopterRope ||
                HasComponentInParentsOrChildren<TempleEntranceRope>(rope) ||
                HasComponentInParentsOrChildren<BreakableRopeAnchor>(rope) ||
                HasComponentInParentsOrChildren<TempleEntranceRope>(anchor) ||
                HasComponentInParentsOrChildren<BreakableRopeAnchor>(anchor))
            {
                return true;
            }

            foreach (PeakSequence sequence in _peakSequences)
            {
                if (sequence != null && (sequence.ropeInstance == rope ||
                    (sequence.ropeAnchorInstance != null && sequence.ropeAnchorInstance.anchor == anchor)))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool HasComponentInParentsOrChildren<T>(Component target) where T : Component
        {
            return target != null && (target.GetComponentInParent<T>(true) != null ||
                target.GetComponentInChildren<T>(true) != null);
        }
    }
}
