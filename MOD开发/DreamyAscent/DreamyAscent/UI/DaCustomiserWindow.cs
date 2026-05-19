using System;
using System.IO;
using System.Globalization;
using DreamyAscent.Data;
using DreamyAscent.Helpers;
using DreamyAscent.Services;
using UnityEngine;

namespace DreamyAscent.UI
{
    internal sealed partial class DaCustomiserWindow : MonoBehaviour
    {
        private Rect _windowRect = new Rect(8f, 30f, 1380f, 820f);
        private Vector2 _treeScroll;
        private Vector2 _detailScroll;
        private int _selectedSegmentIndex;
        private int _selectedGrouperIndex;
        private int _selectedStepIndex;
        private int _selectedSpecialObjectIndex = -1;
        private int _detailTabIndex;
        private string _focusedPropertyName;
        private string _focusedPropertyValue;
        private string _focusedPlacementFieldName;
        private string _focusedPlacementFieldValue;
        private string _importPath = string.Empty;
        private string _statusText = string.Empty;
        private DaMapPreview _preview;
        private Vector2 _segmentRowScroll;
        private Vector2 _grouperRowScroll;
        private bool _visible;
        private CursorLockMode _savedCursorLockState;
        private bool _savedCursorVisible;
        private bool _cursorStateSaved;
        private System.Collections.Generic.Dictionary<UnityEngine.Object, bool> _hiddenCanvasStates;
        private bool _showPlacementConfig = true;
        private bool _showRegistryCandidates;
        private bool _showParentChildRegistry;
        private bool _showRuntimeCatalogItems;
        private bool _showRuntimeCatalogMaterials;
        private bool _sampleAssetsLoaded;
        private string _cachedCatalogKey = string.Empty;
        private System.Collections.Generic.Dictionary<string, DaCatalogItem> _cachedCatalogItemsById = new System.Collections.Generic.Dictionary<string, DaCatalogItem>(StringComparer.Ordinal);
        private System.Collections.Generic.Dictionary<string, DaCatalogMaterial> _cachedCatalogMaterialsById = new System.Collections.Generic.Dictionary<string, DaCatalogMaterial>(StringComparer.Ordinal);
        private System.Collections.Generic.Dictionary<DaLevelGenStepData, CatalogStepSummary> _cachedStepSummaries = new System.Collections.Generic.Dictionary<DaLevelGenStepData, CatalogStepSummary>();
        private System.Collections.Generic.Dictionary<DaLevelGenStepData, int> _cachedRuntimeStepChildCounts = new System.Collections.Generic.Dictionary<DaLevelGenStepData, int>();
        private System.Collections.Generic.Dictionary<DaPropGrouperData, int> _cachedRuntimeGrouperChildCounts = new System.Collections.Generic.Dictionary<DaPropGrouperData, int>();
        private string _cachedSnapshotMatchMapKey = string.Empty;
        private string _cachedSnapshotMatchSegmentName = string.Empty;
        private string _cachedSnapshotMatchVariantName = string.Empty;
        private int _cachedSnapshotMatchGrouperCount = -1;
        private int _cachedSnapshotMatchStepCount = -1;
        private DaTemplateSnapshotMatch _cachedSnapshotMatch;
        private System.Collections.Generic.Dictionary<DaSegmentData, System.Collections.Generic.List<DaSpecialSceneObjectData>> _cachedSpecialObjectsBySegment = new System.Collections.Generic.Dictionary<DaSegmentData, System.Collections.Generic.List<DaSpecialSceneObjectData>>();
        private string _cachedSpecialObjectsMapKey = string.Empty;
        private Rect _topBarRect = new Rect(8f, 30f, 1200f, PreviewTopBarHeight);
        private Rect _navigationRect = new Rect(8f, 146f, PreviewNavigationWidth, 680f);
        private Rect _inspectorRect = new Rect(820f, 146f, PreviewInspectorWidth, 680f);
        private const float PreviewTopBarHeight = 104f;
        private const float PreviewNavigationWidth = 360f;
        private const float PreviewInspectorWidth = 520f;
        private const float PreviewPanelGap = 8f;

        private void Awake()
        {
            _preview = gameObject.AddComponent<DaMapPreview>();
        }

        public void Toggle()
        {
            SetVisible(!_visible);
        }

        private void SetVisible(bool visible)
        {
            if (_visible == visible)
            {
                return;
            }

            _visible = visible;
            if (_visible)
            {
                SaveAndUnlockCursor();
                CloseGamePauseMenu();
            }
            else
            {
                DaSceneHighlighter.SelectedStep = null;
                DaSceneHighlighter.SelectedObject = null;
                if (_preview != null)
                {
                    _preview.ResetPreview();
                }

                RestoreGameCanvases();
                RestoreCursor();
            }

            DaLog.Info("Customiser UI " + (_visible ? "opened." : "closed."));
        }

        private void OnDestroy()
        {
            if (_preview != null)
            {
                _preview.ResetPreview();
            }

            RestoreGameCanvases();
            RestoreCursor();
        }

        private void OnGUI()
        {
            if (!_visible)
            {
                DaSceneHighlighter.SelectedStep = null;
                DaSceneHighlighter.SelectedObject = null;
                return;
            }

            KeepCursorUnlocked();
            if (IsPreviewFirstLayout())
            {
                FitPreviewFirstRects();
                DrawMainCameraPreviewOverlay();
                DrawPreviewFirstWindows();
            }
            else
            {
                FitWindowToScreen();
                _windowRect = GUI.Window(923410, _windowRect, DrawWindow, "DreamyAscent");
            }
        }

        private void Update()
        {
            if (_visible)
            {
                KeepCursorUnlocked();
            }
        }

        private void LateUpdate()
        {
            if (_visible)
            {
                KeepCursorUnlocked();
            }
        }

        private void DrawWindow(int windowId)
        {
            DaTerrainData data = DaTerrainExportService.LastExportedTerrain;

            GUILayout.BeginVertical();
            DrawToolbar(data);

            if (data == null || data.Map == null || data.Map.Segments == null || data.Map.Segments.Count == 0)
            {
                GUILayout.Space(8f);
                GUILayout.Label(Text("NoRuntimeData", "ui"));
                GUILayout.EndVertical();
                GUI.DragWindow();
                return;
            }

            ClampSelection(data);
            GUILayout.Space(6f);
            if (IsPreviewFirstLayout())
            {
                DrawPreviewFirstPanels(data);
            }
            else
            {
                GUILayout.BeginHorizontal();
                DrawLeftPanel(data);
                DrawPreviewPanel(data);
                GUILayout.EndHorizontal();
            }

            if (!string.IsNullOrWhiteSpace(_statusText))
            {
                GUILayout.Label(_statusText);
            }

            GUILayout.EndVertical();
            GUI.DragWindow(new Rect(0f, 0f, _windowRect.width, 24f));
        }

        private void SaveAndUnlockCursor()
        {
            if (!_cursorStateSaved)
            {
                _savedCursorLockState = Cursor.lockState;
                _savedCursorVisible = Cursor.visible;
                _cursorStateSaved = true;
                DaLog.Info("Cursor state saved: lockState=" + _savedCursorLockState + ", visible=" + _savedCursorVisible);
            }

            Input.ResetInputAxes();
            KeepCursorUnlocked();
        }

        private void KeepCursorUnlocked()
        {
            if (Cursor.lockState != CursorLockMode.None)
            {
                Cursor.lockState = CursorLockMode.None;
            }

            if (!Cursor.visible)
            {
                Cursor.visible = true;
            }
        }

        private void RestoreCursor()
        {
            if (!_cursorStateSaved)
            {
                return;
            }

            Cursor.lockState = _savedCursorLockState;
            Cursor.visible = _savedCursorVisible;
            _cursorStateSaved = false;
            DaLog.Info("Cursor state restored: lockState=" + _savedCursorLockState + ", visible=" + _savedCursorVisible);
        }

        private static void CloseGamePauseMenu()
        {
            try
            {
                System.Type guiManagerType = FindTypeByName("GUIManager");
                if (guiManagerType == null)
                {
                    return;
                }

                object instance = GetStaticMemberValue(guiManagerType, "instance") ?? GetStaticMemberValue(guiManagerType, "Instance");
                if (instance == null)
                {
                    return;
                }

                object pauseMenu = GetInstanceMemberValue(instance, "pauseMenu") ?? GetInstanceMemberValue(instance, "PauseMenu");
                GameObject pauseMenuObject = pauseMenu as GameObject;
                if (pauseMenuObject != null && pauseMenuObject.activeSelf)
                {
                    pauseMenuObject.SetActive(false);
                    DaLog.Info("Game pause menu closed for customiser input.");
                }
            }
            catch (Exception ex)
            {
                DaLog.Warn("Failed to close game pause menu: " + ex.Message);
            }
        }

        private static object GetStaticMemberValue(System.Type type, string memberName)
        {
            System.Reflection.FieldInfo field = type.GetField(memberName, System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            if (field != null)
            {
                return field.GetValue(null);
            }

            System.Reflection.PropertyInfo property = type.GetProperty(memberName, System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            return property != null && property.CanRead ? property.GetValue(null, null) : null;
        }

        private static object GetInstanceMemberValue(object target, string memberName)
        {
            if (target == null)
            {
                return null;
            }

            System.Type type = target.GetType();
            System.Reflection.FieldInfo field = type.GetField(memberName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            if (field != null)
            {
                return field.GetValue(target);
            }

            System.Reflection.PropertyInfo property = type.GetProperty(memberName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            return property != null && property.CanRead ? property.GetValue(target, null) : null;
        }

        private void HideGameCanvases()
        {
            if (_hiddenCanvasStates == null)
            {
                _hiddenCanvasStates = new System.Collections.Generic.Dictionary<UnityEngine.Object, bool>();
            }

            _hiddenCanvasStates.Clear();
            System.Type canvasType = FindTypeByName("Canvas");
            if (canvasType == null)
            {
                DaLog.Warn("Canvas type not found while trying to hide HUD.");
                return;
            }

            UnityEngine.Object[] canvases = Resources.FindObjectsOfTypeAll(canvasType);
            for (int index = 0; index < canvases.Length; index++)
            {
                UnityEngine.Object canvasObject = canvases[index];
                GameObject canvasGameObject = canvasObject is Component ? ((Component)canvasObject).gameObject : null;
                if (canvasGameObject == null || !canvasGameObject.scene.IsValid())
                {
                    continue;
                }

                if (_hiddenCanvasStates.ContainsKey(canvasObject))
                {
                    continue;
                }

                bool enabled = GetBehaviourEnabled(canvasObject);
                _hiddenCanvasStates[canvasObject] = enabled;
                SetBehaviourEnabled(canvasObject, false);
            }

            DaLog.Info("Game canvases hidden: " + _hiddenCanvasStates.Count);
        }

        private void RestoreGameCanvases()
        {
            if (_hiddenCanvasStates == null || _hiddenCanvasStates.Count == 0)
            {
                return;
            }

            foreach (System.Collections.Generic.KeyValuePair<UnityEngine.Object, bool> pair in _hiddenCanvasStates)
            {
                if (pair.Key != null)
                {
                    SetBehaviourEnabled(pair.Key, pair.Value);
                }
            }

            DaLog.Info("Game canvases restored: " + _hiddenCanvasStates.Count);
            _hiddenCanvasStates.Clear();
        }

        private static bool GetBehaviourEnabled(UnityEngine.Object target)
        {
            Behaviour behaviour = target as Behaviour;
            if (behaviour != null)
            {
                return behaviour.enabled;
            }

            Component component = target as Component;
            return component != null && component.gameObject != null && component.gameObject.activeSelf;
        }

        private static void SetBehaviourEnabled(UnityEngine.Object target, bool enabled)
        {
            Behaviour behaviour = target as Behaviour;
            if (behaviour != null)
            {
                behaviour.enabled = enabled;
                return;
            }

            Component component = target as Component;
            if (component != null && component.gameObject != null)
            {
                component.gameObject.SetActive(enabled);
            }
        }

        private static System.Type FindTypeByName(string typeName)
        {
            if (string.IsNullOrWhiteSpace(typeName))
            {
                return null;
            }

            System.Reflection.Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int index = 0; index < assemblies.Length; index++)
            {
                System.Type[] types;
                try
                {
                    types = assemblies[index].GetTypes();
                }
                catch
                {
                    continue;
                }

                for (int typeIndex = 0; typeIndex < types.Length; typeIndex++)
                {
                    System.Type type = types[typeIndex];
                    if (type == null)
                    {
                        continue;
                    }

                    if (string.Equals(type.Name, typeName, StringComparison.Ordinal) ||
                        string.Equals(type.FullName, typeName, StringComparison.Ordinal) ||
                        type.FullName != null && type.FullName.EndsWith("." + typeName, StringComparison.Ordinal))
                    {
                        return type;
                    }
                }
            }

            return null;
        }

        private void DrawTree(DaTerrainData data, float height = 250f)
        {
            if (data == null || data.Map == null || data.Map.Segments == null)
            {
                return;
            }

            GUILayoutOption heightOption = height > 0f ? GUILayout.Height(height) : GUILayout.ExpandHeight(true);
            GUILayout.BeginVertical(GUI.skin.box, heightOption);
            GUILayout.Label(Text("Structure", "ui"));

            DrawSegmentSelectorRow(data);

            DaSegmentData selectedSegment = GetSelectedSegment(data);
            DrawStepSelectorList(selectedSegment);
            GUILayout.EndVertical();
        }

        private void DrawSegmentSelectorRow(DaTerrainData data)
        {
            GUILayout.Label(Text("LevelNameRow", "ui"));
            _segmentRowScroll = GUILayout.BeginScrollView(
                _segmentRowScroll,
                false,
                false,
                GUIStyle.none,
                GUI.skin.horizontalScrollbar,
                GUIStyle.none,
                GUILayout.Height(36f));
            GUILayout.BeginHorizontal();

            for (int segmentIndex = 0; segmentIndex < data.Map.Segments.Count; segmentIndex++)
            {
                DaSegmentData segment = data.Map.Segments[segmentIndex];
                if (segment == null)
                {
                    continue;
                }

                bool selectedSegment = segmentIndex == _selectedSegmentIndex;
                if (DrawSelectorButton(Text(segment.SegmentName, "segment"), selectedSegment, 56f, 96f))
                {
                    _selectedSegmentIndex = segmentIndex;
                    _selectedGrouperIndex = 0;
                    _selectedStepIndex = 0;
                    _selectedSpecialObjectIndex = -1;
                    if (_preview != null)
                    {
                        _preview.SetTarget(segment);
                    }
                }
            }

            GUILayout.EndHorizontal();
            GUILayout.EndScrollView();
        }

        private void DrawGrouperSelectorRow(DaSegmentData segment)
        {
            GUILayout.Space(4f);
            GUILayout.BeginHorizontal();
            GUILayout.Label(Text("AreaNameRow", "ui"), GUILayout.ExpandWidth(true));
            if (segment != null && segment.Groupers != null)
            {
                GUILayout.Label(Text("AreaCount", "ui") + ": " + segment.Groupers.Count, GUILayout.Width(82f));
            }

            GUILayout.EndHorizontal();

            if (segment == null || segment.Groupers == null || segment.Groupers.Count == 0)
            {
                GUILayout.Label(Text("NoGroupers", "ui"));
                return;
            }

            _grouperRowScroll = GUILayout.BeginScrollView(
                _grouperRowScroll,
                false,
                false,
                GUIStyle.none,
                GUI.skin.horizontalScrollbar,
                GUIStyle.none,
                GUILayout.Height(36f));
            GUILayout.BeginHorizontal();

            for (int grouperIndex = 0; grouperIndex < segment.Groupers.Count; grouperIndex++)
            {
                DaPropGrouperData grouper = segment.Groupers[grouperIndex];
                if (grouper == null)
                {
                    continue;
                }

                if (DrawSelectorButton(Text(grouper.GrouperName, "grouper"), grouperIndex == _selectedGrouperIndex, 74f, 132f))
                {
                    _selectedGrouperIndex = grouperIndex;
                    _selectedStepIndex = 0;
                }
            }

            GUILayout.EndHorizontal();
            GUILayout.EndScrollView();
        }

        private void DrawStepSelectorList(DaSegmentData segment)
        {
            GUILayout.Space(6f);
            GUILayout.BeginHorizontal();
            GUILayout.Label(Text("GeneratedObjects", "ui"), GUILayout.ExpandWidth(true));
            int grouperCount = segment != null && segment.Groupers != null ? segment.Groupers.Count : 0;
            int stepCount = CountSegmentSteps(segment);
            GUILayout.Label(Text("AreaCount", "ui") + ": " + grouperCount + "  " + Text("StepCount", "ui") + ": " + stepCount, GUILayout.Width(150f));
            GUILayout.EndHorizontal();

            if (segment != null)
            {
                GUILayout.Label(Text(segment.SegmentName ?? string.Empty, "segment"));
            }

            if (segment == null || segment.Groupers == null || segment.Groupers.Count == 0 || stepCount == 0)
            {
                GUILayout.Label(Text("NoGeneratedObjects", "ui"));
                return;
            }

            _treeScroll = GUILayout.BeginScrollView(_treeScroll, GUILayout.ExpandHeight(true));

            for (int grouperIndex = 0; grouperIndex < segment.Groupers.Count; grouperIndex++)
            {
                DaPropGrouperData grouper = segment.Groupers[grouperIndex];
                if (grouper == null)
                {
                    continue;
                }

                EnsureRuntimeCounts(grouper);
                GUILayout.Label(Text(grouper.GrouperName ?? string.Empty, "grouper") + "    " + Text("RuntimeLive", "ui") + ": " + GetRuntimeGrouperChildCount(grouper));
                if (grouper.Steps == null || grouper.Steps.Count == 0)
                {
                    continue;
                }

                for (int stepIndex = 0; stepIndex < grouper.Steps.Count; stepIndex++)
                {
                    DaLevelGenStepData step = grouper.Steps[stepIndex];
                    if (step == null)
                    {
                        continue;
                    }

                    int liveObjectCount = GetRuntimeStepChildCount(step);
                    string marker = HasChanges(step) ? "* " : string.Empty;
                    if (DrawStepButton(
                        marker + Text(step.StepName, "step"),
                        Text(step.StepType, "stepType"),
                        BuildRuntimeStepSummary(step, liveObjectCount),
                        grouperIndex == _selectedGrouperIndex && stepIndex == _selectedStepIndex))
                    {
                        _selectedGrouperIndex = grouperIndex;
                        _selectedStepIndex = stepIndex;
                        _selectedSpecialObjectIndex = -1;
                        _detailScroll = Vector2.zero;
                    }
                }
            }

            DrawSpecialSceneObjectList(segment);
            GUILayout.EndScrollView();
        }

        private void DrawSpecialSceneObjectList(DaSegmentData segment)
        {
            System.Collections.Generic.List<DaSpecialSceneObjectData> specialObjects = GetSpecialSceneObjects(segment);
            GUILayout.Space(8f);
            GUILayout.Label(Text("SpecialSceneObjects", "ui") + ": " + specialObjects.Count);
            if (specialObjects.Count == 0)
            {
                GUILayout.Label(Text("NoSpecialSceneObjects", "ui"));
                return;
            }

            for (int index = 0; index < specialObjects.Count; index++)
            {
                DaSpecialSceneObjectData item = specialObjects[index];
                if (item == null)
                {
                    continue;
                }

                string summary = item.Category + "    " + Text("RendererCount", "ui") + ": " + item.RendererCount + "    " + Text("ColliderCount", "ui") + ": " + item.ColliderCount;
                if (DrawStepButton(item.DisplayName, item.Reason, summary, index == _selectedSpecialObjectIndex))
                {
                    _selectedSpecialObjectIndex = index;
                    _detailScroll = Vector2.zero;
                }
            }
        }

        private System.Collections.Generic.List<DaSpecialSceneObjectData> GetSpecialSceneObjects(DaSegmentData segment)
        {
            if (segment == null)
            {
                return new System.Collections.Generic.List<DaSpecialSceneObjectData>();
            }

            EnsureSpecialSceneObjectCache(DaTerrainExportService.LastExportedTerrain);
            System.Collections.Generic.List<DaSpecialSceneObjectData> items;
            if (_cachedSpecialObjectsBySegment.TryGetValue(segment, out items))
            {
                return items;
            }

            items = DaSpecialSceneObjectService.Scan(segment);
            _cachedSpecialObjectsBySegment[segment] = items;
            return items;
        }

        private void EnsureSpecialSceneObjectCache(DaTerrainData data)
        {
            string mapKey = data != null && data.Map != null ? data.Map.MapKey ?? string.Empty : string.Empty;
            if (string.Equals(_cachedSpecialObjectsMapKey, mapKey, StringComparison.Ordinal))
            {
                return;
            }

            _cachedSpecialObjectsMapKey = mapKey;
            _cachedSpecialObjectsBySegment = DaSpecialSceneObjectService.ScanAll(data);
        }

        private static int CountSegmentSteps(DaSegmentData segment)
        {
            int count = 0;
            for (int grouperIndex = 0; segment != null && segment.Groupers != null && grouperIndex < segment.Groupers.Count; grouperIndex++)
            {
                DaPropGrouperData grouper = segment.Groupers[grouperIndex];
                if (grouper != null && grouper.Steps != null)
                {
                    count += grouper.Steps.Count;
                }
            }

            return count;
        }

        private static bool DrawSelectorButton(string label, bool selected, float minWidth, float maxWidth)
        {
            Color oldColor = GUI.color;
            if (selected)
            {
                GUI.color = new Color(0.55f, 0.95f, 1f, 1f);
            }

            float width = Mathf.Clamp(GUI.skin.button.CalcSize(new GUIContent(label)).x + 24f, minWidth, maxWidth);
            bool clicked = GUILayout.Button(label, GUILayout.Width(width), GUILayout.Height(24f));
            GUI.color = oldColor;
            return clicked;
        }

        private static bool DrawStepButton(string name, string stepType, string summary, bool selected)
        {
            Color oldColor = GUI.color;
            if (selected)
            {
                GUI.color = new Color(0.55f, 0.95f, 1f, 1f);
            }

            string label = name;
            if (!string.IsNullOrWhiteSpace(stepType) || !string.IsNullOrWhiteSpace(summary))
            {
                label += "\n" + stepType + (string.IsNullOrWhiteSpace(summary) ? string.Empty : "    " + summary);
            }

            bool clicked = GUILayout.Button(label, GUI.skin.button, GUILayout.ExpandWidth(true), GUILayout.Height(40f));
            GUI.color = oldColor;
            return clicked;
        }

        private string BuildRuntimeStepSummary(DaLevelGenStepData step, int liveObjectCount)
        {
            if (step == null)
            {
                return string.Empty;
            }

            if (step.SourceObject == null)
            {
                return Text("RuntimeMissing", "ui");
            }

            return Text("RuntimeLive", "ui") + ": " + liveObjectCount;
        }

        private static int CountLiveSteps(DaPropGrouperData grouper)
        {
            if (grouper == null || grouper.SourceObject == null || grouper.SourceObject.transform == null)
            {
                return 0;
            }

            int count = 0;
            for (int index = 0; index < grouper.SourceObject.transform.childCount; index++)
            {
                Transform child = grouper.SourceObject.transform.GetChild(index);
                if (child != null && child.GetComponent<LevelGenStep>() != null)
                {
                    count++;
                }
            }

            return count;
        }

        private static int CountLiveChildren(Transform root)
        {
            if (root == null)
            {
                return 0;
            }

            int count = 0;
            for (int index = 0; index < root.childCount; index++)
            {
                if (root.GetChild(index) != null)
                {
                    count++;
                }
            }

            return count;
        }

        private int GetRuntimeStepChildCount(DaLevelGenStepData step)
        {
            if (step == null)
            {
                return 0;
            }

            int count;
            if (_cachedRuntimeStepChildCounts != null && _cachedRuntimeStepChildCounts.TryGetValue(step, out count))
            {
                return count;
            }

            count = CountRuntimeStepChildren(step);
            if (_cachedRuntimeStepChildCounts == null)
            {
                _cachedRuntimeStepChildCounts = new System.Collections.Generic.Dictionary<DaLevelGenStepData, int>();
            }

            _cachedRuntimeStepChildCounts[step] = count;
            return count;
        }

        private int GetRuntimeGrouperChildCount(DaPropGrouperData grouper)
        {
            if (grouper == null)
            {
                return 0;
            }

            int count;
            if (_cachedRuntimeGrouperChildCounts != null && _cachedRuntimeGrouperChildCounts.TryGetValue(grouper, out count))
            {
                return count;
            }

            count = CountRuntimeGrouperChildren(grouper);
            if (_cachedRuntimeGrouperChildCounts == null)
            {
                _cachedRuntimeGrouperChildCounts = new System.Collections.Generic.Dictionary<DaPropGrouperData, int>();
            }

            _cachedRuntimeGrouperChildCounts[grouper] = count;
            return count;
        }

        private void EnsureRuntimeCounts(DaPropGrouperData grouper)
        {
            if (grouper == null)
            {
                return;
            }

            GetRuntimeGrouperChildCount(grouper);
            if (grouper.Steps == null)
            {
                return;
            }

            for (int index = 0; index < grouper.Steps.Count; index++)
            {
                DaLevelGenStepData step = grouper.Steps[index];
                if (step != null)
                {
                    GetRuntimeStepChildCount(step);
                }
            }
        }

        private static int CountRuntimeStepChildren(DaLevelGenStepData step)
        {
            if (step == null || step.SourceObject == null || step.SourceObject.transform == null)
            {
                return 0;
            }

            return step.SourceObject.transform.childCount;
        }

        private static int CountRuntimeGrouperChildren(DaPropGrouperData grouper)
        {
            if (grouper == null || grouper.SourceObject == null || grouper.SourceObject.transform == null)
            {
                return 0;
            }

            return grouper.SourceObject.transform.childCount;
        }

        private void DrawDetail(DaTerrainData data)
        {
            DaSegmentData segment = GetSelectedSegment(data);
            DaPropGrouperData grouper = GetSelectedGrouper(segment);
            DaLevelGenStepData step = GetSelectedStep(grouper);
            DaSpecialSceneObjectData specialObject = GetSelectedSpecialSceneObject(segment);
            DaSceneHighlighter.SelectedStep = specialObject == null ? step : null;
            DaSceneHighlighter.SelectedObject = specialObject != null ? specialObject.SourceObject : null;
            SyncFocusedParameterWithSelection(step);

            DrawDetailHeader(segment, grouper, step, specialObject);

            DrawDetailTabs();
            _detailScroll = GUILayout.BeginScrollView(
                _detailScroll,
                false,
                true,
                GUIStyle.none,
                GUI.skin.verticalScrollbar,
                GUILayout.ExpandHeight(true));
            if (_detailTabIndex == 0)
            {
                if (specialObject != null)
                {
                    DrawSpecialSceneObjectDetail(specialObject);
                }
                else if (step == null)
                {
                    GUILayout.Label(Text("NoStepSelected", "ui"));
                }
                else
                {
                    DrawStepEditor(step);
                }
            }
            else
            {
                DrawSegmentCatalog(segment);
            }

            GUILayout.EndScrollView();
        }

        private void DrawPreviewPanel(DaTerrainData data)
        {
            DaSegmentData segment = GetSelectedSegment(data);
            if (_preview != null)
            {
                _preview.SetTarget(segment);
            }

            GUILayout.BeginVertical(GUI.skin.box, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            GUILayout.BeginHorizontal();
            GUILayout.Label(Text("Preview", "ui") + ": " + Text(segment != null ? segment.SegmentName : string.Empty, "segment"), GUILayout.ExpandWidth(true));
            GUILayout.Label(Text("PreviewHelp", "ui"), GUILayout.Width(360f));
            GUILayout.EndHorizontal();

            Rect rect = GUILayoutUtility.GetRect(300f, 300f, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            if (_preview != null)
            {
                _preview.Draw(rect);
            }
            else
            {
                GUI.Box(rect, Text("PreviewUnavailable", "ui"));
            }

            GUILayout.EndVertical();
        }

        private void DrawDetailHeader(DaSegmentData segment, DaPropGrouperData grouper, DaLevelGenStepData step, DaSpecialSceneObjectData specialObject)
        {
            GUILayout.Label(Text("Segment", "ui") + ": " + Text(segment != null ? segment.SegmentName : string.Empty, "segment"));
            if (specialObject != null)
            {
                GUILayout.Label(Text("SpecialSceneObject", "ui") + ": " + specialObject.DisplayName + "    " + Text("Type", "ui") + ": " + specialObject.Category);
            }
            else
            {
                GUILayout.Label(string.Format(
                    "{0}: {1}    {2}: {3}    {4}: {5}",
                    Text("Grouper", "ui"),
                    Text(grouper != null ? grouper.GrouperName : string.Empty, "grouper"),
                    Text("Step", "ui"),
                    Text(step != null ? step.StepName : string.Empty, "step"),
                    Text("Type", "ui"),
                    Text(step != null ? step.StepType : string.Empty, "stepType")));
            }

            GUILayout.BeginHorizontal();
            bool canGenerateGrouper = segment == null || segment.EditMode != DaSegmentEditMode.CustomBlank;
            bool oldEnabled = GUI.enabled;
            GUI.enabled = oldEnabled && canGenerateGrouper;
            if (GUILayout.Button(Text("GenerateGrouper", "ui"), GUILayout.Height(24f)))
            {
                ReleasePreviewSceneIsolationForExport();
                EnsureRuntimeReferences(segment, grouper);
                if (DaRuntimeEditService.RunGrouper(grouper))
                {
                    _statusText = Text("GeneratedGrouper", "ui") + ": " + (grouper != null ? grouper.GrouperName : string.Empty);
                    RefreshRuntimeSnapshotAfterMutation(false);
                }
            }
            GUI.enabled = oldEnabled;

            if (GUILayout.Button(Text("GenerateSegment", "ui"), GUILayout.Height(24f)))
            {
                ReleasePreviewSceneIsolationForExport();
                int count = DaRuntimeEditService.RunSegment(segment);
                _statusText = GetSegmentMutationStatus(segment, count);
                DaPlacementRuntimeResult placementResult = RunPlacementRulesForSegment(segment);
                if (placementResult != null)
                {
                    _statusText += "  " + BuildPlacementRuntimeStatus(placementResult);
                }

                RefreshRuntimeSnapshotAfterMutation(false);
            }

            GUILayout.EndHorizontal();

            DrawSegmentEditMode(segment);
        }

        private void DrawSegmentEditMode(DaSegmentData segment)
        {
            if (segment == null)
            {
                return;
            }

            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label(Text("SegmentEditMode", "ui") + ": " + Text(segment.EditMode.ToString(), "ui"));
            GUILayout.BeginHorizontal();
            DrawSegmentEditModeButton(segment, DaSegmentEditMode.OfficialTemplate);
            DrawSegmentEditModeButton(segment, DaSegmentEditMode.CustomBlank);
            DrawSegmentEditModeButton(segment, DaSegmentEditMode.Hybrid);
            GUILayout.EndHorizontal();
            GUILayout.Label(GetSegmentEditModeHint(segment.EditMode));
            GUILayout.EndVertical();
        }

        private void DrawSegmentEditModeButton(DaSegmentData segment, DaSegmentEditMode mode)
        {
            Color oldColor = GUI.color;
            if (segment.EditMode == mode)
            {
                GUI.color = new Color(0.55f, 0.95f, 1f, 1f);
            }

            if (GUILayout.Button(Text(mode.ToString(), "ui"), GUILayout.Height(24f)))
            {
                if (segment.EditMode != mode)
                {
                    DaSegmentEditMode oldMode = segment.EditMode;
                    segment.EditMode = mode;
                    ReleasePreviewSceneIsolationForExport();
                    int count = DaRuntimeEditService.RunSegment(segment);
                    _statusText = Text("SegmentEditModeChanged", "ui") + ": " + Text(mode.ToString(), "ui") + "  " + GetSegmentMutationStatus(segment, count);
                    DaPlacementRuntimeResult placementResult = RunPlacementRulesForSegment(segment);
                    if (placementResult != null)
                    {
                        _statusText += "  " + BuildPlacementRuntimeStatus(placementResult);
                    }

                    DaLog.Info("Segment edit mode changed. segment=" + segment.SegmentName + ", mode=" + mode);
                    RefreshRuntimeSnapshotAfterMutation(false);

                    if (oldMode == DaSegmentEditMode.CustomBlank && mode == DaSegmentEditMode.OfficialTemplate)
                    {
                        _statusText += "  " + Text("OfficialAfterBlankLimited", "ui");
                        DaLog.Warn("Switching from custom blank back to official template does not fully restore generated dependencies. Restart the run or rescan a fresh map before relying on official generation.");
                    }
                }
            }

            GUI.color = oldColor;
        }

        private static string GetSegmentEditModeHint(DaSegmentEditMode mode)
        {
            switch (mode)
            {
                case DaSegmentEditMode.CustomBlank:
                    return Text("ModeCustomBlankHint", "ui");
                case DaSegmentEditMode.Hybrid:
                    return Text("ModeHybridHint", "ui");
                default:
                    return Text("ModeOfficialTemplateHint", "ui");
            }
        }

        private static string GetSegmentMutationStatus(DaSegmentData segment, int count)
        {
            if (segment != null && segment.EditMode == DaSegmentEditMode.CustomBlank)
            {
                return Text("ClearedSegmentObjects", "ui") + ": " + count;
            }

            return Text("GeneratedSegment", "ui") + ": " + count;
        }

        private DaPlacementRuntimeResult RunPlacementRulesForSegment(DaSegmentData segment, bool force = false)
        {
            if (segment == null || segment.PlacementRules == null || segment.PlacementRules.Count == 0)
            {
                return null;
            }

            if (!force && segment.EditMode == DaSegmentEditMode.OfficialTemplate)
            {
                return null;
            }

            return DaPlacementRuntimeService.RunPlacementRules(DaTerrainExportService.LastExportedTerrain, segment);
        }

        private static string BuildPlacementRuntimeStatus(DaPlacementRuntimeResult result)
        {
            if (result == null)
            {
                return Text("NoPlacementRules", "ui");
            }

            string status = string.Format(
                CultureInfo.InvariantCulture,
                "{0}: {1}  {2}: {3}  {4}: {5}",
                Text("PlacementRuntimeSpawned", "ui"),
                result.SpawnedObjects,
                Text("PlacementRuntimeAppliedRules", "ui"),
                result.AppliedRules,
                Text("PlacementRuntimeFailedRules", "ui"),
                result.FailedRules + result.SkippedRules);

            if (result.Warnings != null && result.Warnings.Count > 0)
            {
                status += "  " + Text("PlacementRuntimeWarnings", "ui") + ": " + result.Warnings.Count;
            }

            return status;
        }

        private void DrawStepEditor(DaLevelGenStepData step)
        {
            DrawPropertyGroup(Text("Properties", "ui"), step, null, step.Properties);
            DrawConstraintGroup(Text("Modifiers", "ui"), step.Modifiers);
            DrawConstraintGroup(Text("Constraints", "ui"), step.Constraints);
            DrawConstraintGroup(Text("PostConstraints", "ui"), step.PostConstraints);
        }

        private void DrawSpecialSceneObjectDetail(DaSpecialSceneObjectData item)
        {
            if (item == null)
            {
                return;
            }

            GUILayout.Label(Text("SpecialSceneObjectManageHint", "ui"));
            DrawSpecialSceneObjectActions(item);
            GUILayout.Space(6f);
            DrawReadonlyRow(Text("Name", "ui"), item.DisplayName);
            DrawReadonlyRow(Text("Type", "ui"), item.Category);
            DrawReadonlyRow(Text("Reason", "ui"), item.Reason);
            DrawReadonlyRow(Text("ManageState", "ui"), BuildSpecialObjectManageState(item));
            DrawReadonlyRow(Text("HierarchyPath", "ui"), item.Path);
            DrawReadonlyRow(Text("ParentPath", "ui"), item.ParentPath);
            DrawReadonlyRow(Text("RootPath", "ui"), item.RootPath);
            DrawReadonlyRow(Text("Active", "ui"), item.ActiveSelf + " / " + item.ActiveInHierarchy);
            DrawReadonlyRow(Text("Layer", "ui"), item.Layer.ToString(CultureInfo.InvariantCulture));
            DrawReadonlyRow(Text("Tag", "ui"), item.Tag);
            DrawReadonlyRow(Text("LocalPosition", "ui"), FormatVector(item.LocalPosition));
            DrawReadonlyRow(Text("WorldPosition", "ui"), FormatVector(item.WorldPosition));
            DrawReadonlyRow(Text("RendererCount", "ui"), item.RendererCount.ToString(CultureInfo.InvariantCulture));
            DrawReadonlyRow(Text("ColliderCount", "ui"), item.ColliderCount.ToString(CultureInfo.InvariantCulture));
            DrawReadonlyRow(Text("Components", "ui"), item.Components != null ? string.Join(", ", item.Components.ToArray()) : string.Empty);
            DrawReadonlyRow(Text("Materials", "ui"), item.Materials != null ? string.Join(", ", item.Materials.ToArray()) : string.Empty);
        }

        private void DrawSpecialSceneObjectActions(DaSpecialSceneObjectData item)
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(Text("RefreshSpecialObjects", "ui"), GUILayout.Height(24f)))
            {
                RefreshSpecialSceneObjects();
                _statusText = Text("SpecialObjectsRefreshed", "ui");
            }

            bool oldEnabled = GUI.enabled;
            GUI.enabled = oldEnabled && item.SourceObject != null && item.CanToggleActive;
            string toggleLabel = item.SourceObject != null && item.SourceObject.activeSelf ? Text("HideSpecialObject", "ui") : Text("ShowSpecialObject", "ui");
            if (GUILayout.Button(toggleLabel, GUILayout.Height(24f)))
            {
                bool nextActive = item.SourceObject == null || !item.SourceObject.activeSelf;
                if (DaSpecialSceneObjectService.TrySetActive(item, nextActive))
                {
                    _statusText = (nextActive ? Text("SpecialObjectShown", "ui") : Text("SpecialObjectHidden", "ui")) + ": " + item.DisplayName;
                    RefreshSpecialSceneObjects();
                }
            }

            GUI.enabled = oldEnabled && item.SourceObject != null && item.CanDelete;
            if (GUILayout.Button(Text("DeleteSpecialObject", "ui"), GUILayout.Height(24f)))
            {
                string failure;
                if (DaSpecialSceneObjectService.TryDelete(item, out failure))
                {
                    _statusText = Text("SpecialObjectDeleted", "ui") + ": " + item.DisplayName;
                    _selectedSpecialObjectIndex = -1;
                    RefreshSpecialSceneObjects();
                    RefreshRuntimeSnapshotAfterMutation(false);
                }
                else
                {
                    _statusText = Text("SpecialObjectDeleteBlocked", "ui") + ": " + failure;
                }
            }

            GUI.enabled = oldEnabled;
            GUILayout.EndHorizontal();
        }

        private string BuildSpecialObjectManageState(DaSpecialSceneObjectData item)
        {
            if (item == null)
            {
                return string.Empty;
            }

            string state = Text("CanToggleActive", "ui") + ": " + item.CanToggleActive + "    " + Text("CanDelete", "ui") + ": " + item.CanDelete;
            if (item.IsProtected)
            {
                state += "    " + Text("ProtectedObject", "ui") + ": " + (item.ProtectionReason ?? string.Empty);
            }

            return state;
        }

        private static void DrawReadonlyRow(string label, string value)
        {
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label(label);
            GUILayout.TextArea(value ?? string.Empty);
            GUILayout.EndVertical();
        }

        private static string FormatVector(Vector3 value)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0:0.###}, {1:0.###}, {2:0.###}", value.x, value.y, value.z);
        }

        private void DrawDetailTabs()
        {
            GUILayout.BeginHorizontal();
            DrawDetailTabButton(0, Text("ParametersTab", "ui"));
            DrawDetailTabButton(1, Text("Catalog", "ui"));
            GUILayout.EndHorizontal();
        }

        private void DrawDetailTabButton(int index, string label)
        {
            Color oldColor = GUI.color;
            if (_detailTabIndex == index)
            {
                GUI.color = new Color(0.55f, 0.95f, 1f, 1f);
            }

            if (GUILayout.Button(label, GUILayout.Height(24f)))
            {
                _detailTabIndex = index;
                _detailScroll = Vector2.zero;
            }

            GUI.color = oldColor;
        }

        private void DrawSegmentCatalog(DaSegmentData segment)
        {
            if (segment == null)
            {
                return;
            }

            DaObjectCatalog catalog = DaObjectCatalogService.GetCurrentCatalog(DaTerrainExportService.LastExportedTerrain);
            EnsureCatalogCaches(catalog);
            DaCatalogSegment catalogSegment = FindCatalogSegment(catalog, segment.SegmentName);
            if (catalog == null || catalogSegment == null)
            {
                return;
            }

            GUILayout.Space(10f);
            GUILayout.Label(Text("Catalog", "ui") + ": " + catalogSegment.DisplayName);
            GUILayout.Label(string.Format(
                "{0}: {1}    {2}: {3}",
                Text("CatalogItems", "ui"),
                catalogSegment.ItemIds.Count,
                Text("CatalogMaterials", "ui"),
                catalogSegment.MaterialIds.Count));

            DrawObjectRegistrySummary(segment);
            DrawParentChildRegistrySummary(segment);
            DrawPlacementConfig(segment);
            DrawCatalogItemsSection(catalog, catalogSegment);
            DrawCatalogMaterialsSection(catalog, catalogSegment);
        }

        private void DrawObjectRegistrySummary(DaSegmentData segment)
        {
            if (!_sampleAssetsLoaded)
            {
                GUILayout.Space(8f);
                GUILayout.BeginVertical(GUI.skin.box);
                GUILayout.Label(Text("ObjectRegistry", "ui") + ": " + Text("SampleAssetsNotLoaded", "ui"));
                GUILayout.Label(Text("SampleAssetsManualLoadHint", "ui"));
                if (GUILayout.Button(Text("LoadSampleAssets", "ui"), GUILayout.Height(24f)))
                {
                    _sampleAssetsLoaded = true;
                    DaTemplateSnapshotService.Invalidate();
                    DaObjectRegistryService.Invalidate();
                    DaParentChildRegistryService.Invalidate();
                    DaTemplateBaselineService.Invalidate();
                    _cachedSnapshotMatch = null;
                    _cachedSnapshotMatchMapKey = string.Empty;
                }

                GUILayout.EndVertical();
                return;
            }

            DaObjectRegistry registry = DaObjectRegistryService.GetRegistry();
            if (registry == null)
            {
                GUILayout.Space(6f);
                GUILayout.Label(Text("ObjectRegistry", "ui") + ": " + Text("ObjectRegistryMissing", "ui"));
                GUILayout.Label(DaObjectRegistryService.RegistryFilePath ?? string.Empty);
                return;
            }

            GUILayout.Space(8f);
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label(Text("ObjectRegistry", "ui"));
            DaObjectRegistrySummary summary = registry.Summary;
            if (summary != null)
            {
                GUILayout.Label(string.Format(
                    "{0}: {1}    {2}: {3}    {4}: {5}    {6}: {7}",
                    Text("RegistryTemplates", "ui"),
                    summary.TemplateCount,
                    Text("RegistryMaterials", "ui"),
                    summary.MaterialCount,
                    Text("RegistryTechnicalLowRisk", "ui"),
                    summary.TechnicalLowRiskPlacementCandidateCount,
                    Text("RegistryRecommended", "ui"),
                    summary.RecommendedFirstPassCandidateCount));
            }

            _showRegistryCandidates = DrawFoldoutHeader(
                _showRegistryCandidates,
                Text("RegistryRecommended", "ui") + ": " + DaObjectRegistryService.GetRecommendedTemplatesForSegment(segment).Count);
            if (_showRegistryCandidates)
            {
                DrawRecommendedRegistryTemplates(segment);
            }

            GUILayout.EndVertical();
        }

        private void DrawParentChildRegistrySummary(DaSegmentData segment)
        {
            if (!_sampleAssetsLoaded)
            {
                return;
            }

            DaParentChildRegistry registry = DaParentChildRegistryService.GetRegistry();
            GUILayout.Space(8f);
            GUILayout.BeginVertical(GUI.skin.box);
            if (registry == null)
            {
                GUILayout.Label(Text("ParentChildRegistry", "ui") + ": " + Text("ParentChildRegistryMissing", "ui"));
                GUILayout.Label(DaParentChildRegistryService.RegistryFilePath ?? string.Empty);
                GUILayout.EndVertical();
                return;
            }

            GUILayout.Label(Text("ParentChildRegistry", "ui"));
            if (registry.Summary != null)
            {
                GUILayout.Label(string.Format(
                    "{0}: {1}    {2}: {3}    {4}: {5}",
                    Text("ParentChildTemplates", "ui"),
                    registry.Summary.TemplateCount,
                    Text("RegistryTechnicalLowRisk", "ui"),
                    registry.Summary.TechnicalLowRiskPlacementCandidateCount,
                    Text("RegistryRecommended", "ui"),
                    registry.Summary.RecommendedFirstPassCandidateCount));
            }

            _showParentChildRegistry = DrawFoldoutHeader(
                _showParentChildRegistry,
                Text("ParentChildTemplates", "ui") + ": " + DaParentChildRegistryService.GetTemplatesForSegment(segment).Count);
            if (_showParentChildRegistry)
            {
                DrawParentChildTemplates(segment);
            }

            GUILayout.EndVertical();
        }

        private void DrawParentChildTemplates(DaSegmentData segment)
        {
            System.Collections.Generic.List<DaParentChildRegistryTemplate> templates = DaParentChildRegistryService.GetTemplatesForSegment(segment);
            int drawn = 0;
            for (int index = 0; index < templates.Count && drawn < 8; index++)
            {
                DaParentChildRegistryTemplate template = templates[index];
                if (template == null)
                {
                    continue;
                }

                GUILayout.BeginVertical(GUI.skin.box);
                GUILayout.Label(template.DisplayName ?? template.Name ?? string.Empty);
                GUILayout.Label("template: " + (template.RegistryId ?? string.Empty));
                GUILayout.Label(Text("Source", "ui") + ": " + (template.SourceExamples.Count > 0 ? template.SourceExamples[0].StepPath ?? string.Empty : string.Empty));
                GUILayout.Label(BuildParentChildFlags(template));
                GUILayout.EndVertical();
                drawn++;
            }

            if (templates.Count > drawn)
            {
                GUILayout.Label("... " + (templates.Count - drawn));
            }
        }

        private string BuildParentChildFlags(DaParentChildRegistryTemplate template)
        {
            System.Collections.Generic.List<string> flags = new System.Collections.Generic.List<string>();
            if (template.HasChildGeneration)
            {
                flags.Add(Text("CatalogChild", "ui") + "=" + template.ChildLevelGenStepCount);
            }

            if (template.HasSingleItemSpawner)
            {
                flags.Add(Text("CatalogSingleItemSpawner", "ui"));
            }

            if (template.HasPhotonView)
            {
                flags.Add(Text("PhotonView", "ui"));
            }

            return string.Join("  ", flags.ToArray());
        }

        private void DrawRecommendedRegistryTemplates(DaSegmentData segment)
        {
            if (segment == null)
            {
                return;
            }

            System.Collections.Generic.List<DaObjectRegistryTemplate> templates = DaObjectRegistryService.GetRecommendedTemplatesForSegment(segment);
            int drawn = 0;
            for (int index = 0; index < templates.Count && drawn < 12; index++)
            {
                DaObjectRegistryTemplate template = templates[index];
                if (template == null)
                {
                    continue;
                }

                DrawRegistryTemplateRow(segment, template);
                drawn++;
            }

            if (templates.Count > drawn)
            {
                GUILayout.Label("... " + (templates.Count - drawn));
            }
        }

        private void DrawRegistryTemplateRow(DaSegmentData segment, DaObjectRegistryTemplate template)
        {
            bool supported = DaPlacementRuntimeService.IsTemplateSupportedForDirectPlacement(template, out string unsupportedReason);
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.BeginHorizontal();
            bool oldEnabled = GUI.enabled;
            GUI.enabled = oldEnabled && supported;
            if (GUILayout.Button("+", GUILayout.Width(30f), GUILayout.Height(24f)))
            {
                AddPlacementRuleFromTemplate(segment, template);
            }

            GUI.enabled = oldEnabled;
            GUILayout.BeginVertical();
            GUILayout.Label(template.DisplayName ?? template.Name ?? string.Empty, GUILayout.ExpandWidth(true));
            GUILayout.Label("template: " + (template.RegistryId ?? string.Empty), GUILayout.ExpandWidth(true));
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
            if (template.SourceExamples != null && template.SourceExamples.Count > 0)
            {
                DaObjectRegistrySourceExample source = template.SourceExamples[0];
                GUILayout.Label(Text("Source", "ui") + ": " + (source.StepPath ?? string.Empty));
            }

            if (template.RiskTags != null && template.RiskTags.Count > 0)
            {
                GUILayout.Label(Text("RegistryRiskTags", "ui") + ": " + string.Join(", ", template.RiskTags.ToArray()));
            }

            if (!supported)
            {
                GUILayout.Label(Text("PlacementTemplateUnsupported", "ui") + ": " + (unsupportedReason ?? string.Empty));
            }

            GUILayout.EndVertical();
        }

        private void DrawPlacementConfig(DaSegmentData segment)
        {
            if (segment == null)
            {
                return;
            }

            EnsurePlacementLists(segment);
            GUILayout.Space(8f);
            GUILayout.BeginVertical(GUI.skin.box);
            _showPlacementConfig = DrawFoldoutHeader(
                _showPlacementConfig,
                Text("PlacementConfig", "ui") + "  " +
                Text("PlacementRegions", "ui") + "=" + segment.SubAreas.Count + "  " +
                Text("PlacementRules", "ui") + "=" + segment.PlacementRules.Count);
            if (!_showPlacementConfig)
            {
                GUILayout.EndVertical();
                return;
            }

            GUILayout.BeginHorizontal();
            GUILayout.Label(Text("PlacementConfigHint", "ui"), GUILayout.ExpandWidth(true));
            if (GUILayout.Button(Text("RunPlacementRules", "ui"), GUILayout.Width(120f)))
            {
                DaPlacementRuntimeResult result = RunPlacementRulesForSegment(segment, true);
                _statusText = result != null ? BuildPlacementRuntimeStatus(result) : Text("NoPlacementRules", "ui");
                RefreshRuntimeSnapshotAfterMutation(false);
            }

            if (GUILayout.Button(Text("ClearPlacementRuntime", "ui"), GUILayout.Width(120f)))
            {
                int removed = DaPlacementRuntimeService.ClearSegmentRuntimeObjects(segment);
                _statusText = Text("ClearedPlacementRuntime", "ui") + ": " + removed;
                RefreshRuntimeSnapshotAfterMutation(false);
            }

            if (GUILayout.Button(Text("AddDefaultSubArea", "ui"), GUILayout.Width(135f)))
            {
                AddDefaultSubArea(segment);
            }

            GUILayout.EndHorizontal();

            DrawSubAreas(segment);
            DrawPlacementRules(segment);
            GUILayout.EndVertical();
        }

        private void DrawSubAreas(DaSegmentData segment)
        {
            if (segment.SubAreas == null || segment.SubAreas.Count == 0)
            {
                GUILayout.Label(Text("NoSubAreas", "ui"));
                return;
            }

            GUILayout.Label(Text("PlacementRegions", "ui"));
            for (int index = 0; index < segment.SubAreas.Count; index++)
            {
                DaSubAreaData area = segment.SubAreas[index];
                if (area == null)
                {
                    continue;
                }

                GUILayout.BeginVertical(GUI.skin.box);
                GUILayout.BeginHorizontal();
                area.Enabled = GUILayout.Toggle(area.Enabled, string.Empty, GUILayout.Width(22f));
                area.DisplayName = GUILayout.TextField(area.DisplayName ?? string.Empty, GUILayout.Width(180f));
                GUILayout.Label(Text("RegistryId", "ui") + ": " + (area.Id ?? string.Empty), GUILayout.ExpandWidth(true));
                if (GUILayout.Button(Text("Delete", "ui"), GUILayout.Width(58f)))
                {
                    RemoveSubArea(segment, area);
                    GUILayout.EndHorizontal();
                    GUILayout.EndVertical();
                    break;
                }

                GUILayout.EndHorizontal();
                GUILayout.Label(Text("Shape", "ui") + ": " + Text(area.Shape.ToString(), "ui") + "    " + Text("CenterOffset", "ui") + ": " + FormatVector(area.CenterOffset) + "    " + Text("Size", "ui") + ": " + FormatVector(area.Size));
                GUILayout.EndVertical();
            }
        }

        private void DrawPlacementRules(DaSegmentData segment)
        {
            if (segment.PlacementRules == null || segment.PlacementRules.Count == 0)
            {
                GUILayout.Label(Text("NoPlacementRules", "ui"));
                return;
            }

            GUILayout.Label(Text("PlacementRules", "ui"));
            for (int index = 0; index < segment.PlacementRules.Count; index++)
            {
                DaPlacementRuleData rule = segment.PlacementRules[index];
                if (rule == null)
                {
                    continue;
                }

                DrawPlacementRuleRow(segment, rule);
            }
        }

        private void DrawPlacementRuleRow(DaSegmentData segment, DaPlacementRuleData rule)
        {
            EnsurePlacementRuleEditText(rule);
            if (string.IsNullOrWhiteSpace(rule.TargetSubAreaId))
            {
                rule.TargetSubAreaId = GetDefaultSubAreaId(segment);
            }

            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.BeginHorizontal();
            rule.Enabled = GUILayout.Toggle(rule.Enabled, string.Empty, GUILayout.Width(22f));
            rule.DisplayName = GUILayout.TextField(rule.DisplayName ?? string.Empty, GUILayout.ExpandWidth(true));
            if (GUILayout.Button(Text("Delete", "ui"), GUILayout.Width(58f)))
            {
                segment.PlacementRules.Remove(rule);
                segment.MarkPlacementRulesSpecified();
                _statusText = Text("PlacementRuleDeleted", "ui") + ": " + (rule.DisplayName ?? rule.RegistryDisplayName ?? string.Empty);
                GUILayout.EndHorizontal();
                GUILayout.EndVertical();
                return;
            }

            GUILayout.EndHorizontal();
            GUILayout.Label("template: " + (rule.RegistryId ?? string.Empty));

            GUILayout.BeginHorizontal();
            GUILayout.Label(Text("TargetSubArea", "ui"), GUILayout.Width(92f));
            DrawSubAreaSelector(segment, rule);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label(Text("Count", "ui"), GUILayout.Width(42f));
            string countControl = "da_rule_count_" + rule.GetHashCode();
            GUI.SetNextControlName(countControl);
            string nextCount = GUILayout.TextField(rule.CountEditText ?? string.Empty, GUILayout.Width(52f));
            rule.CountEditText = nextCount;
            if (GUI.GetNameOfFocusedControl() == countControl)
            {
                TrackFocusedPlacementField("Count", rule.CountEditText);
            }

            GUILayout.Label(Text("MinScale", "ui"), GUILayout.Width(58f));
            string minScaleControl = "da_rule_minscale_" + rule.GetHashCode();
            GUI.SetNextControlName(minScaleControl);
            string nextMinScale = GUILayout.TextField(rule.MinScaleEditText ?? string.Empty, GUILayout.Width(54f));
            rule.MinScaleEditText = nextMinScale;
            if (GUI.GetNameOfFocusedControl() == minScaleControl)
            {
                TrackFocusedPlacementField("MinScale", rule.MinScaleEditText);
            }

            GUILayout.Label(Text("MaxScale", "ui"), GUILayout.Width(58f));
            string maxScaleControl = "da_rule_maxscale_" + rule.GetHashCode();
            GUI.SetNextControlName(maxScaleControl);
            string nextMaxScale = GUILayout.TextField(rule.MaxScaleEditText ?? string.Empty, GUILayout.Width(54f));
            rule.MaxScaleEditText = nextMaxScale;
            if (GUI.GetNameOfFocusedControl() == maxScaleControl)
            {
                TrackFocusedPlacementField("MaxScale", rule.MaxScaleEditText);
            }

            if (GUILayout.Button(Text("Apply", "ui"), GUILayout.Width(64f)))
            {
                TrackFocusedPlacementField("Count", rule.CountEditText);
                ApplyPlacementRuleEdits(rule);
            }

            GUILayout.EndHorizontal();
            GUILayout.Label(Text("PlacementMode", "ui") + ": " + rule.PlacementMode + "    " +
                Text("RotationMode", "ui") + ": " + rule.RotationMode + "    " +
                Text("OwnershipMode", "ui") + ": " + rule.OwnershipMode);
            GUILayout.EndVertical();
        }

        private void DrawSubAreaSelector(DaSegmentData segment, DaPlacementRuleData rule)
        {
            if (segment == null || rule == null || segment.SubAreas == null || segment.SubAreas.Count == 0)
            {
                GUILayout.Label(Text("NoSubAreas", "ui"), GUILayout.Width(145f));
                return;
            }

            int currentIndex = FindSubAreaIndex(segment, rule.TargetSubAreaId);
            if (currentIndex < 0)
            {
                currentIndex = 0;
                rule.TargetSubAreaId = segment.SubAreas[0] != null ? segment.SubAreas[0].Id : string.Empty;
                segment.MarkPlacementRulesSpecified();
            }

            DaSubAreaData current = segment.SubAreas[currentIndex];
            string label = current != null ? (current.DisplayName ?? current.Id ?? string.Empty) : string.Empty;
            if (GUILayout.Button("<", GUILayout.Width(24f)))
            {
                SetRuleSubAreaByIndex(segment, rule, currentIndex - 1);
                TrackFocusedPlacementField("TargetSubArea", rule.TargetSubAreaId);
            }

            GUILayout.Label(label, GUILayout.Width(112f));
            if (GUILayout.Button(">", GUILayout.Width(24f)))
            {
                SetRuleSubAreaByIndex(segment, rule, currentIndex + 1);
                TrackFocusedPlacementField("TargetSubArea", rule.TargetSubAreaId);
            }
        }

        private static int FindSubAreaIndex(DaSegmentData segment, string id)
        {
            if (segment == null || segment.SubAreas == null || string.IsNullOrWhiteSpace(id))
            {
                return -1;
            }

            for (int index = 0; index < segment.SubAreas.Count; index++)
            {
                DaSubAreaData area = segment.SubAreas[index];
                if (area != null && string.Equals(area.Id, id, StringComparison.Ordinal))
                {
                    return index;
                }
            }

            return -1;
        }

        private static void SetRuleSubAreaByIndex(DaSegmentData segment, DaPlacementRuleData rule, int index)
        {
            if (segment == null || rule == null || segment.SubAreas == null || segment.SubAreas.Count == 0)
            {
                return;
            }

            int nextIndex = index;
            if (nextIndex < 0)
            {
                nextIndex = segment.SubAreas.Count - 1;
            }
            else if (nextIndex >= segment.SubAreas.Count)
            {
                nextIndex = 0;
            }

            DaSubAreaData area = segment.SubAreas[nextIndex];
            rule.TargetSubAreaId = area != null ? area.Id : string.Empty;
            segment.MarkPlacementRulesSpecified();
        }

        private void AddPlacementRuleFromTemplate(DaSegmentData segment, DaObjectRegistryTemplate template)
        {
            if (segment == null || template == null)
            {
                return;
            }

            EnsurePlacementLists(segment);
            if (segment.SubAreas.Count == 0)
            {
                AddDefaultSubArea(segment);
            }

            string displayName = template.DisplayName ?? template.Name ?? template.RegistryId ?? Text("Unknown", "ui");
            DaPlacementRuleData rule = new DaPlacementRuleData
            {
                Id = CreateUniquePlacementRuleId(segment),
                DisplayName = displayName,
                Enabled = true,
                RegistryId = template.RegistryId,
                RegistryDisplayName = displayName,
                TargetSubAreaId = GetDefaultSubAreaId(segment),
                Count = 1,
                MinScale = 1f,
                MaxScale = 1f,
                PlacementMode = DaPlacementMode.SurfaceRaycast,
                RotationMode = DaPlacementRotationMode.RandomYaw,
                OwnershipMode = DaPlacementOwnershipMode.DreamyAscentRuntime,
                LocalOffset = DaVector3Data.Zero()
            };

            segment.PlacementRules.Add(rule);
            segment.MarkPlacementRulesSpecified();
            _statusText = Text("PlacementRuleAdded", "ui") + ": " + displayName;
            DaLog.Info("Placement rule added from registry. segment=" + segment.SegmentName + ", rule=" + rule.Id + ", registryId=" + rule.RegistryId + ", targetSubArea=" + rule.TargetSubAreaId);
        }

        private void AddDefaultSubArea(DaSegmentData segment)
        {
            if (segment == null)
            {
                return;
            }

            EnsurePlacementLists(segment);
            int ordinal = segment.SubAreas.Count + 1;
            DaSubAreaData area = new DaSubAreaData
            {
                Id = CreateUniqueSubAreaId(segment),
                DisplayName = Text("DefaultSubArea", "ui") + " " + ordinal,
                Shape = DaSubAreaShape.SegmentBounds,
                CenterOffset = DaVector3Data.Zero(),
                Size = DaVector3Data.AreaSize(),
                Enabled = true
            };

            segment.SubAreas.Add(area);
            segment.MarkSubAreasSpecified();
            _statusText = Text("SubAreaAdded", "ui") + ": " + area.DisplayName;
        }

        private void RemoveSubArea(DaSegmentData segment, DaSubAreaData area)
        {
            if (segment == null || area == null || segment.SubAreas == null)
            {
                return;
            }

            string removedId = area.Id ?? string.Empty;
            string displayName = area.DisplayName ?? removedId;
            segment.SubAreas.Remove(area);
            segment.MarkSubAreasSpecified();
            string fallbackId = GetDefaultSubAreaId(segment);
            for (int index = 0; segment.PlacementRules != null && index < segment.PlacementRules.Count; index++)
            {
                DaPlacementRuleData rule = segment.PlacementRules[index];
                if (rule != null && string.Equals(rule.TargetSubAreaId, removedId, StringComparison.Ordinal))
                {
                    rule.TargetSubAreaId = fallbackId;
                    segment.MarkPlacementRulesSpecified();
                }
            }

            _statusText = Text("SubAreaDeleted", "ui") + ": " + displayName;
        }

        private void ApplyPlacementRuleEdits(DaPlacementRuleData rule)
        {
            if (rule == null)
            {
                return;
            }

            if (!int.TryParse(rule.CountEditText ?? string.Empty, NumberStyles.Integer, CultureInfo.InvariantCulture, out int count) ||
                !float.TryParse(rule.MinScaleEditText ?? string.Empty, NumberStyles.Float, CultureInfo.InvariantCulture, out float minScale) ||
                !float.TryParse(rule.MaxScaleEditText ?? string.Empty, NumberStyles.Float, CultureInfo.InvariantCulture, out float maxScale))
            {
                _statusText = Text("PlacementRuleInvalid", "ui") + ": " + (rule.DisplayName ?? rule.RegistryDisplayName ?? string.Empty);
                return;
            }

            int clampedCount = Mathf.Clamp(count, 0, DaPlacementRuntimeService.MaxRuleCount);
            bool countClamped = clampedCount != count;
            rule.Count = clampedCount;
            rule.MinScale = Mathf.Max(0.01f, minScale);
            rule.MaxScale = Mathf.Max(rule.MinScale, maxScale);
            rule.CountEditText = rule.Count.ToString(CultureInfo.InvariantCulture);
            rule.MinScaleEditText = rule.MinScale.ToString("0.###", CultureInfo.InvariantCulture);
            rule.MaxScaleEditText = rule.MaxScale.ToString("0.###", CultureInfo.InvariantCulture);
            TrackFocusedPlacementField("Count", rule.CountEditText);
            MarkSegmentPlacementRulesSpecified(rule);
            _statusText = Text("PlacementRuleUpdated", "ui") + ": " + (rule.DisplayName ?? rule.RegistryDisplayName ?? string.Empty);
            if (countClamped)
            {
                _statusText += "  " + Text("PlacementRuleCountClamped", "ui") + ": " + DaPlacementRuntimeService.MaxRuleCount.ToString(CultureInfo.InvariantCulture);
            }
        }

        private static void MarkSegmentPlacementRulesSpecified(DaPlacementRuleData rule)
        {
            DaTerrainData data = DaTerrainExportService.LastExportedTerrain;
            if (data == null || data.Map == null || data.Map.Segments == null)
            {
                return;
            }

            for (int segmentIndex = 0; segmentIndex < data.Map.Segments.Count; segmentIndex++)
            {
                DaSegmentData segment = data.Map.Segments[segmentIndex];
                for (int ruleIndex = 0; segment != null && segment.PlacementRules != null && ruleIndex < segment.PlacementRules.Count; ruleIndex++)
                {
                    if (object.ReferenceEquals(segment.PlacementRules[ruleIndex], rule))
                    {
                        segment.MarkPlacementRulesSpecified();
                        return;
                    }
                }
            }
        }

        private static void EnsurePlacementLists(DaSegmentData segment)
        {
            if (segment == null)
            {
                return;
            }

            if (segment.SubAreas == null)
            {
                segment.SubAreas = new System.Collections.Generic.List<DaSubAreaData>();
            }

            if (segment.PlacementRules == null)
            {
                segment.PlacementRules = new System.Collections.Generic.List<DaPlacementRuleData>();
            }
        }

        private static void EnsurePlacementRuleEditText(DaPlacementRuleData rule)
        {
            if (rule == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(rule.CountEditText))
            {
                rule.CountEditText = rule.Count.ToString(CultureInfo.InvariantCulture);
            }

            if (string.IsNullOrWhiteSpace(rule.MinScaleEditText))
            {
                rule.MinScaleEditText = rule.MinScale.ToString("0.###", CultureInfo.InvariantCulture);
            }

            if (string.IsNullOrWhiteSpace(rule.MaxScaleEditText))
            {
                rule.MaxScaleEditText = rule.MaxScale.ToString("0.###", CultureInfo.InvariantCulture);
            }
        }

        private static string CreateUniqueSubAreaId(DaSegmentData segment)
        {
            for (int index = 1; index < 10000; index++)
            {
                string id = "area-" + index.ToString(CultureInfo.InvariantCulture);
                if (!SubAreaIdExists(segment, id))
                {
                    return id;
                }
            }

            return "area-" + Guid.NewGuid().ToString("N");
        }

        private static string CreateUniquePlacementRuleId(DaSegmentData segment)
        {
            for (int index = 1; index < 10000; index++)
            {
                string id = "rule-" + index.ToString(CultureInfo.InvariantCulture);
                if (!PlacementRuleIdExists(segment, id))
                {
                    return id;
                }
            }

            return "rule-" + Guid.NewGuid().ToString("N");
        }

        private static bool SubAreaIdExists(DaSegmentData segment, string id)
        {
            for (int index = 0; segment != null && segment.SubAreas != null && index < segment.SubAreas.Count; index++)
            {
                DaSubAreaData area = segment.SubAreas[index];
                if (area != null && string.Equals(area.Id, id, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool PlacementRuleIdExists(DaSegmentData segment, string id)
        {
            for (int index = 0; segment != null && segment.PlacementRules != null && index < segment.PlacementRules.Count; index++)
            {
                DaPlacementRuleData rule = segment.PlacementRules[index];
                if (rule != null && string.Equals(rule.Id, id, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static string GetDefaultSubAreaId(DaSegmentData segment)
        {
            if (segment == null || segment.SubAreas == null || segment.SubAreas.Count == 0 || segment.SubAreas[0] == null)
            {
                return string.Empty;
            }

            return segment.SubAreas[0].Id ?? string.Empty;
        }

        private static string FormatVector(DaVector3Data value)
        {
            if (value == null)
            {
                return "0,0,0";
            }

            return string.Format(CultureInfo.InvariantCulture, "{0:0.###},{1:0.###},{2:0.###}", value.X, value.Y, value.Z);
        }

        private static bool DrawFoldoutHeader(bool expanded, string label)
        {
            GUILayout.BeginHorizontal();
            string marker = expanded ? "[-] " : "[+] ";
            bool next = expanded;
            if (GUILayout.Button(marker + label, GUILayout.Height(24f)))
            {
                next = !expanded;
            }

            GUILayout.EndHorizontal();
            return next;
        }

        private static DaCatalogSegment FindCatalogSegment(DaObjectCatalog catalog, string segmentName)
        {
            if (catalog == null || catalog.Segments == null)
            {
                return null;
            }

            string name = segmentName ?? string.Empty;
            for (int index = 0; index < catalog.Segments.Count; index++)
            {
                DaCatalogSegment segment = catalog.Segments[index];
                if (segment != null && string.Equals(segment.Name, name, StringComparison.Ordinal))
                {
                    return segment;
                }
            }

            return null;
        }

        private void DrawCatalogItemsSection(DaObjectCatalog catalog, DaCatalogSegment segment)
        {
            int count = segment != null && segment.ItemIds != null ? segment.ItemIds.Count : 0;
            _showRuntimeCatalogItems = DrawFoldoutHeader(_showRuntimeCatalogItems, Text("CatalogItems", "ui") + ": " + count);
            if (_showRuntimeCatalogItems)
            {
                DrawCatalogItems(catalog, segment);
            }
        }

        private void DrawCatalogMaterialsSection(DaObjectCatalog catalog, DaCatalogSegment segment)
        {
            int count = segment != null && segment.MaterialIds != null ? segment.MaterialIds.Count : 0;
            _showRuntimeCatalogMaterials = DrawFoldoutHeader(_showRuntimeCatalogMaterials, Text("CatalogMaterials", "ui") + ": " + count);
            if (_showRuntimeCatalogMaterials)
            {
                DrawCatalogMaterials(catalog, segment);
            }
        }

        private void DrawCatalogItems(DaObjectCatalog catalog, DaCatalogSegment segment)
        {
            if (catalog == null || segment == null || segment.ItemIds == null || segment.ItemIds.Count == 0)
            {
                return;
            }

            GUILayout.Space(6f);
            GUILayout.Label(Text("CatalogItems", "ui"));
            int drawn = 0;
            for (int index = 0; index < segment.ItemIds.Count && drawn < 36; index++)
            {
                DaCatalogItem item = FindCatalogItem(segment.ItemIds[index]);
                if (item == null)
                {
                    continue;
                }

                DrawCatalogItemRow(item);
                drawn++;
            }

            if (segment.ItemIds.Count > drawn)
            {
                GUILayout.Label("... " + (segment.ItemIds.Count - drawn));
            }
        }

        private DaCatalogItem FindCatalogItem(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return null;
            }

            DaCatalogItem item;
            return _cachedCatalogItemsById.TryGetValue(id, out item) ? item : null;
        }

        private void DrawCatalogItemRow(DaCatalogItem item)
        {
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.BeginHorizontal();
            GUILayout.Label(item.DisplayName ?? item.Name ?? string.Empty, GUILayout.Width(155f));
            GUILayout.Label(Text(item.Role, "catalog"), GUILayout.Width(110f));
            GUILayout.Label(BuildCatalogItemFlags(item), GUILayout.ExpandWidth(true));
            GUILayout.EndHorizontal();

            GUILayout.Label(BuildCatalogDefaultsText(item.Defaults));
            if (item.Source != null)
            {
                GUILayout.Label(Text("Source", "ui") + ": " + Text(item.Source.Grouper, "grouper") + " / " + Text(item.Source.Step, "step") + " / " + item.Source.Field);
            }

            GUILayout.EndVertical();
        }

        private string BuildCatalogItemFlags(DaCatalogItem item)
        {
            System.Collections.Generic.List<string> flags = new System.Collections.Generic.List<string>();
            if (item.HasChildGeneration)
            {
                flags.Add(Text("CatalogChild", "ui") + "=" + item.ChildLevelGenStepCount);
            }

            if (item.HasSingleItemSpawner)
            {
                flags.Add(Text("CatalogSingleItemSpawner", "ui"));
            }

            if (item.HasPhotonView)
            {
                flags.Add(Text("PhotonView", "ui"));
            }

            return string.Join("  ", flags.ToArray());
        }

        private string BuildCatalogDefaultsText(DaCatalogDefaults defaults)
        {
            if (defaults == null || defaults.Properties == null || defaults.Properties.Count == 0)
            {
                return Text("CatalogDefaults", "ui") + ": -";
            }

            string[] preferred = { "nrOfSpawns", "minSpawnCount", "minMaxSpawn", "scaleMinMax", "area", "chanceToUseSpawner", "overallSpawnChance" };
            System.Collections.Generic.List<string> parts = new System.Collections.Generic.List<string>();
            for (int index = 0; index < preferred.Length; index++)
            {
                string key = preferred[index];
                if (defaults.Properties.TryGetValue(key, out string value))
                {
                    parts.Add(Text(key, "property") + "=" + value);
                }
            }

            return Text("CatalogDefaults", "ui") + ": " + (parts.Count == 0 ? "-" : string.Join("  ", parts.ToArray()));
        }

        private void DrawCatalogMaterials(DaObjectCatalog catalog, DaCatalogSegment segment)
        {
            if (catalog == null || segment == null || segment.MaterialIds == null || segment.MaterialIds.Count == 0)
            {
                return;
            }

            GUILayout.Space(6f);
            GUILayout.Label(Text("CatalogMaterials", "ui"));
            int drawn = 0;
            for (int index = 0; index < segment.MaterialIds.Count && drawn < 18; index++)
            {
                DaCatalogMaterial material = FindCatalogMaterial(segment.MaterialIds[index]);
                if (material == null)
                {
                    continue;
                }

                DrawCatalogMaterialRow(material);
                drawn++;
            }
        }

        private DaCatalogMaterial FindCatalogMaterial(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return null;
            }

            DaCatalogMaterial material;
            return _cachedCatalogMaterialsById.TryGetValue(id, out material) ? material : null;
        }

        private void DrawCatalogMaterialRow(DaCatalogMaterial material)
        {
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label((material.DisplayName ?? material.Name ?? string.Empty) + "    " + Text(material.Role, "catalog"));
            GUILayout.Label(Text("Shader", "ui") + ": " + (material.Shader ?? string.Empty));
            if (material.Source != null)
            {
                GUILayout.Label(Text("Source", "ui") + ": " + Text(material.Source.Grouper, "grouper") + " / " + Text(material.Source.Step, "step") + " / " + material.Source.Field);
            }

            GUILayout.EndVertical();
        }

        private void DrawConstraintGroup(string title, System.Collections.Generic.List<DaConstraintData> constraints)
        {
            if (constraints == null || constraints.Count == 0)
            {
                return;
            }

            GUILayout.Space(8f);
            GUILayout.Label(title);

            for (int index = 0; index < constraints.Count; index++)
            {
                DaConstraintData constraint = constraints[index];
                if (constraint == null)
                {
                    continue;
                }

                GUILayout.BeginVertical(GUI.skin.box);
                GUILayout.Label(Text(constraint.Type, "constraint"));
                DrawPropertyGroup(Text("Properties", "ui"), null, constraint, constraint.Properties);
                GUILayout.EndVertical();
            }
        }

        private void DrawPropertyGroup(string title, DaLevelGenStepData step, DaConstraintData constraint, System.Collections.Generic.List<DaPropertyData> properties)
        {
            if (properties == null || properties.Count == 0)
            {
                return;
            }

            GUILayout.Space(6f);
            GUILayout.Label(title + ":");

            for (int index = 0; index < properties.Count; index++)
            {
                DaPropertyData property = properties[index];
                if (property == null)
                {
                    continue;
                }

                DrawEditableProperty(step, constraint, property);
            }
        }

        private void DrawEditableProperty(DaLevelGenStepData step, DaConstraintData constraint, DaPropertyData property)
        {
            bool changed = DaRuntimeEditService.IsChanged(property);
            Color oldColor = GUI.color;
            if (changed)
            {
                GUI.color = Color.yellow;
            }

            GUILayout.BeginVertical(GUI.skin.box);
            string label = (changed ? "* " : string.Empty) + GetPropertyDisplayLabel(property);
            if (GUILayout.Button(label, GUI.skin.label, GUILayout.ExpandWidth(true)))
            {
                TrackFocusedProperty(property, GetPropertyCurrentDisplayValue(property));
            }
            GUI.color = oldColor;

            if (DaRuntimeEditService.TryGetVectorParts(property.Value, out string[] parts))
            {
                DrawVectorPropertyEditor(step, constraint, property, parts);
            }
            else
            {
                DrawScalarPropertyEditor(step, constraint, property);
            }

            GUILayout.EndVertical();
        }

        private void DrawScalarPropertyEditor(DaLevelGenStepData step, DaConstraintData constraint, DaPropertyData property)
        {
            if (TryGetBoolValue(property.Value, out bool boolValue))
            {
                DrawBoolPropertyEditor(step, constraint, property, boolValue);
                return;
            }

            if (DrawSpecialPropertyEditor(step, constraint, property))
            {
                return;
            }

            if (IsUnsupportedEditableValue(property.Value))
            {
                DrawReadOnlyProperty(property);
                return;
            }

            if (property.EditText == null)
            {
                property.EditText = DaRuntimeEditService.FormatForEdit(property.Value);
            }

            string controlName = "da_prop_" + property.Name + "_" + property.GetHashCode();
            GUI.SetNextControlName(controlName);
            GUILayout.BeginHorizontal();
            string nextText = GUILayout.TextField(property.EditText, GUILayout.ExpandWidth(true));
            if (!string.Equals(nextText, property.EditText, StringComparison.Ordinal))
            {
                property.EditText = nextText;
                TrackFocusedProperty(property, property.EditText);
            }
            else
            {
                property.EditText = nextText;
            }

            if (GUI.GetNameOfFocusedControl() == controlName)
            {
                TrackFocusedProperty(property, property.EditText);
            }

            DrawApplyResetButtons(step, constraint, property, property.EditText);
            GUILayout.EndHorizontal();
        }

        private bool DrawSpecialPropertyEditor(DaLevelGenStepData step, DaConstraintData constraint, DaPropertyData property)
        {
            if (property == null || !string.Equals(property.Name, "layerType", StringComparison.Ordinal))
            {
                return false;
            }

            string value = DaRuntimeEditService.FormatForEdit(property.Value);
            string controlName = "da_prop_layerType_" + property.GetHashCode();
            GUI.SetNextControlName(controlName);
            GUILayout.BeginHorizontal();
            GUILayout.Label(Text("CurrentValue", "ui") + ": " + FormatLayerTypeValue(value), GUILayout.ExpandWidth(true));
            if (GUI.GetNameOfFocusedControl() == controlName)
            {
                TrackFocusedProperty(property, FormatLayerTypeValue(value));
            }
            DrawApplyResetButtons(step, constraint, property, value);
            GUILayout.EndHorizontal();
            if (GUI.GetNameOfFocusedControl() == controlName)
            {
                TrackFocusedProperty(property, FormatLayerTypeValue(value));
            }
            return true;
        }

        private void DrawReadOnlyProperty(DaPropertyData property)
        {
            GUILayout.Label(Text("ReadOnlyProperty", "ui") + ": " + DaRuntimeEditService.FormatForEdit(property != null ? property.Value : null));
            GUILayout.Label(Text("UnsupportedPropertyValue", "ui"));
        }

        private void DrawBoolPropertyEditor(DaLevelGenStepData step, DaConstraintData constraint, DaPropertyData property, bool value)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(Text("BoolCurrentValue", "ui") + ": " + Text(value ? "EnableValue" : "DisableValue", "ui"), GUILayout.ExpandWidth(true));

            bool oldEnabled = GUI.enabled;
            GUI.enabled = oldEnabled && !value;
            if (GUILayout.Button(Text("EnableValue", "ui"), GUILayout.Width(64f)))
            {
                TrackFocusedProperty(property, Text("EnableValue", "ui"));
                ApplyPropertyValue(step, constraint, property, "true");
            }

            GUI.enabled = oldEnabled && value;
            if (GUILayout.Button(Text("DisableValue", "ui"), GUILayout.Width(64f)))
            {
                TrackFocusedProperty(property, Text("DisableValue", "ui"));
                ApplyPropertyValue(step, constraint, property, "false");
            }

            GUI.enabled = oldEnabled;
            if (GUILayout.Button(Text("Reset", "ui"), GUILayout.Width(54f)))
            {
                string resetValue = DaRuntimeEditService.FormatForEdit(property.InitialValue);
                if (ApplyPropertyValue(step, constraint, property, resetValue))
                {
                    property.EditText = resetValue;
                    property.EditParts = null;
                }
            }

            GUILayout.EndHorizontal();
        }

        private void DrawVectorPropertyEditor(DaLevelGenStepData step, DaConstraintData constraint, DaPropertyData property, string[] parts)
        {
            EnsureVectorEditText(property, parts);
            string[] labels = parts.Length == 2 ? new[] { "X", "Y" } : new[] { "X", "Y", "Z" };
            GUILayout.BeginHorizontal();
            for (int index = 0; index < parts.Length; index++)
            {
                GUILayout.Label(labels[index], GUILayout.Width(14f));
                string nextPart = GUILayout.TextField(property.EditParts[index], GUILayout.Width(54f));
                if (!string.Equals(nextPart, property.EditParts[index], StringComparison.Ordinal))
                {
                    property.EditParts[index] = nextPart;
                    TrackFocusedProperty(property, string.Join(",", property.EditParts));
                }
                else
                {
                    property.EditParts[index] = nextPart;
                }
            }

            GUILayout.FlexibleSpace();
            DrawApplyResetButtons(step, constraint, property, string.Join(",", property.EditParts));
            GUILayout.EndHorizontal();
        }

        private void DrawApplyResetButtons(DaLevelGenStepData step, DaConstraintData constraint, DaPropertyData property, string value)
        {
            if (GUILayout.Button(Text("Apply", "ui"), GUILayout.Width(54f)))
            {
                TrackFocusedProperty(property, value);
                ApplyPropertyValue(step, constraint, property, value);
            }

            if (GUILayout.Button(Text("Reset", "ui"), GUILayout.Width(54f)))
            {
                string resetValue = DaRuntimeEditService.FormatForEdit(property.InitialValue);
                TrackFocusedProperty(property, resetValue);
                if (ApplyPropertyValue(step, constraint, property, resetValue))
                {
                    property.EditText = resetValue;
                    property.EditParts = null;
                }
            }
        }

        private bool ApplyPropertyValue(DaLevelGenStepData step, DaConstraintData constraint, DaPropertyData property, string value)
        {
            bool ok = constraint != null
                ? DaRuntimeEditService.TrySetConstraintProperty(constraint, property, value)
                : DaRuntimeEditService.TrySetStepProperty(step, property, value);

            _statusText = ok
                ? Text("Applied", "ui") + ": " + property.Name
                : Text("ApplyFailed", "ui") + ": " + property.Name;

            if (ok)
            {
                RefreshRuntimeSnapshotAfterMutation(false);
            }

            return ok;
        }

        private void TrackFocusedProperty(DaPropertyData property, string value)
        {
            if (property == null || string.IsNullOrWhiteSpace(property.Name))
            {
                return;
            }

            _focusedPlacementFieldName = string.Empty;
            _focusedPlacementFieldValue = string.Empty;
            _focusedPropertyName = property.Name;
            _focusedPropertyValue = value ?? string.Empty;
        }

        private void TrackFocusedPlacementField(string fieldName, string value)
        {
            if (string.IsNullOrWhiteSpace(fieldName))
            {
                return;
            }

            _focusedPropertyName = string.Empty;
            _focusedPropertyValue = string.Empty;
            _focusedPlacementFieldName = fieldName;
            _focusedPlacementFieldValue = value ?? string.Empty;
        }

        private static string GetPlacementFieldHint(string fieldName)
        {
            if (string.IsNullOrWhiteSpace(fieldName))
            {
                return string.Empty;
            }

            string key = "PlacementHint." + fieldName.Trim();
            string translated = DaLocalization.TranslateOrOriginal(key);
            return string.Equals(translated, key, StringComparison.Ordinal) ? string.Empty : translated;
        }

        private void SyncFocusedParameterWithSelection(DaLevelGenStepData step)
        {
            if (step == null)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(_focusedPlacementFieldName))
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(_focusedPropertyName) && HasPropertyWithName(step, _focusedPropertyName))
            {
                return;
            }

            DaPropertyData firstProperty = FindFirstProperty(step);
            if (firstProperty != null)
            {
                TrackFocusedProperty(firstProperty, GetPropertyCurrentDisplayValue(firstProperty));
            }
        }

        private static bool HasPropertyWithName(DaLevelGenStepData step, string propertyName)
        {
            if (step == null || string.IsNullOrWhiteSpace(propertyName))
            {
                return false;
            }

            if (ContainsProperty(step.Properties, propertyName))
            {
                return true;
            }

            if (ContainsConstraintProperty(step.Modifiers, propertyName) ||
                ContainsConstraintProperty(step.Constraints, propertyName) ||
                ContainsConstraintProperty(step.PostConstraints, propertyName))
            {
                return true;
            }

            return false;
        }

        private static bool ContainsProperty(System.Collections.Generic.List<DaPropertyData> properties, string propertyName)
        {
            if (properties == null)
            {
                return false;
            }

            for (int index = 0; index < properties.Count; index++)
            {
                DaPropertyData property = properties[index];
                if (property != null && string.Equals(property.Name, propertyName, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsConstraintProperty(System.Collections.Generic.List<DaConstraintData> constraints, string propertyName)
        {
            if (constraints == null)
            {
                return false;
            }

            for (int index = 0; index < constraints.Count; index++)
            {
                DaConstraintData constraint = constraints[index];
                if (constraint != null && ContainsProperty(constraint.Properties, propertyName))
                {
                    return true;
                }
            }

            return false;
        }

        private static DaPropertyData FindFirstProperty(DaLevelGenStepData step)
        {
            if (step == null)
            {
                return null;
            }

            DaPropertyData property = FindFirst(step.Properties);
            if (property != null)
            {
                return property;
            }

            property = FindFirstConstraintProperty(step.Modifiers);
            if (property != null)
            {
                return property;
            }

            property = FindFirstConstraintProperty(step.Constraints);
            if (property != null)
            {
                return property;
            }

            return FindFirstConstraintProperty(step.PostConstraints);
        }

        private static DaPropertyData FindFirst(System.Collections.Generic.List<DaPropertyData> properties)
        {
            if (properties == null)
            {
                return null;
            }

            for (int index = 0; index < properties.Count; index++)
            {
                if (properties[index] != null)
                {
                    return properties[index];
                }
            }

            return null;
        }

        private static DaPropertyData FindFirstConstraintProperty(System.Collections.Generic.List<DaConstraintData> constraints)
        {
            if (constraints == null)
            {
                return null;
            }

            for (int index = 0; index < constraints.Count; index++)
            {
                DaConstraintData constraint = constraints[index];
                DaPropertyData property = constraint != null ? FindFirst(constraint.Properties) : null;
                if (property != null)
                {
                    return property;
                }
            }

            return null;
        }

        private static string GetPropertyCurrentDisplayValue(DaPropertyData property)
        {
            if (property == null)
            {
                return string.Empty;
            }

            if (property.EditParts != null && property.EditParts.Length > 0)
            {
                return string.Join(",", property.EditParts);
            }

            if (property.EditText != null)
            {
                return property.EditText;
            }

            return DaRuntimeEditService.FormatForEdit(property.Value);
        }

        private static string GetPropertyDisplayLabel(DaPropertyData property)
        {
            string name = property != null ? property.Name : string.Empty;
            string translated = Text(name, "property");
            if (string.IsNullOrWhiteSpace(name) || string.Equals(translated, name, StringComparison.Ordinal))
            {
                return translated;
            }

            return translated + " (" + name + ")";
        }

        private static string GetPropertyHint(string propertyName)
        {
            if (string.IsNullOrWhiteSpace(propertyName))
            {
                return string.Empty;
            }

            string key = "PropertyHint." + propertyName.Trim();
            string translated = DaLocalization.TranslateOrOriginal(key);
            return string.Equals(translated, key, StringComparison.Ordinal) ? string.Empty : translated;
        }

        private static bool TryGetBoolValue(object value, out bool boolValue)
        {
            boolValue = false;
            if (value is bool direct)
            {
                boolValue = direct;
                return true;
            }

            Newtonsoft.Json.Linq.JValue jsonValue = value as Newtonsoft.Json.Linq.JValue;
            if (jsonValue != null && jsonValue.Value is bool fromJson)
            {
                boolValue = fromJson;
                return true;
            }

            return false;
        }

        private static bool IsUnsupportedEditableValue(object value)
        {
            if (value == null ||
                value is string ||
                value is bool ||
                value is int ||
                value is float ||
                value is double ||
                value is decimal ||
                value is UnityEngine.Vector2 ||
                value is UnityEngine.Vector3 ||
                value is UnityEngine.Vector2Int ||
                value is UnityEngine.Vector3Int)
            {
                return false;
            }

            Newtonsoft.Json.Linq.JValue jsonValue = value as Newtonsoft.Json.Linq.JValue;
            if (jsonValue != null)
            {
                return false;
            }

            Newtonsoft.Json.Linq.JObject jsonObject = value as Newtonsoft.Json.Linq.JObject;
            if (jsonObject != null)
            {
                return !DaRuntimeEditService.TryGetVectorParts(jsonObject, out _);
            }

            return !(value is IConvertible);
        }

        private static string FormatLayerTypeValue(string rawValue)
        {
            string key = null;
            switch (rawValue)
            {
                case "0":
                case "AllPhysical":
                    key = "LayerType.AllPhysical";
                    break;
                case "1":
                case "TerrainMap":
                    key = "LayerType.TerrainMap";
                    break;
                case "2":
                case "Terrain":
                    key = "LayerType.Terrain";
                    break;
                case "3":
                case "Map":
                    key = "LayerType.Map";
                    break;
                case "4":
                case "Default":
                    key = "LayerType.Default";
                    break;
                case "5":
                case "AllPhysicalExceptCharacter":
                    key = "LayerType.AllPhysicalExceptCharacter";
                    break;
                case "6":
                case "CharacterAndDefault":
                    key = "LayerType.CharacterAndDefault";
                    break;
                case "7":
                case "AllPhysicalExceptDefault":
                    key = "LayerType.AllPhysicalExceptDefault";
                    break;
            }

            if (string.IsNullOrWhiteSpace(key))
            {
                return rawValue;
            }

            return Text(key, "ui") + " (" + rawValue + ")";
        }

        private static void EnsureVectorEditText(DaPropertyData property, string[] parts)
        {
            if (property.EditParts != null && property.EditParts.Length == parts.Length)
            {
                return;
            }

            property.EditParts = new string[parts.Length];
            for (int index = 0; index < parts.Length; index++)
            {
                property.EditParts[index] = parts[index];
            }
        }

        private void EnsureCatalogCaches(DaObjectCatalog catalog)
        {
            string catalogKey = catalog != null ? catalog.MapKey ?? string.Empty : string.Empty;
            if (string.Equals(_cachedCatalogKey, catalogKey, StringComparison.Ordinal))
            {
                return;
            }

            _cachedCatalogKey = catalogKey;
            _cachedCatalogItemsById = new System.Collections.Generic.Dictionary<string, DaCatalogItem>(StringComparer.Ordinal);
            _cachedCatalogMaterialsById = new System.Collections.Generic.Dictionary<string, DaCatalogMaterial>(StringComparer.Ordinal);
            _cachedStepSummaries = new System.Collections.Generic.Dictionary<DaLevelGenStepData, CatalogStepSummary>();

            if (catalog == null)
            {
                return;
            }

            if (catalog.Items != null)
            {
                for (int index = 0; index < catalog.Items.Count; index++)
                {
                    DaCatalogItem item = catalog.Items[index];
                    if (item != null && !string.IsNullOrWhiteSpace(item.Id) && !_cachedCatalogItemsById.ContainsKey(item.Id))
                    {
                        _cachedCatalogItemsById[item.Id] = item;
                    }
                }
            }

            if (catalog.Materials != null)
            {
                for (int index = 0; index < catalog.Materials.Count; index++)
                {
                    DaCatalogMaterial material = catalog.Materials[index];
                    if (material != null && !string.IsNullOrWhiteSpace(material.Id) && !_cachedCatalogMaterialsById.ContainsKey(material.Id))
                    {
                        _cachedCatalogMaterialsById[material.Id] = material;
                    }
                }
            }
        }

        private void InvalidateUiCatalogCaches()
        {
            _cachedCatalogKey = string.Empty;
            _cachedCatalogItemsById.Clear();
            _cachedCatalogMaterialsById.Clear();
            _cachedStepSummaries.Clear();
            if (_cachedRuntimeStepChildCounts != null)
            {
                _cachedRuntimeStepChildCounts.Clear();
            }

            if (_cachedRuntimeGrouperChildCounts != null)
            {
                _cachedRuntimeGrouperChildCounts.Clear();
            }

            _cachedSnapshotMatchMapKey = string.Empty;
            _cachedSnapshotMatchSegmentName = string.Empty;
            _cachedSnapshotMatchVariantName = string.Empty;
            _cachedSnapshotMatchGrouperCount = -1;
            _cachedSnapshotMatchStepCount = -1;
            _cachedSnapshotMatch = null;
            _cachedSpecialObjectsBySegment.Clear();
            _cachedSpecialObjectsMapKey = string.Empty;
        }

        private void RefreshSpecialSceneObjects()
        {
            _cachedSpecialObjectsBySegment.Clear();
            _cachedSpecialObjectsMapKey = string.Empty;
            EnsureSpecialSceneObjectCache(DaTerrainExportService.LastExportedTerrain);
        }

        private DaTemplateSnapshotMatch GetCachedSnapshotMatch(DaSegmentData segment)
        {
            string mapKey = DaTerrainExportService.LastExportedTerrain != null &&
                            DaTerrainExportService.LastExportedTerrain.Map != null
                ? DaTerrainExportService.LastExportedTerrain.Map.MapKey ?? string.Empty
                : string.Empty;
            string segmentName = segment != null ? segment.SegmentName ?? string.Empty : string.Empty;
            string variantName = segment != null ? segment.NormalizedVariantName ?? string.Empty : string.Empty;
            int grouperCount = segment != null && segment.Groupers != null ? segment.Groupers.Count : 0;
            int stepCount = CountSegmentSteps(segment);

            if (_cachedSnapshotMatch != null &&
                string.Equals(_cachedSnapshotMatchMapKey, mapKey, StringComparison.Ordinal) &&
                string.Equals(_cachedSnapshotMatchSegmentName, segmentName, StringComparison.Ordinal) &&
                string.Equals(_cachedSnapshotMatchVariantName, variantName, StringComparison.Ordinal) &&
                _cachedSnapshotMatchGrouperCount == grouperCount &&
                _cachedSnapshotMatchStepCount == stepCount)
            {
                return _cachedSnapshotMatch;
            }

            _cachedSnapshotMatch = DaTemplateSnapshotService.MatchSegment(segment);
            _cachedSnapshotMatchMapKey = mapKey;
            _cachedSnapshotMatchSegmentName = segmentName;
            _cachedSnapshotMatchVariantName = variantName;
            _cachedSnapshotMatchGrouperCount = grouperCount;
            _cachedSnapshotMatchStepCount = stepCount;
            return _cachedSnapshotMatch;
        }

        private sealed class CatalogStepSummary
        {
            public CatalogStepSummary(int itemCount, int materialCount)
            {
                ItemCount = itemCount;
                MaterialCount = materialCount;
            }

            public int ItemCount { get; }
            public int MaterialCount { get; }

            public string ToDisplayText(DaCustomiserWindow window)
            {
                if (ItemCount == 0 && MaterialCount == 0)
                {
                    return DaLocalization.Translate("NoCatalogForStep", "ui");
                }

                return DaLocalization.Translate("CatalogItems", "ui") + ": " + ItemCount + "    " + DaLocalization.Translate("CatalogMaterials", "ui") + ": " + MaterialCount;
            }
        }

        private void Rescan()
        {
            if (RefreshRuntimeSnapshotAfterMutation(false))
            {
                _selectedSegmentIndex = 0;
                _selectedGrouperIndex = 0;
                _selectedStepIndex = 0;
                _statusText = Text("RescanDone", "ui");
                DaLog.Info("Manual UI rescan completed.");
            }
            else
            {
                _statusText = Text("RescanFailed", "ui");
                DaLog.Warn("Manual UI rescan failed.");
            }
        }

        private bool RefreshRuntimeSnapshotAfterMutation(bool writeDiagnostics)
        {
            ReleasePreviewSceneIsolationForExport();
            if (!DaTerrainExportService.TryExportCurrent(out DaTerrainData data))
            {
                DaLog.Warn("Runtime snapshot refresh failed after UI mutation.");
                return false;
            }

            if (writeDiagnostics)
            {
                DaDiagnosticService.WriteRuntimeExport(data);
            }

            InvalidateUiCatalogCaches();
            ClampSelection(data);
            if (_preview != null)
            {
                _preview.SetTarget(GetSelectedSegment(data));
            }

            DaLog.Info("Runtime snapshot refreshed after UI mutation.");
            return true;
        }

        private void WriteDiagnostics(DaTerrainData data)
        {
            ReleasePreviewSceneIsolationForExport();
            if (!DaTerrainExportService.TryExportCurrent(out data))
            {
                DaLog.Warn("Cannot write UI diagnostics because no terrain data is available.");
                _statusText = Text("RuntimeMissing", "ui");
                return;
            }

            InvalidateUiCatalogCaches();
            ClampSelection(data);
            DaDiagnosticService.WriteRuntimeExport(data);
            if (_preview != null)
            {
                _preview.SetTarget(GetSelectedSegment(data));
            }

            _statusText = Text("DiagnosticsWritten", "ui");
        }

        private void ReleasePreviewSceneIsolationForExport()
        {
            if (_preview != null)
            {
                _preview.ReleaseSceneIsolationForExport();
            }
        }

        private void ExportCurrent(DaTerrainData data)
        {
            string path = DaImportExportService.ExportCurrent(data);
            if (!string.IsNullOrWhiteSpace(path))
            {
                _statusText = Text("Exported", "ui") + ": " + path;
            }
        }

        private void UseLatestImportFile()
        {
            string[] files = DaImportExportService.GetImportFiles();
            if (files.Length == 0)
            {
                _statusText = Text("NoImportFiles", "ui") + ": " + DaImportExportService.ImportDirectoryPath;
                return;
            }

            _importPath = files[0];
            _statusText = Text("SelectedImport", "ui") + ": " + Path.GetFileName(_importPath);
        }

        private void OpenFileDirectory()
        {
            if (DaImportExportService.TryOpenFileDirectory())
            {
                _statusText = DaImportExportService.FileDirectoryPath;
            }
            else
            {
                _statusText = Text("OpenFileDirectoryFailed", "ui");
            }
        }

        private void ImportAndApply(DaTerrainData current)
        {
            if (current == null)
            {
                _statusText = Text("NoRuntimeDataShort", "ui");
                return;
            }

            if (!DaImportExportService.TryImportFile(_importPath, out DaTerrainData imported))
            {
                _statusText = Text("ImportFailed", "ui");
                return;
            }

            int applied = DaRuntimeEditService.ApplyImportedData(current, imported);
            InvalidateUiCatalogCaches();
                _statusText = Text("ImportedApplied", "ui") + ": " + applied + "  " + Text("GenerateAfterImport", "ui");
                _selectedSpecialObjectIndex = -1;
                _cachedSpecialObjectsBySegment.Clear();
        }

        private static void EnsureRuntimeReferences(DaSegmentData segment, DaPropGrouperData grouper)
        {
            if (segment == null ||
                grouper == null ||
                grouper.SourceObject != null)
            {
                return;
            }

            DaTerrainExportService.TryBindSegmentRuntimeReferences(segment);
        }

        private void ClampSelection(DaTerrainData data)
        {
            _selectedSegmentIndex = Mathf.Clamp(_selectedSegmentIndex, 0, data.Map.Segments.Count - 1);
            DaSegmentData segment = GetSelectedSegment(data);
            int grouperCount = segment != null && segment.Groupers != null ? segment.Groupers.Count : 0;
            _selectedGrouperIndex = grouperCount == 0 ? 0 : Mathf.Clamp(_selectedGrouperIndex, 0, grouperCount - 1);

            DaPropGrouperData grouper = GetSelectedGrouper(segment);
            int stepCount = grouper != null && grouper.Steps != null ? grouper.Steps.Count : 0;
            _selectedStepIndex = stepCount == 0 ? 0 : Mathf.Clamp(_selectedStepIndex, 0, stepCount - 1);

            System.Collections.Generic.List<DaSpecialSceneObjectData> specialItems;
            if (_cachedSpecialObjectsBySegment.TryGetValue(segment, out specialItems) && _selectedSpecialObjectIndex >= specialItems.Count)
            {
                _selectedSpecialObjectIndex = -1;
            }
        }

        private DaSegmentData GetSelectedSegment(DaTerrainData data)
        {
            if (data == null || data.Map == null || data.Map.Segments == null || data.Map.Segments.Count == 0)
            {
                return null;
            }

            return data.Map.Segments[Mathf.Clamp(_selectedSegmentIndex, 0, data.Map.Segments.Count - 1)];
        }

        private DaPropGrouperData GetSelectedGrouper(DaSegmentData segment)
        {
            if (segment == null || segment.Groupers == null || segment.Groupers.Count == 0)
            {
                return null;
            }

            return segment.Groupers[Mathf.Clamp(_selectedGrouperIndex, 0, segment.Groupers.Count - 1)];
        }

        private DaLevelGenStepData GetSelectedStep(DaPropGrouperData grouper)
        {
            if (grouper == null || grouper.Steps == null || grouper.Steps.Count == 0)
            {
                return null;
            }

            return grouper.Steps[Mathf.Clamp(_selectedStepIndex, 0, grouper.Steps.Count - 1)];
        }

        private DaSpecialSceneObjectData GetSelectedSpecialSceneObject(DaSegmentData segment)
        {
            if (_selectedSpecialObjectIndex < 0)
            {
                return null;
            }

            System.Collections.Generic.List<DaSpecialSceneObjectData> items = GetSpecialSceneObjects(segment);
            if (items == null || items.Count == 0)
            {
                return null;
            }

            int index = Mathf.Clamp(_selectedSpecialObjectIndex, 0, items.Count - 1);
            DaSpecialSceneObjectData item = items[index];
            if (item == null || item.SourceObject == null)
            {
                _selectedSpecialObjectIndex = -1;
                return null;
            }

            return item;
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

        private static bool HasChangedProperties(System.Collections.Generic.List<DaPropertyData> properties)
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

        private static bool HasChangedConstraints(System.Collections.Generic.List<DaConstraintData> constraints)
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

        private static string GetMapKey(DaTerrainData data)
        {
            if (data == null || data.Map == null || string.IsNullOrWhiteSpace(data.Map.MapKey))
            {
                return Text("Unknown", "ui");
            }

            return data.Map.MapKey;
        }

        private static string GetShortMapName(DaTerrainData data)
        {
            if (data == null || data.Map == null || data.Map.Segments == null || data.Map.Segments.Count == 0)
            {
                return Text("Unknown", "ui");
            }

            System.Collections.Generic.List<string> names = new System.Collections.Generic.List<string>();
            for (int index = 0; index < data.Map.Segments.Count; index++)
            {
                string name = data.Map.Segments[index] != null ? data.Map.Segments[index].SegmentName : null;
                if (!string.IsNullOrWhiteSpace(name))
                {
                    names.Add(Text(name, "segment"));
                }
            }

            return names.Count == 0 ? GetMapKey(data) : string.Join(" / ", names.ToArray());
        }

        private static string Text(string key, string context)
        {
            return DaLocalization.Translate(key, context);
        }
    }
}


