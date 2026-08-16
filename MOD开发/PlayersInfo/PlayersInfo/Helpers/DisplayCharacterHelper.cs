using UnityEngine;

namespace PlayersInfo.Helpers
{
    internal static class DisplayCharacterHelper
    {
        public static Character GetObservedOrLocal()
        {
            try
            {
                var observed = Character.observedCharacter;
                if (observed != null && !observed.Equals(null)) return observed;
            }
            catch { }

            try
            {
                var local = Character.localCharacter;
                if (local != null && !local.Equals(null)) return local;
            }
            catch { }
            return null;
        }

        public static bool IsLocalDisplay(Character character)
        {
            var local = Character.localCharacter;
            return character != null && !character.Equals(null)
                && local != null && !local.Equals(null)
                && character == local;
        }
    }
}
