using System;

namespace StateKeeper
{
    internal static class AssistanceEvidence
    {
        internal static float HealedAmount(float before, float after)
        {
            if (float.IsNaN(before) || float.IsInfinity(before) || float.IsNaN(after) || float.IsInfinity(after)) return 0;
            return Math.Max(0, before - after);
        }

        // CharacterAfflictions.shouldPassOut uses 0.99, not the achievement branch's 1.0.
        internal static bool RestoredRecoveryEligibility(bool wasPassedOut, bool dead, float beforeSum, float afterSum, int petrify)
        {
            return wasPassedOut && !dead && petrify < 100 && beforeSum > .99f && afterSum >= 0 && afterSum <= .99f
                && !float.IsInfinity(beforeSum);
        }
    }
}
