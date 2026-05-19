using System.Globalization;
using DreamyAscent.Data;
using DreamyAscent.Services;
using UnityEngine;

namespace DreamyAscent.UI
{
    internal sealed partial class DaCustomiserWindow
    {
        private void FitWindowToScreen()
        {
            bool previewFirst = IsPreviewFirstLayout();
            float width = previewFirst ? Mathf.Max(900f, Screen.width - 16f) : Mathf.Max(900f, Screen.width - 16f);
            float height = Mathf.Max(620f, Screen.height - 60f);
            _windowRect.width = width;
            _windowRect.height = height;
            float minX = previewFirst ? 8f : 0f;
            float minY = 24f;
            _windowRect.x = Mathf.Clamp(_windowRect.x, minX, Mathf.Max(minX, Screen.width - _windowRect.width));
            _windowRect.y = Mathf.Clamp(_windowRect.y, minY, Mathf.Max(minY, Screen.height - _windowRect.height));
        }

        private bool IsPreviewFirstLayout()
        {
            return _preview != null && _preview.UseMainCameraView;
        }

        private void FitPreviewFirstRects()
        {
            _topBarRect.x = 8f;
            _topBarRect.y = 30f;
            _topBarRect.width = Mathf.Max(720f, Screen.width - 16f);
            _topBarRect.height = PreviewTopBarHeight;

            float panelY = _topBarRect.y + _topBarRect.height + PreviewPanelGap;
            float panelHeight = Mathf.Max(420f, Screen.height - panelY - 12f);
            float availableWidth = Mathf.Max(640f, Screen.width - 16f);
            float navigationWidth = Mathf.Min(PreviewNavigationWidth, Mathf.Max(260f, availableWidth * 0.34f));
            float inspectorWidth = Mathf.Min(
                PreviewInspectorWidth,
                Mathf.Max(320f, availableWidth - navigationWidth - PreviewPanelGap));
            _navigationRect.x = 8f;
            _navigationRect.y = panelY;
            _navigationRect.width = navigationWidth;
            _navigationRect.height = panelHeight;

            _inspectorRect.x = Mathf.Max(
                _navigationRect.x + _navigationRect.width + PreviewPanelGap,
                Screen.width - inspectorWidth - 8f);
            _inspectorRect.y = panelY;
            _inspectorRect.width = inspectorWidth;
            _inspectorRect.height = panelHeight;
        }

        private void DrawPreviewFirstWindows()
        {
            _topBarRect = GUI.Window(923411, _topBarRect, DrawTopBarWindow, "DreamyAscent");

            DaTerrainData data = DaTerrainExportService.LastExportedTerrain;
            if (data == null || data.Map == null || data.Map.Segments == null || data.Map.Segments.Count == 0)
            {
                _navigationRect = GUI.Window(923412, _navigationRect, DrawNoRuntimeDataWindow, Text("Structure", "ui"));
                return;
            }

            ClampSelection(data);
            _navigationRect = GUI.Window(923412, _navigationRect, DrawNavigationWindow, Text("Structure", "ui"));
            _inspectorRect = GUI.Window(923413, _inspectorRect, DrawInspectorWindow, GetInspectorTitle(data));
        }

        private string GetInspectorTitle(DaTerrainData data)
        {
            DaSegmentData segment = GetSelectedSegment(data);
            string segmentName = Text(segment != null ? segment.SegmentName : string.Empty, "segment");
            string tab = _detailTabIndex == 0 ? Text("ParametersTab", "ui") : Text("Catalog", "ui");
            return tab + " - " + segmentName;
        }

        private void DrawTopBarWindow(int windowId)
        {
            GUILayout.BeginVertical();
            DrawToolbar(DaTerrainExportService.LastExportedTerrain);
            GUILayout.EndVertical();
            GUI.DragWindow(new Rect(0f, 0f, _topBarRect.width, 24f));
        }

        private void DrawNoRuntimeDataWindow(int windowId)
        {
            GUILayout.BeginVertical();
            GUILayout.Label(Text("NoRuntimeData", "ui"));
            GUILayout.EndVertical();
            GUI.DragWindow(new Rect(0f, 0f, _navigationRect.width, 24f));
        }

        private void DrawNavigationWindow(int windowId)
        {
            DaTerrainData data = DaTerrainExportService.LastExportedTerrain;
            GUILayout.BeginVertical();
            DrawTree(data, -1f);
            DrawPlacementSummaryPanel(data);
            GUILayout.EndVertical();
            GUI.DragWindow(new Rect(0f, 0f, _navigationRect.width, 24f));
        }

        private void DrawInspectorWindow(int windowId)
        {
            DaTerrainData data = DaTerrainExportService.LastExportedTerrain;
            GUILayout.BeginVertical();
            DrawDetail(data);
            if (!string.IsNullOrWhiteSpace(_statusText))
            {
                GUILayout.Label(_statusText);
            }

            GUILayout.EndVertical();
            GUI.DragWindow(new Rect(0f, 0f, _inspectorRect.width, 24f));
        }

        private void DrawToolbar(DaTerrainData data)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(Text("CurrentMap", "ui") + ": " + GetShortMapName(data), GUILayout.Width(360f));
            if (GUILayout.Button(Text("Rescan", "ui"), GUILayout.Width(100f)))
            {
                Rescan();
            }

            if (GUILayout.Button(Text("WriteDiagnostics", "ui"), GUILayout.Width(115f)))
            {
                WriteDiagnostics(data);
            }

            if (GUILayout.Button(Text("ExportCurrent", "ui"), GUILayout.Width(100f)))
            {
                ExportCurrent(data);
            }

            if (GUILayout.Button(Text("Close", "ui"), GUILayout.Width(65f)))
            {
                SetVisible(false);
            }

            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label(Text("ImportFile", "ui"), GUILayout.Width(70f));
            _importPath = GUILayout.TextField(_importPath ?? string.Empty, GUILayout.ExpandWidth(true));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Space(70f);
            if (GUILayout.Button(Text("OpenFileDirectory", "ui"), GUILayout.Width(90f)))
            {
                OpenFileDirectory();
            }

            if (GUILayout.Button(Text("UseLatestImport", "ui"), GUILayout.Width(130f)))
            {
                UseLatestImportFile();
            }

            if (GUILayout.Button(Text("ImportApply", "ui"), GUILayout.Width(110f)))
            {
                ImportAndApply(data);
            }

            GUILayout.EndHorizontal();
        }

        private void DrawLeftPanel(DaTerrainData data)
        {
            GUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(490f), GUILayout.ExpandHeight(true));
            DrawTree(data);
            GUILayout.Space(6f);
            DrawDetail(data);
            GUILayout.EndVertical();
        }

        private void DrawPreviewFirstPanels(DaTerrainData data)
        {
            GUILayout.BeginHorizontal();
            DrawNavigationPanel(data);
            GUILayout.Space(PreviewPanelGap);
            DrawInspectorPanel(data);
            GUILayout.EndHorizontal();
        }

        private void DrawNavigationPanel(DaTerrainData data)
        {
            GUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(PreviewNavigationWidth), GUILayout.ExpandHeight(true));
            DrawTree(data);
            DrawPlacementSummaryPanel(data);
            GUILayout.EndVertical();
        }

        private void DrawInspectorPanel(DaTerrainData data)
        {
            GUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(PreviewInspectorWidth), GUILayout.ExpandHeight(true));
            DrawDetail(data);
            GUILayout.EndVertical();
        }

        private void DrawPlacementSummaryPanel(DaTerrainData data)
        {
            DaSegmentData segment = GetSelectedSegment(data);
            if (segment == null)
            {
                return;
            }

            EnsurePlacementLists(segment);
            GUILayout.Space(8f);
            GUILayout.BeginVertical(GUI.skin.box, GUILayout.ExpandHeight(true));
            DrawParameterHintsPanel(data);
            GUILayout.Space(8f);
            DrawSampleAssetsPanel();
            GUILayout.Space(8f);
            GUILayout.Label(Text("PlacementConfig", "ui"));
            GUILayout.Label(Text("Segment", "ui") + ": " + Text(segment.SegmentName ?? string.Empty, "segment"));
            GUILayout.Label(Text("SegmentEditMode", "ui") + ": " + Text(segment.EditMode.ToString(), "ui"));
            GUILayout.Label(
                Text("PlacementRegions", "ui") + ": " + segment.SubAreas.Count + "    " +
                Text("PlacementRules", "ui") + ": " + segment.PlacementRules.Count);

            if (segment.PlacementRules.Count > 0)
            {
                int enabledCount = 0;
                for (int index = 0; index < segment.PlacementRules.Count; index++)
                {
                    DaPlacementRuleData rule = segment.PlacementRules[index];
                    if (rule != null && rule.Enabled)
                    {
                        enabledCount++;
                    }
                }

                GUILayout.Label(Text("Enabled", "ui") + ": " + enabledCount);
            }

            if (GUILayout.Button(Text("OpenPlacementConfig", "ui"), GUILayout.Height(24f)))
            {
                _detailTabIndex = 1;
                _showPlacementConfig = true;
            }

            GUILayout.Label(Text("PlacementSummaryHint", "ui"));
            GUILayout.EndVertical();
        }

        private void DrawParameterHintsPanel(DaTerrainData data)
        {
            GUILayout.Label(Text("ParameterHints", "ui"));
            string nameText = Text("NoFocusedProperty", "ui");
            string currentValue = string.Empty;
            string hintText = Text("NoKnownParameterHints", "ui");

            if (!string.IsNullOrWhiteSpace(_focusedPlacementFieldName))
            {
                nameText = Text(_focusedPlacementFieldName, "ui") + " (" + _focusedPlacementFieldName + ")";
                currentValue = _focusedPlacementFieldValue ?? string.Empty;
                string placementHint = GetPlacementFieldHint(_focusedPlacementFieldName);
                hintText = string.IsNullOrWhiteSpace(placementHint) ? Text("NoKnownParameterHints", "ui") : placementHint;
            }
            else if (!string.IsNullOrWhiteSpace(_focusedPropertyName))
            {
                nameText = Text(_focusedPropertyName, "property") + " (" + _focusedPropertyName + ")";
                currentValue = _focusedPropertyValue ?? string.Empty;
                string hint = GetPropertyHint(_focusedPropertyName);
                hintText = string.IsNullOrWhiteSpace(hint) ? Text("NoKnownParameterHints", "ui") : hint;
            }

            GUILayout.Label(nameText);
            GUILayout.Label(Text("CurrentValue", "ui") + ": " + currentValue);
            GUILayout.Label(hintText);
        }

        private void DrawSampleAssetsPanel()
        {
            GUILayout.Label(Text("SampleAssets", "ui"));
            GUILayout.Label(Text("SampleAssetsManualLoadHint", "ui"));
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(Text(_sampleAssetsLoaded ? "RefreshSampleAssets" : "LoadSampleAssets", "ui"), GUILayout.Height(24f)))
            {
                _sampleAssetsLoaded = true;
                DaTemplateSnapshotService.Invalidate();
                DaObjectRegistryService.Invalidate();
                DaTemplateBaselineService.Invalidate();
                _cachedSnapshotMatch = null;
                _cachedSnapshotMatchMapKey = string.Empty;
            }

            if (_sampleAssetsLoaded && GUILayout.Button(Text("UnloadSampleAssets", "ui"), GUILayout.Height(24f)))
            {
                _sampleAssetsLoaded = false;
                DaTemplateSnapshotService.Invalidate();
                DaObjectRegistryService.Invalidate();
                DaTemplateBaselineService.Invalidate();
                _cachedSnapshotMatch = null;
                _cachedSnapshotMatchMapKey = string.Empty;
            }

            GUILayout.EndHorizontal();
            if (!_sampleAssetsLoaded)
            {
                GUILayout.Label(Text("SampleAssetsNotLoaded", "ui"));
                return;
            }

            DaObjectRegistry registry = DaObjectRegistryService.GetRegistry();
            bool hasRegistry = registry != null && registry.Summary != null;
            GUILayout.Label(hasRegistry ? Text("SampleRegistryLoaded", "ui") : Text("SampleRegistryMissing", "ui"));
            GUILayout.Label(
                Text("RegistryTemplates", "ui") + ": " + (hasRegistry ? registry.Summary.TemplateCount : 0) + "    " +
                Text("RegistryRecommended", "ui") + ": " + (hasRegistry ? registry.Summary.RecommendedFirstPassCandidateCount : 0));

            DaTemplateSnapshotMatch currentMatch = GetCachedSnapshotMatch(GetSelectedSegment(DaTerrainExportService.LastExportedTerrain));
            GUILayout.Space(4f);
            GUILayout.Label(Text("TemplateSnapshotMatch", "ui"));
            GUILayout.Label(Text("SnapshotMatchStatus", "ui") + ": " + Text(currentMatch != null ? currentMatch.Status ?? string.Empty : "Unknown", "ui"));
            GUILayout.Label(
                Text("SnapshotMatchScore", "ui") + ": " + (currentMatch != null ? currentMatch.Score.ToString("0.###", CultureInfo.InvariantCulture) : "0") +
                "    " + Text("SnapshotCandidates", "ui") + ": " + (currentMatch != null ? currentMatch.CandidateCount : 0));
            GUILayout.Label(
                Text("SnapshotMatchDetail", "ui") + ": " +
                (currentMatch != null && !string.IsNullOrWhiteSpace(currentMatch.SnapshotId) ? currentMatch.SnapshotId : Text("Unknown", "ui")) +
                " / " +
                (currentMatch != null && !string.IsNullOrWhiteSpace(currentMatch.SnapshotVariantName) ? Text(currentMatch.SnapshotVariantName, "segment") : Text("Unknown", "ui")));

            DaTemplateBaselineData baseline = DaTemplateBaselineService.GetBaseline(GetSelectedSegment(DaTerrainExportService.LastExportedTerrain));
            GUILayout.Space(4f);
            GUILayout.Label(Text("TemplateBaseline", "ui") + ": " + Text(baseline != null ? baseline.Status ?? string.Empty : "Unknown", "ui"));
            GUILayout.Label(
                Text("CurrentVariantBaseline", "ui") + ": " +
                Text(DaTemplateBaselineService.HasCurrentVariantDefaultTemplate(GetSelectedSegment(DaTerrainExportService.LastExportedTerrain)) ? "EnableValue" : "DisableValue", "ui") +
                "    " + Text("TemplateVariant", "ui") + ": " +
                (baseline != null && !string.IsNullOrWhiteSpace(baseline.NormalizedVariantName) ? Text(baseline.NormalizedVariantName, "segment") : Text("Unknown", "ui")));
            GUILayout.Label(
                Text("BaselineMatched", "ui") + ": " +
                (baseline != null ? baseline.MatchedGrouperCount : 0) + "/" + (baseline != null ? baseline.SnapshotGrouperCount : 0) +
                "  " + (baseline != null ? baseline.MatchedStepCount : 0) + "/" + (baseline != null ? baseline.SnapshotStepCount : 0));
            GUILayout.Label(
                Text("BaselineWarnings", "ui") + ": " +
                (baseline != null ? baseline.MissingGrouperCount + baseline.MissingStepCount + baseline.ExtraRuntimeGrouperCount + baseline.ExtraRuntimeStepCount : 0));

            GUILayout.Label(Text("SampleDevAssetsHint", "ui"));
        }

        private void DrawMainCameraPreviewOverlay()
        {
            if (_preview == null || !_preview.UseMainCameraView)
            {
                return;
            }

            DaTerrainData data = DaTerrainExportService.LastExportedTerrain;
            if (data == null || data.Map == null || data.Map.Segments == null || data.Map.Segments.Count == 0)
            {
                return;
            }

            DaSegmentData segment = GetSelectedSegment(data);
            _preview.SetTarget(segment);

            Rect previewRect = new Rect(0f, 0f, Screen.width, Screen.height);
            _preview.DrawScreenOverlay(previewRect, GetPreviewBlockedInputRects());
        }

        private Rect[] GetPreviewBlockedInputRects()
        {
            return new[] { _topBarRect, _navigationRect, _inspectorRect };
        }
    }
}
