using System.Collections.Generic;
using DreamyAscent.Data;
using DreamyAscent.Helpers;
using DreamyAscent.Services;
using UnityEngine;

namespace DreamyAscent.UI
{
    internal sealed class DaSceneHighlighter : MonoBehaviour
    {
        private readonly List<LineRenderer> _lines = new List<LineRenderer>();
        private readonly List<MeshRenderer> _fills = new List<MeshRenderer>();
        private Material _material;
        private Material _fillMaterial;
        private float _nextRefreshTime;
        private int _lastSelectedId;

        public static DaLevelGenStepData SelectedStep { get; set; }
        public static GameObject SelectedObject { get; set; }

        private void Awake()
        {
            Shader shader = Shader.Find("Hidden/Internal-Colored");
            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }

            _material = new Material(shader);
            _material.hideFlags = HideFlags.HideAndDontSave;
            _material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            _material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            _material.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
            _material.SetInt("_ZWrite", 0);
            _material.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
            _material.renderQueue = 5000;

            _fillMaterial = new Material(_material);
            _fillMaterial.hideFlags = HideFlags.HideAndDontSave;
            _fillMaterial.renderQueue = 4999;
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextRefreshTime)
            {
                return;
            }

            _nextRefreshTime = Time.unscaledTime + 0.2f;
            Refresh();
        }

        private void OnDestroy()
        {
            for (int index = 0; index < _lines.Count; index++)
            {
                if (_lines[index] != null)
                {
                    Destroy(_lines[index].gameObject);
                }
            }

            if (_material != null)
            {
                Destroy(_material);
            }

            for (int index = 0; index < _fills.Count; index++)
            {
                if (_fills[index] != null)
                {
                    Destroy(_fills[index].gameObject);
                }
            }

            if (_fillMaterial != null)
            {
                Destroy(_fillMaterial);
            }
        }

        private void Refresh()
        {
            int used = 0;
            int usedFills = 0;

            if (SelectedStep != null && SelectedStep.SourceObject != null)
            {
                LogSelectedStepIfChanged(SelectedStep);
                DrawStep(SelectedStep, new Color(0f, 0.95f, 1f, 1f), 0.36f, true, ref used, ref usedFills);
            }
            else if (SelectedObject != null)
            {
                LogSelectedObjectIfChanged(SelectedObject);
                DrawObjectBounds(SelectedObject, new Color(0f, 1f, 0.45f, 1f), 0.32f, ref used);
            }
            else
            {
                _lastSelectedId = 0;
            }

            DaTerrainData data = DaTerrainExportService.LastExportedTerrain;
            if (data != null && data.Map != null && data.Map.Segments != null)
            {
                for (int segmentIndex = 0; segmentIndex < data.Map.Segments.Count; segmentIndex++)
                {
                    DaSegmentData segment = data.Map.Segments[segmentIndex];
                    if (segment == null || segment.Groupers == null)
                    {
                        continue;
                    }

                    for (int grouperIndex = 0; grouperIndex < segment.Groupers.Count; grouperIndex++)
                    {
                        DaPropGrouperData grouper = segment.Groupers[grouperIndex];
                        if (grouper == null || grouper.Steps == null)
                        {
                            continue;
                        }

                        for (int stepIndex = 0; stepIndex < grouper.Steps.Count; stepIndex++)
                        {
                            DaLevelGenStepData step = grouper.Steps[stepIndex];
                            if (step != null && step != SelectedStep && HasChanges(step))
                            {
                                DrawStep(step, new Color(1f, 0.45f, 0f, 0.9f), 0.18f, false, ref used, ref usedFills);
                            }
                        }
                    }
                }
            }

            for (int index = used; index < _lines.Count; index++)
            {
                if (_lines[index] != null)
                {
                    _lines[index].gameObject.SetActive(false);
                }
            }

            for (int index = usedFills; index < _fills.Count; index++)
            {
                if (_fills[index] != null)
                {
                    _fills[index].gameObject.SetActive(false);
                }
            }
        }

        private void DrawStep(DaLevelGenStepData step, Color color, float width, bool drawCenterMarker, ref int used, ref int usedFills)
        {
            Transform transform = step.SourceObject != null ? step.SourceObject.transform : null;
            if (transform == null)
            {
                return;
            }

            Vector2 area = GetAreaProperty(step, new Vector2(6f, 6f));
            float height = GetFloatProperty(step, "height", 0f);
            float radius = Mathf.Max(GetFloatProperty(step, "radius", 0f), GetFloatProperty(step, "circleSize", 0f));
            Vector3 center = transform.position + Vector3.up * (height + 0.35f);
            Quaternion yawRotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);

            if (radius > 0f)
            {
                DrawCircle(center, radius, color, width, ref used);
                if (drawCenterMarker)
                {
                    DrawSolidRectangle(center, yawRotation, new Vector2(radius * 0.18f, radius * 0.18f), WithAlpha(color, 0.18f), ref usedFills);
                    DrawCenterMarker(center, Mathf.Max(radius * 0.08f, 4f), color, width, ref used);
                }

                return;
            }

            area = new Vector2(Mathf.Max(area.x, 1f), Mathf.Max(area.y, 1f));
            if (drawCenterMarker)
            {
                Vector2 fillSize = new Vector2(Mathf.Min(area.x, 36f), Mathf.Min(area.y, 36f));
                DrawSolidRectangle(center, yawRotation, fillSize, WithAlpha(color, 0.16f), ref usedFills);
            }

            DrawFootprint(center, yawRotation, area, color, width, ref used);
            if (drawCenterMarker)
            {
                DrawCenterMarker(center, Mathf.Clamp(Mathf.Min(area.x, area.y) * 0.06f, 4f, 14f), color, width, ref used);
            }
        }

        private void LogSelectedStepIfChanged(DaLevelGenStepData step)
        {
            int id = step.SourceObject.GetInstanceID();
            if (_lastSelectedId == id)
            {
                return;
            }

            _lastSelectedId = id;
            Transform transform = step.SourceObject.transform;
            DaLog.Info("Highlight selected step: " + step.StepName + " type=" + step.StepType + " pos=" + transform.position);
        }

        private void LogSelectedObjectIfChanged(GameObject gameObject)
        {
            int id = gameObject.GetInstanceID();
            if (_lastSelectedId == id)
            {
                return;
            }

            _lastSelectedId = id;
            DaLog.Info("Highlight selected object: " + gameObject.name + " pos=" + gameObject.transform.position);
        }

        private void DrawObjectBounds(GameObject gameObject, Color color, float width, ref int used)
        {
            Bounds bounds;
            if (!TryGetObjectBounds(gameObject, out bounds))
            {
                Vector3 center = gameObject.transform.position + Vector3.up * 0.6f;
                DrawCenterMarker(center, 2.5f, color, width, ref used);
                return;
            }

            DrawBounds(bounds, color, width, ref used);
            DrawCenterMarker(bounds.center, Mathf.Clamp(bounds.extents.magnitude * 0.1f, 1f, 4f), color, width, ref used);
        }

        private static bool TryGetObjectBounds(GameObject gameObject, out Bounds bounds)
        {
            bounds = new Bounds(Vector3.zero, Vector3.zero);
            bool hasBounds = false;
            Renderer[] renderers = gameObject != null ? gameObject.GetComponentsInChildren<Renderer>(true) : null;
            for (int index = 0; renderers != null && index < renderers.Length; index++)
            {
                Renderer renderer = renderers[index];
                if (renderer == null)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            return hasBounds && bounds.size != Vector3.zero;
        }

        private void DrawBounds(Bounds bounds, Color color, float width, ref int used)
        {
            Vector3 min = bounds.min;
            Vector3 max = bounds.max;
            Vector3 p000 = new Vector3(min.x, min.y, min.z);
            Vector3 p001 = new Vector3(min.x, min.y, max.z);
            Vector3 p010 = new Vector3(min.x, max.y, min.z);
            Vector3 p011 = new Vector3(min.x, max.y, max.z);
            Vector3 p100 = new Vector3(max.x, min.y, min.z);
            Vector3 p101 = new Vector3(max.x, min.y, max.z);
            Vector3 p110 = new Vector3(max.x, max.y, min.z);
            Vector3 p111 = new Vector3(max.x, max.y, max.z);

            AddLine(new[] { p000, p001, p101, p100, p000 }, color, width, ref used);
            AddLine(new[] { p010, p011, p111, p110, p010 }, color, width, ref used);
            AddLine(new[] { p000, p010 }, color, width, ref used);
            AddLine(new[] { p001, p011 }, color, width, ref used);
            AddLine(new[] { p100, p110 }, color, width, ref used);
            AddLine(new[] { p101, p111 }, color, width, ref used);
        }

        private void DrawFootprint(Vector3 center, Quaternion rotation, Vector2 size, Color color, float width, ref int used)
        {
            Vector2 ext = size * 0.5f;
            Vector3[] corners =
            {
                new Vector3(-ext.x, 0f, -ext.y),
                new Vector3(ext.x, 0f, -ext.y),
                new Vector3(ext.x, 0f, ext.y),
                new Vector3(-ext.x, 0f, ext.y)
            };

            for (int index = 0; index < corners.Length; index++)
            {
                corners[index] = center + rotation * corners[index];
            }

            float shortSide = Mathf.Min(size.x, size.y);
            float bracket = Mathf.Clamp(shortSide * 0.12f, 0.6f, 12f);
            bracket = Mathf.Min(bracket, shortSide * 0.35f);
            DrawCornerBracket(corners[0], (corners[1] - corners[0]).normalized, (corners[3] - corners[0]).normalized, bracket, color, width, ref used);
            DrawCornerBracket(corners[1], (corners[0] - corners[1]).normalized, (corners[2] - corners[1]).normalized, bracket, color, width, ref used);
            DrawCornerBracket(corners[2], (corners[3] - corners[2]).normalized, (corners[1] - corners[2]).normalized, bracket, color, width, ref used);
            DrawCornerBracket(corners[3], (corners[2] - corners[3]).normalized, (corners[0] - corners[3]).normalized, bracket, color, width, ref used);
        }

        private void DrawSolidRectangle(Vector3 center, Quaternion rotation, Vector2 size, Color color, ref int used)
        {
            Vector2 ext = size * 0.5f;
            Vector3[] vertices =
            {
                center + rotation * new Vector3(-ext.x, 0f, -ext.y),
                center + rotation * new Vector3(ext.x, 0f, -ext.y),
                center + rotation * new Vector3(ext.x, 0f, ext.y),
                center + rotation * new Vector3(-ext.x, 0f, ext.y)
            };

            MeshRenderer renderer = GetFill(used++);
            MeshFilter filter = renderer.GetComponent<MeshFilter>();
            Mesh mesh = filter.sharedMesh;
            mesh.Clear();
            mesh.vertices = vertices;
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.RecalculateBounds();
            renderer.material.color = color;
            renderer.gameObject.SetActive(true);
        }

        private void DrawCornerBracket(Vector3 corner, Vector3 firstDir, Vector3 secondDir, float length, Color color, float width, ref int used)
        {
            AddLine(new[] { corner, corner + firstDir * length }, color, width, ref used);
            AddLine(new[] { corner, corner + secondDir * length }, color, width, ref used);
        }

        private void DrawCircle(Vector3 center, float radius, Color color, float width, ref int used)
        {
            Vector3[] points = new Vector3[49];
            for (int index = 0; index < points.Length; index++)
            {
                float radians = Mathf.PI * 2f * index / (points.Length - 1);
                points[index] = center + new Vector3(Mathf.Cos(radians) * radius, 0f, Mathf.Sin(radians) * radius);
            }

            AddLine(points, color, width, ref used);
        }

        private void DrawCenterMarker(Vector3 center, float size, Color color, float width, ref int used)
        {
            AddLine(new[] { center + Vector3.left * size, center + Vector3.right * size }, color, width, ref used);
            AddLine(new[] { center + Vector3.back * size, center + Vector3.forward * size }, color, width, ref used);
            AddLine(new[] { center, center + Vector3.up * Mathf.Max(size * 0.7f, 2f) }, color, width, ref used);
        }

        private void AddLine(Vector3[] points, Color color, float width, ref int used)
        {
            LineRenderer line = GetLine(used++);
            line.positionCount = points.Length;
            line.SetPositions(points);
            line.startColor = color;
            line.endColor = color;
            line.startWidth = width;
            line.endWidth = width;
            line.gameObject.SetActive(true);
        }

        private LineRenderer GetLine(int index)
        {
            while (_lines.Count <= index)
            {
                GameObject lineObject = new GameObject("DreamyAscent_Highlight");
                lineObject.hideFlags = HideFlags.HideAndDontSave;
                lineObject.transform.SetParent(transform, false);
                LineRenderer renderer = lineObject.AddComponent<LineRenderer>();
                renderer.material = _material;
                renderer.useWorldSpace = true;
                renderer.loop = false;
                renderer.numCapVertices = 4;
                renderer.numCornerVertices = 4;
                renderer.alignment = LineAlignment.View;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                _lines.Add(renderer);
            }

            return _lines[index];
        }

        private MeshRenderer GetFill(int index)
        {
            while (_fills.Count <= index)
            {
                GameObject fillObject = new GameObject("DreamyAscent_HighlightFill");
                fillObject.hideFlags = HideFlags.HideAndDontSave;
                fillObject.transform.SetParent(transform, false);
                MeshFilter filter = fillObject.AddComponent<MeshFilter>();
                Mesh mesh = new Mesh();
                mesh.hideFlags = HideFlags.HideAndDontSave;
                filter.sharedMesh = mesh;
                MeshRenderer renderer = fillObject.AddComponent<MeshRenderer>();
                renderer.material = _fillMaterial;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                _fills.Add(renderer);
            }

            return _fills[index];
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            return new Color(color.r, color.g, color.b, alpha);
        }

        private static bool HasChanges(DaLevelGenStepData step)
        {
            if (step == null)
            {
                return false;
            }

            return HasChangedProperties(step.Properties) ||
                   HasChangedConstraints(step.Modifiers) ||
                   HasChangedConstraints(step.Constraints) ||
                   HasChangedConstraints(step.PostConstraints);
        }

        private static bool HasChangedProperties(List<DaPropertyData> properties)
        {
            if (properties == null)
            {
                return false;
            }

            for (int index = 0; index < properties.Count; index++)
            {
                if (DaRuntimeEditService.IsChanged(properties[index]))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasChangedConstraints(List<DaConstraintData> constraints)
        {
            if (constraints == null)
            {
                return false;
            }

            for (int index = 0; index < constraints.Count; index++)
            {
                if (constraints[index] != null && HasChangedProperties(constraints[index].Properties))
                {
                    return true;
                }
            }

            return false;
        }

        private static Vector2 GetAreaProperty(DaLevelGenStepData step, Vector2 fallback)
        {
            DaPropertyData property = FindProperty(step, "area");
            if (property == null)
            {
                return fallback;
            }

            if (property.Value is Vector3 v3)
            {
                return new Vector2(v3.x, v3.z);
            }

            if (property.Value is Vector2 v2)
            {
                return v2;
            }

            return fallback;
        }

        private static float GetFloatProperty(DaLevelGenStepData step, string name, float fallback)
        {
            DaPropertyData property = FindProperty(step, name);
            if (property == null || property.Value == null)
            {
                return fallback;
            }

            if (property.Value is float f)
            {
                return f;
            }

            if (property.Value is int i)
            {
                return i;
            }

            return fallback;
        }

        private static DaPropertyData FindProperty(DaLevelGenStepData step, string name)
        {
            if (step == null || step.Properties == null)
            {
                return null;
            }

            for (int index = 0; index < step.Properties.Count; index++)
            {
                DaPropertyData property = step.Properties[index];
                if (property != null && property.Name == name)
                {
                    return property;
                }
            }

            return null;
        }
    }
}


