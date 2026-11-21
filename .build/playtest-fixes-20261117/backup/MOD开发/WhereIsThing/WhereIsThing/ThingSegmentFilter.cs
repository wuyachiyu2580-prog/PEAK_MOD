using System;
using UnityEngine;
using Zorro.Core;

namespace WhereIsThing
{
    internal enum ThingMapScope { CurrentSegment, WholeMap }

    internal sealed class ThingSegmentFilter
    {
        private Transform[] _roots = new Transform[0];
        private Transform[] _camps = new Transform[0];
        private float[] _starts = new float[0];
        private int _current = -1;
        private MapHandler _map;
        private float _refreshAt;
        private bool _spatialReady;
        private int _progressSegmentCount;
        private readonly RaycastHit[] _groundHits = new RaycastHit[32];
        public int Revision { get; private set; }

        public void Reset()
        {
            _map = null;
            _current = -1;
            _roots = new Transform[0];
            _camps = new Transform[0];
            _starts = new float[0];
            _refreshAt = 0f;
            _spatialReady = false;
            _progressSegmentCount = 0;
            Revision++;
        }

        public void Refresh()
        {
            if (Time.unscaledTime < _refreshAt) return;
            _refreshAt = Time.unscaledTime + 0.25f;
            MapHandler map = Singleton<MapHandler>.Instance;
            if (map == null || map.segments == null || map.segments.Length == 0)
            {
                if (_map != null || _current >= 0) Reset();
                return;
            }
            MapHandler.MapSegment current;
            try { current = MapHandler.CurrentMapSegment; }
            catch (IndexOutOfRangeException)
            {
                _map = map;
                if (_current != -1) Revision++;
                _current = -1;
                return;
            }
            int index = Array.IndexOf(map.segments, current);
            bool changed = _map != map || _current != index || _roots.Length != map.segments.Length;
            _map = map;
            _current = index;
            if (_roots.Length != map.segments.Length)
            {
                _roots = new Transform[map.segments.Length];
                _camps = new Transform[map.segments.Length];
                _starts = new float[map.segments.Length];
            }
            var progress = Singleton<MountainProgressHandler>.Instance;
            bool oldSpatialReady = _spatialReady;
            _spatialReady = true;
            _progressSegmentCount = map.segments.Length;
            for (int i = 0; i < map.segments.Length; i++)
            {
                var segment = map.segments[i];
                if (segment != null && segment.biome == Biome.BiomeType.Void)
                    _progressSegmentCount = Math.Min(_progressSegmentCount, i);
                Transform root = segment == null || segment.segmentParent == null ? null : segment.segmentParent.transform;
                Transform camp = segment == null || segment.segmentCampfire == null ? null : segment.segmentCampfire.transform;
                changed |= _roots[i] != root || _camps[i] != camp;
                _roots[i] = root;
                _camps[i] = camp;
                float start = float.NaN;
                if (segment != null && progress != null && progress.progressPoints != null)
                    foreach (var point in progress.progressPoints)
                        if (point != null && point.transform != null && point.biome == segment.biome &&
                            (i == 0 || point.transform.position.z > _starts[i - 1]) &&
                            (float.IsNaN(start) || point.transform.position.z < start))
                        {
                            start = point.transform.position.z;
                        }
                // The game checks progress along world Z. Only use that rule when this map supplies
                // a complete, ordered set of runtime points; never guess from nearest campfires.
                changed |= !_starts[i].Equals(start);
                _starts[i] = start;
                if (i < _progressSegmentCount && i <= _current + 1 &&
                    (float.IsNaN(start) || (i > 0 && start <= _starts[i - 1]))) _spatialReady = false;
            }
            if (_current >= _progressSegmentCount) _spatialReady = false;
            changed |= oldSpatialReady != _spatialReady;
            if (changed) Revision++;
        }

        public bool Includes(Transform target, Vector3 position)
        {
            if (target == null) return false;
            if (_map == null) return true; // Airport or a single-scene map without MapHandler.
            if (_current < 0 || _current >= _roots.Length) return false;
            // Moving Items must follow their current location, even if a mod retains a scene parent.
            bool moving = target.GetComponentInParent<Item>() != null || target.GetComponentInParent<Character>() != null;
            if (!moving)
            {
                // Camp roots may sit below another segment root: test all camps first.
                for (int i = 0; i < _camps.Length; i++)
                    if (Under(target, _camps[i])) return i == _current || i == _current - 1;
                for (int i = 0; i < _roots.Length; i++)
                    if (Under(target, _roots[i])) return i == _current;
            }
            if (_current > 0 && _camps[_current - 1] != null && _camps[_current - 1].gameObject.activeInHierarchy &&
                (position - _camps[_current - 1].position).sqrMagnitude <= 40f * 40f) return true;
            // Photon items have no segment parent. Prefer the terrain supporting their live
            // position, including custom/overlapping layouts, before the game's Z progress planes.
            bool onCamp;
            int groundSegment = FindSupportingSegment(target, position, out onCamp);
            if (groundSegment >= 0) return groundSegment == _current || (onCamp && groundSegment == _current - 1);
            if (_spatialReady) return InBand(position.z, _starts, _current, _progressSegmentCount);
            // For moving items, an old parent is not proof of their current segment.
            // Custom layouts may lack ordered progress planes. Limit unassigned targets to
            // the local player's immediate surroundings instead of treating them as whole-map.
            return Character.localCharacter != null &&
                (position - Character.localCharacter.Center).sqrMagnitude <= 40f * 40f;
        }

        internal static bool InBand(float z, float[] starts, int current, int count = -1)
        {
            if (count < 0) count = starts.Length;
            return current >= 0 && current < count && !float.IsNaN(z) &&
                (current == 0 || z >= starts[current]) &&
                (current == count - 1 || z < starts[current + 1]);
        }

        private int FindSupportingSegment(Transform target, Vector3 position, out bool onCamp)
        {
            onCamp = false;
            int segment = -1;
            float closest = float.PositiveInfinity;
            int hits = Physics.RaycastNonAlloc(position + Vector3.up * 0.5f, Vector3.down, _groundHits,
                80f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (int hit = 0; hit < hits; hit++)
            {
                Transform ground = _groundHits[hit].transform;
                if (ground == null || ground == target || ground.IsChildOf(target) ||
                    ground.GetComponentInParent<Item>() != null || ground.GetComponentInParent<Character>() != null ||
                    _groundHits[hit].distance >= closest) continue;
                int found = -1;
                bool camp = false;
                for (int i = 0; i < _camps.Length; i++)
                    if (Under(ground, _camps[i])) { found = i; camp = true; break; }
                if (found < 0)
                    for (int i = 0; i < _roots.Length; i++)
                        if (Under(ground, _roots[i])) { found = i; break; }
                if (found < 0) continue;
                closest = _groundHits[hit].distance;
                segment = found;
                onCamp = camp;
            }
            return segment;
        }

        private static bool Under(Transform target, Transform root)
        {
            return root != null && (target == root || target.IsChildOf(root));
        }
    }
}
