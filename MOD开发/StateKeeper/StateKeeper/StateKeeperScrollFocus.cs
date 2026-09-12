using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace StateKeeper
{
    internal sealed class StateKeeperScrollFocus : MonoBehaviour, ISelectHandler
    {
        internal ScrollRect Scroll;
        private readonly Vector3[] _corners = new Vector3[4];

        public void OnSelect(BaseEventData eventData)
        {
            if (Scroll == null || Scroll.content == null || Scroll.viewport == null) return;
            Canvas.ForceUpdateCanvases();
            var target = transform as RectTransform;
            if (target == null) return;
            target.GetWorldCorners(_corners);
            float bottom = Scroll.viewport.InverseTransformPoint(_corners[0]).y;
            float top = Scroll.viewport.InverseTransformPoint(_corners[1]).y;
            Rect view = Scroll.viewport.rect;
            float delta = top > view.yMax ? view.yMax - top : bottom < view.yMin ? view.yMin - bottom : 0;
            Vector2 position = Scroll.content.anchoredPosition;
            position.y = Mathf.Clamp(position.y + delta, 0, Mathf.Max(0, Scroll.content.rect.height - view.height));
            Scroll.StopMovement(); Scroll.content.anchoredPosition = position;
        }
    }
}
