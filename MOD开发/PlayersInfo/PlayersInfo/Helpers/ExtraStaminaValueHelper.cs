using UnityEngine;

namespace PlayersInfo.Helpers
{
    internal static class ExtraStaminaValueHelper
    {
        public static float GetCap01(Character character)
        {
            if (character == null || character.Equals(null) || character.data == null)
                return 1f;

            return Mathf.Clamp01(1f - character.data.petrifyAmount * 0.01f);
        }
    }
}
