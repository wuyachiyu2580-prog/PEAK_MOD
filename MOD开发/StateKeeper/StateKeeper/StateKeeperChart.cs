using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System;
using UnityEngine.EventSystems;

namespace StateKeeper
{
    internal sealed class StateKeeperChart : MaskableGraphic, IPointerClickHandler
    {
        internal sealed class Series
        {
            internal Color color;
            internal List<Vector2> points = new List<Vector2>();
        }
        private readonly List<Series> _series = new List<Series>();
        private readonly List<float> _ticks = new List<float>();
        private const float LineWidth = 3f;
        internal Action<float> OnSelected;
        public void OnPointerClick(PointerEventData e)
        {
            Vector2 position;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, e.position, e.pressEventCamera, out position))
                OnSelected?.Invoke(Mathf.Clamp01((position.x - rectTransform.rect.xMin) / Mathf.Max(1, rectTransform.rect.width)));
        }

        internal void SetSeries(IEnumerable<Series> series, IEnumerable<float> ticks = null)
        {
            _series.Clear(); if (series != null) _series.AddRange(series);
            _ticks.Clear(); if (ticks != null) _ticks.AddRange(ticks);
            raycastTarget = true; SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect rect = rectTransform.rect;
            for (int i = 0; i <= 4; i++)
            {
                float y = rect.yMin + i * rect.height / 4;
                Quad(vh, new Vector2(rect.xMin, y), new Vector2(rect.xMax, y + 1), new Color(1, 1, 1, .12f));
            }
            foreach (float tick in _ticks)
            { float x = rect.xMin + Mathf.Clamp01(tick) * rect.width; Quad(vh, new Vector2(x, rect.yMin), new Vector2(x + 1, rect.yMin + 8), new Color(1, 1, 1, .5f)); }
            foreach (Series series in _series) DrawLine(vh, rect, series.points, series.color);
        }

        private void DrawLine(VertexHelper vh, Rect rect, List<Vector2> points, Color tint)
        {
            for (int i = 1; i < points.Count; i++)
            {
                if (vh.currentVertCount > 63000) break;
                if (float.IsNaN(points[i - 1].y) || float.IsNaN(points[i].y)) continue;
                Vector2 a = new Vector2(rect.xMin + Mathf.Clamp01(points[i - 1].x) * rect.width, rect.yMin + Mathf.Clamp01(points[i - 1].y) * rect.height);
                Vector2 b = new Vector2(rect.xMin + Mathf.Clamp01(points[i].x) * rect.width, rect.yMin + Mathf.Clamp01(points[i].y) * rect.height);
                Vector2 direction = (b - a).normalized;
                Vector2 normal = new Vector2(-direction.y, direction.x) * LineWidth * 0.5f;
                int start = vh.currentVertCount;
                vh.AddVert(a - normal, tint, Vector2.zero);
                vh.AddVert(a + normal, tint, Vector2.zero);
                vh.AddVert(b + normal, tint, Vector2.zero);
                vh.AddVert(b - normal, tint, Vector2.zero);
                vh.AddTriangle(start, start + 1, start + 2);
                vh.AddTriangle(start, start + 2, start + 3);
            }
        }

        private static void Quad(VertexHelper vh, Vector2 min, Vector2 max, Color tint)
        {
            int i = vh.currentVertCount;
            if (i > 63000) return;
            vh.AddVert(min, tint, Vector2.zero); vh.AddVert(new Vector2(min.x, max.y), tint, Vector2.zero);
            vh.AddVert(max, tint, Vector2.zero); vh.AddVert(new Vector2(max.x, min.y), tint, Vector2.zero);
            vh.AddTriangle(i, i + 1, i + 2); vh.AddTriangle(i, i + 2, i + 3);
        }
    }
}
