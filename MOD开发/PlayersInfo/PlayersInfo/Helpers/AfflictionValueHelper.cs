using UnityEngine;

namespace PlayersInfo.Helpers
{
    internal static class AfflictionValueHelper
    {
        public static float GetValue(Character character, BarAffliction bar)
        {
            if (character == null || character.Equals(null) || character.data == null || bar == null)
                return 0f;

            return GetValue(character, bar.afflictionType, bar.isPetrify);
        }

        public static float GetValue(Character character, CharacterAfflictions.STATUSTYPE afflictionType, bool isPetrify)
        {
            if (character == null || character.Equals(null) || character.data == null)
                return 0f;

            if (isPetrify)
                return Mathf.Clamp01(character.data.petrifyAmount / 100f);

            try
            {
                if (character.refs == null || character.refs.afflictions == null)
                    return 0f;
                return Mathf.Clamp01(character.refs.afflictions.GetCurrentStatus(afflictionType));
            }
            catch
            {
                return 0f;
            }
        }
    }
}
