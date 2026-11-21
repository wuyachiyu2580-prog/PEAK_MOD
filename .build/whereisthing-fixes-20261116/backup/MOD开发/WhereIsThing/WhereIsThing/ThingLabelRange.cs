using UnityEngine;

namespace WhereIsThing
{
    internal static class ThingLabelRange
    {
        internal static bool Within(Vector3 position, Vector3 cameraPosition, float maxDistance, float margin = 0f)
        {
            if (maxDistance <= 0f) return true;
            float distance = maxDistance + margin;
            return (position - cameraPosition).sqrMagnitude <= distance * distance;
        }
    }
}
