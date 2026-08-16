using UnityEngine;

namespace PlayersInfo.MonoBehaviours
{
    /// <summary>
    /// Owns one teammate affliction visual. The vanilla BarAffliction reads the
    /// global observed character, so teammate bars use this target-local driver.
    /// </summary>
    internal sealed class TeammateBarAffliction : MonoBehaviour
    {
        public CharacterAfflictions.STATUSTYPE afflictionType;
        public bool isPetrify;
        public RectTransform rtf;
        public float size;

        public float width
        {
            get { return rtf != null ? rtf.sizeDelta.x : 0f; }
            set
            {
                if (rtf == null) return;
                var delta = rtf.sizeDelta;
                if (Mathf.Abs(delta.x - value) < 0.01f) return;
                delta.x = value;
                rtf.sizeDelta = delta;
            }
        }

        public void Initialize(BarAffliction source)
        {
            if (source == null) return;
            afflictionType = source.afflictionType;
            isPetrify = source.isPetrify;
            rtf = source.rtf != null ? source.rtf : GetComponent<RectTransform>();
            size = 0f;
            width = 0f;

            if (source.icon != null)
                source.icon.transform.localScale = Vector3.one;
        }

        public void ResetVisual()
        {
            size = 0f;
            width = 0f;
            if (gameObject.activeSelf) gameObject.SetActive(false);
        }

        public void UpdateVisual(Character target, float fullWidth, float minWidth)
        {
            if (target == null || target.Equals(null) || target.data == null)
            {
                ResetVisual();
                return;
            }

            float current = isPetrify
                ? Mathf.Clamp01(target.data.petrifyAmount / 100f)
                : GetStatus(target);

            size = Mathf.Max(0f, fullWidth * current);
            if (current <= 0.01f)
            {
                if (gameObject.activeSelf) gameObject.SetActive(false);
                width = 0f;
                return;
            }

            size = Mathf.Max(size, minWidth);
            if (!gameObject.activeSelf) gameObject.SetActive(true);
            width = Mathf.Lerp(width, size, Mathf.Clamp01(Time.deltaTime * 10f));
        }

        public void SyncVisualImmediate(Character target, float fullWidth, float minWidth)
        {
            if (target == null || target.Equals(null) || target.data == null)
            {
                ResetVisual();
                return;
            }

            float current = isPetrify
                ? Mathf.Clamp01(target.data.petrifyAmount / 100f)
                : GetStatus(target);
            if (current <= 0.01f)
            {
                ResetVisual();
                return;
            }

            size = Mathf.Max(fullWidth * current, minWidth);
            width = size;
            if (!gameObject.activeSelf) gameObject.SetActive(true);
        }

        private float GetStatus(Character target)
        {
            try
            {
                if (target.refs == null || target.refs.afflictions == null)
                    return 0f;
                return Mathf.Clamp01(target.refs.afflictions.GetCurrentStatus(afflictionType));
            }
            catch
            {
                return 0f;
            }
        }
    }
}
