using UnityEngine;

namespace PlayersInfo.Helpers
{
    internal static class AfflictionValueHelper
    {
        public static float GetValue(Character character, BarAffliction bar)
        {
            if (character == null || character.Equals(null) || character.data == null || bar == null)
                return 0f;

            if (bar.isPetrify)
                return Mathf.Clamp01(character.data.petrifyAmount / 100f);

            try
            {
                if (character.refs == null || character.refs.afflictions == null)
                    return 0f;
                return Mathf.Clamp01(character.refs.afflictions.GetCurrentStatus(bar.afflictionType));
            }
            catch
            {
                return 0f;
            }
        }
    }
}
