using System;
using System.Collections.Generic;
using System.Reflection;
using DreamyAscent.Data;
using DreamyAscent.Helpers;
using DreamyAscent.Services;
using UnityEngine;

namespace DreamyAscent.UI
{
    internal sealed class DaMapPreview : MonoBehaviour
    {
        private Camera _camera;
        private RenderTexture _texture;
        private DaSegmentData _segment;
        private Vector3 _orbitTarget;
        private float _yawOffset;
        private float _pitchOffset;
        private float _distanceScale = 1f;
        private float _width;
        private float _height;
        private float _nextPreviewDiagnosticTime;
        private float _nextPreviewSampleTime;
        private Vector3 _previewPosition;
        private float _previewFieldOfView = 24f;
        private readonly Dictionary<GameObject, bool> _activeStateCache = new Dictionary<GameObject, bool>();
        private readonly Dictionary<GameObject, bool> _previewEnvironmentStateCache = new Dictionary<GameObject, bool>();
        private bool _visibilityApplied;
        private bool _previewEnvironmentSuppressionApplied;
        private bool _mainCameraPreviewActive;
        private Vector3 _mainCameraSavedPosition;
        private Quaternion _mainCameraSavedRotation;
        private Rect _mainCameraSavedRect;
        private float _mainCameraSavedFieldOfView;
        private object _mainCameraSavedGodCam;
        private bool _mainCameraSupportInitialized;
        private bool _mainCameraSupportFailed;
        private Camera _mainCamera;
        private Component _mainCameraBridge;
        private Component _mainCameraMovement;
        private FieldInfo _isGodCamField;
        private Component _cameraOverride;
        private MethodInfo _setCameraOverrideMethod;
        private int _dragControlId;
        private Rect _screenPreviewRect;
        private readonly List<Rect> _blockedInputRects = new List<Rect>();
        private bool _screenPreviewActive;
        private bool _previewFogIsolationLogged;
        private bool _previewStormIsolationLogged;
        private bool _previewRenderRequested;
        private Vector3 _lastRenderedPreviewPosition;
        private Vector3 _lastRenderedPreviewLookAt;
        private float _lastRenderedPreviewFieldOfView;
        private float _nextPreviewAutoRefreshTime;
        private const float PreviewAutoRefreshInterval = 0.35f;

        private struct RendererState
        {
            public Renderer Renderer;
            public bool Enabled;
        }

        private struct PreviewPose
        {
            public Vector3 Position;
            public Vector3 LookAt;

            public PreviewPose(Vector3 position, Vector3 lookAt)
            {
                Position = position;
                LookAt = lookAt;
            }
        }

        private static readonly Dictionary<string, PreviewPose> DefaultPreviewPoses = new Dictionary<string, PreviewPose>(StringComparer.OrdinalIgnoreCase)
        {
            ["Beach_Segment"] = new PreviewPose(
                new Vector3(29.7978268f, 827.851f, -1220.65076f),
                new Vector3(-25.1340256f, 76.54797f, -72.91404f)),
            ["Roots Segment"] = new PreviewPose(
                new Vector3(-11.6731215f, 1580.804f, -552.539551f),
                new Vector3(-16.474968f, -529.561951f, 1030.072f)),
            ["Jungle_Segment"] = new PreviewPose(
                new Vector3(31.7416286f, 699.861938f, -987.0286f),
                new Vector3(40.13162f, 284.3871f, 828.957153f)),
            ["Snow_Segment"] = new PreviewPose(
                new Vector3(59.98482f, 1346.79382f, -714.307f),
                new Vector3(8.546737f, 560.4567f, 1099.04822f)),
            ["Desert_Segment"] = new PreviewPose(
                new Vector3(-12.7842312f, 1939.07227f, -583.3626f),
                new Vector3(-50.3149567f, 251.042862f, 1368.85962f)),
            ["Caldera_Segment"] = new PreviewPose(
                new Vector3(9.093892f, 2312.24268f, 948.1194f),
                new Vector3(-26.0809479f, -1904.221f, 2602.593f)),
            ["Volcano_Segment"] = new PreviewPose(
                new Vector3(10.9909172f, 836.854553f, 2060.24341f),
                new Vector3(-156.909058f, 2070.51f, 1917.73108f))
        };

        private static readonly Vector3[] DefaultPanelOffsets =
        {
            new Vector3(9.9005f, 123.3415f, -427.0678f),
            new Vector3(9.5816f, 360.8353f, 36.5277f),
            new Vector3(-0.7802f, 572.6466f, 552.7788f),
            new Vector3(-0.7802f, 572.6466f, 552.7788f),
            new Vector3(2.2909f, 917.6837f, 1251.495f),
            new Vector3(2.7372f, 862.5374f, 2043.938f)
        };

        private static readonly float[] DefaultPanelYaws = { 180f, 0f, 180f, 180f, 180f, 180f };
        private static readonly float[] DefaultPanelPitches = { 24f, 24f, 24f, 24f, 20f, 18f };

        public Texture Texture => _texture;
        public bool UseMainCameraView => true;

        private void Awake()
        {
            GameObject cameraObject = new GameObject("DreamyAscent_PreviewCamera");
            cameraObject.hideFlags = HideFlags.HideAndDontSave;
            cameraObject.transform.SetParent(transform, false);
            _camera = cameraObject.AddComponent<Camera>();
            _camera.enabled = false;
            _camera.orthographic = false;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(0.68f, 0.74f, 0.76f, 1f);
            _camera.nearClipPlane = 0.05f;
            _camera.farClipPlane = 6000f;
            _camera.depth = 100f;
            _camera.allowHDR = false;
            _camera.allowMSAA = false;
            CopyMainCameraSettings();
        }

        private void OnDestroy()
        {
            ResetPreview();

            if (_texture != null)
            {
                _texture.Release();
                Destroy(_texture);
            }

            if (_camera != null)
            {
                Destroy(_camera.gameObject);
            }
        }

        public void SetTarget(DaSegmentData segment)
        {
            if (_segment == segment)
            {
                return;
            }

            RestoreSegmentVisibility();
            RestoreMainCameraPose();
            _segment = segment;
            _yawOffset = 0f;
            _pitchOffset = 0f;
            _distanceScale = 1f;

            ApplySegmentVisibility(segment);
            ApplyPreviewEnvironmentSuppression();

            if (!TryLoadDefaultPanelPose(segment, out Vector3 position, out Vector3 lookAt))
            {
                BuildFallbackPose(segment, out position, out lookAt);
            }

            if (segment != null && DaPreviewPoseService.TryGetPose(segment.SegmentName, out Vector3 savedPosition, out Vector3 savedLookAt, out float savedFieldOfView))
            {
                position = savedPosition;
                lookAt = savedLookAt;
                _previewFieldOfView = savedFieldOfView;
                DaLog.Info("Loaded saved preview pose for " + segment.SegmentName + ": position=" + position + ", lookAt=" + lookAt + ", fov=" + _previewFieldOfView);
            }
            else
            {
                _previewFieldOfView = 24f;
            }

            _previewPosition = position;
            _orbitTarget = lookAt;
            ApplyPreviewCameraPose(position, lookAt);
            RequestPreviewRender();
            DaLog.Info("Preview target changed: " + (segment != null ? segment.SegmentName : "null") + ", position=" + position + ", lookAt=" + lookAt);
        }

        public void ResetPreview()
        {
            RestoreSegmentVisibility();
            RestorePreviewEnvironmentSuppression();
            RestoreMainCameraPose();
            DisablePreviewCamera();
            _segment = null;
            _screenPreviewActive = false;
            _previewRenderRequested = false;
            _yawOffset = 0f;
            _pitchOffset = 0f;
            _distanceScale = 1f;
            _previewPosition = Vector3.zero;
            _previewFieldOfView = 24f;
            _orbitTarget = Vector3.zero;
            _lastRenderedPreviewPosition = Vector3.positiveInfinity;
            _lastRenderedPreviewLookAt = Vector3.positiveInfinity;
            _lastRenderedPreviewFieldOfView = float.NaN;
            _nextPreviewAutoRefreshTime = 0f;
        }

        public void ReleaseSceneIsolationForExport()
        {
            RestoreSegmentVisibility();
            RestorePreviewEnvironmentSuppression();
            _segment = null;
            _screenPreviewActive = false;
            _previewRenderRequested = false;
        }

        public void Draw(Rect rect)
        {
            if (UseMainCameraView)
            {
                UpdateMainCameraPreview();
                GUI.Box(rect, Text("PreviewMainCameraNote"));
                HandleInput(rect);
                return;
            }

            EnsureTexture(Mathf.Clamp(Mathf.RoundToInt(rect.width), 64, 1440), Mathf.Clamp(Mathf.RoundToInt(rect.height), 64, 900));
            RenderPreview();
            GUI.DrawTexture(rect, _texture, ScaleMode.StretchToFill, false);
            GUI.Box(rect, string.Empty);
            HandleInput(rect);
        }

        private void DrawTexturePreview(Rect previewRect)
        {
            int width = Mathf.Clamp(Mathf.RoundToInt(previewRect.width), 64, 1920);
            int height = Mathf.Clamp(Mathf.RoundToInt(previewRect.height), 64, 1080);
            EnsureTexture(width, height);
            if (_texture != null)
            {
                GUI.DrawTexture(previewRect, _texture, ScaleMode.StretchToFill, false);
            }

            GUI.Box(previewRect, string.Empty);
        }

        public void DrawScreenOverlay(Rect previewRect)
        {
            DrawScreenOverlay(previewRect, (Rect?)null);
        }

        public void DrawScreenOverlay(Rect previewRect, Rect? blockedInputRect)
        {
            _blockedInputRects.Clear();
            if (blockedInputRect.HasValue)
            {
                _blockedInputRects.Add(blockedInputRect.Value);
            }

            DrawScreenOverlayInternal(previewRect);
        }

        public void DrawScreenOverlay(Rect previewRect, Rect[] blockedInputRects)
        {
            _blockedInputRects.Clear();
            if (blockedInputRects != null)
            {
                for (int index = 0; index < blockedInputRects.Length; index++)
                {
                    _blockedInputRects.Add(blockedInputRects[index]);
                }
            }

            DrawScreenOverlayInternal(previewRect);
        }

        private void DrawScreenOverlayInternal(Rect previewRect)
        {
            if (!UseMainCameraView)
            {
                return;
            }

            _screenPreviewRect = previewRect;
            _screenPreviewActive = true;
            DrawTexturePreview(previewRect);
            bool inputBlocked = Event.current != null && IsPreviewInputBlocked(Event.current.mousePosition);
            if (inputBlocked)
            {
                if (Event.current.type == EventType.MouseUp && GUIUtility.hotControl == _dragControlId)
                {
                    GUIUtility.hotControl = 0;
                    _dragControlId = 0;
                }
            }
            else
            {
                HandleInput(previewRect);
            }

            DrawPreviewHelp(previewRect);
        }

        private void LateUpdate()
        {
            if (!UseMainCameraView || _segment == null || !_screenPreviewActive)
            {
                return;
            }

            UpdateMainCameraPreview();
            RenderRequestedPreview();
        }

        private void HandleInput(Rect rect)
        {
            Event current = Event.current;
            if (current == null)
            {
                return;
            }

            int controlId = GUIUtility.GetControlID(FocusType.Passive, rect);
            bool pointerInside = rect.Contains(current.mousePosition);

            if (current.type == EventType.MouseDown && current.button == 0 && pointerInside)
            {
                _dragControlId = controlId;
                GUIUtility.hotControl = controlId;
                current.Use();
                DaLog.ThrottleInfo("preview-mouse-down", "Preview mouse capture started.", 1f);
                return;
            }

            if (current.type == EventType.MouseUp && current.button == 0 && GUIUtility.hotControl == _dragControlId)
            {
                GUIUtility.hotControl = 0;
                _dragControlId = 0;
                current.Use();
                DaLog.ThrottleInfo("preview-mouse-up", "Preview mouse capture ended.", 1f);
                return;
            }

            if (current.type == EventType.MouseDrag && current.button == 0 && GUIUtility.hotControl == _dragControlId)
            {
                _yawOffset += current.delta.x * 0.5f;
                _pitchOffset = Mathf.Clamp(_pitchOffset - current.delta.y * 0.25f, -25f, 25f);
                ApplyOrbitOffsets();
                UpdateMainCameraPreview();
                current.Use();
                DaLog.ThrottleInfo("preview-mouse-drag", "Preview mouse drag applied: yawOffset=" + _yawOffset + ", pitchOffset=" + _pitchOffset, 1f);
            }

            if (current.type == EventType.ScrollWheel && pointerInside)
            {
                ZoomPreview(current.delta.y);
                UpdateMainCameraPreview();
                current.Use();
                DaLog.ThrottleInfo("preview-mouse-wheel", "Preview mouse wheel applied: distanceScale=" + _distanceScale, 1f);
            }
        }

        private static void DrawPreviewHelp(Rect previewRect)
        {
            Rect helpRect = new Rect(previewRect.x + 12f, 12f, 420f, 48f);
            Color oldColor = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, 0.78f);
            GUI.Box(helpRect, Text("PreviewScreenHelp"));
            GUI.color = oldColor;
        }

        private static string Text(string key)
        {
            return DaLocalization.Translate(key, "ui");
        }

        private void UpdateMainCameraPreview()
        {
            if (!UseMainCameraView || _segment == null)
            {
                return;
            }

            if (_previewPosition == Vector3.zero || _orbitTarget == Vector3.zero)
            {
                Vector3 basePosition;
                Vector3 baseLookAt;
                if (!TryLoadDefaultPanelPose(_segment, out basePosition, out baseLookAt))
                {
                    BuildFallbackPose(_segment, out basePosition, out baseLookAt);
                }

                _previewPosition = basePosition;
                _orbitTarget = baseLookAt;
            }

            ApplyKeyboardMovement();
            SaveCurrentPoseIfRequested();
            if (HasPreviewPoseChanged() || Time.unscaledTime >= _nextPreviewAutoRefreshTime)
            {
                RequestPreviewRender();
            }
        }

        private void RenderRequestedPreview()
        {
            if (!_previewRenderRequested)
            {
                return;
            }

            ApplyPreviewCameraView(_previewPosition, _orbitTarget);
            MaybeLogPreviewDiagnostic(_camera, _previewPosition, _orbitTarget, Vector3.Distance(_previewPosition, _orbitTarget));
        }

        private void ApplyOrbitOffsets()
        {
            if (_previewPosition == Vector3.zero || _orbitTarget == Vector3.zero)
            {
                return;
            }

            Vector3 offset = _previewPosition - _orbitTarget;
            if (offset == Vector3.zero)
            {
                offset = new Vector3(0f, 40f, -80f);
            }

            Quaternion orbit = Quaternion.Euler(_pitchOffset, _yawOffset, 0f);
            _previewPosition = _orbitTarget + orbit * offset;
            _yawOffset = 0f;
            _pitchOffset = 0f;
            RequestPreviewRender();
        }

        private void ZoomPreview(float wheelDelta)
        {
            Vector3 offset = _previewPosition - _orbitTarget;
            if (offset == Vector3.zero)
            {
                return;
            }

            float zoomMultiplier = Mathf.Clamp(1f + wheelDelta * 0.06f, 0.45f, 2.4f);
            Vector3 zoomed = offset * zoomMultiplier;
            float distance = Mathf.Clamp(zoomed.magnitude, 8f, 5000f);
            _previewPosition = _orbitTarget + zoomed.normalized * distance;
            _distanceScale = Mathf.Clamp(_distanceScale + wheelDelta * 0.06f, 0.45f, 2.4f);
            RequestPreviewRender();
        }

        private void ApplyKeyboardMovement()
        {
            Vector2 guiMousePosition = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
            if (!_screenPreviewActive || _camera == null || !_screenPreviewRect.Contains(guiMousePosition))
            {
                return;
            }

            if (IsPreviewInputBlocked(guiMousePosition))
            {
                return;
            }

            Vector3 move = Vector3.zero;
            if (Input.GetKey(KeyCode.W))
            {
                move += _camera.transform.forward;
            }

            if (Input.GetKey(KeyCode.S))
            {
                move -= _camera.transform.forward;
            }

            if (Input.GetKey(KeyCode.D))
            {
                move += _camera.transform.right;
            }

            if (Input.GetKey(KeyCode.A))
            {
                move -= _camera.transform.right;
            }

            if (Input.GetKey(KeyCode.Space))
            {
                move += Vector3.up;
            }

            if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
            {
                move -= Vector3.up;
            }

            if (move == Vector3.zero)
            {
                return;
            }

            float speed = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift) ? 120f : 35f;
            Vector3 delta = move.normalized * speed * Time.unscaledDeltaTime;
            _previewPosition += delta;
            _orbitTarget += delta;
            RequestPreviewRender();
            DaLog.ThrottleInfo("preview-keyboard-move", "Preview keyboard movement applied: delta=" + delta, 1f);
        }

        private bool IsPreviewInputBlocked(Vector2 guiMousePosition)
        {
            for (int index = 0; index < _blockedInputRects.Count; index++)
            {
                if (_blockedInputRects[index].Contains(guiMousePosition))
                {
                    return true;
                }
            }

            return false;
        }

        private void SaveCurrentPoseIfRequested()
        {
            if (!Input.GetKeyDown(KeyCode.F6) || _segment == null)
            {
                return;
            }

            DaPreviewPoseService.SavePose(_segment.SegmentName, _previewPosition, _orbitTarget, _previewFieldOfView);
        }

        private void ApplyPreviewCameraView(Vector3 cameraPosition, Vector3 lookAt)
        {
            if (_camera == null || _texture == null)
            {
                return;
            }

            ApplyPreviewCameraPose(cameraPosition, lookAt);
            _camera.targetTexture = _texture;
            RenderCameraWithPreviewEnvironment(_camera, "Preview camera render failed.");
            MaybeLogPreviewSample();
            _previewRenderRequested = false;
        }

        private void ApplyPreviewCameraPose(Vector3 cameraPosition, Vector3 lookAt)
        {
            if (_camera == null)
            {
                return;
            }

            CopyMainCameraSettings();
            _camera.enabled = false;
            _camera.transform.position = cameraPosition;
            _camera.transform.LookAt(lookAt, Vector3.up);
            _camera.fieldOfView = _previewFieldOfView;
            _camera.nearClipPlane = 0.05f;
            _camera.farClipPlane = 6000f;
            _camera.depth = 100f;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(0.68f, 0.74f, 0.76f, 1f);
        }

        private void RequestPreviewRender()
        {
            _previewRenderRequested = true;
        }

        private void ApplyMainCameraView(Vector3 cameraPosition, Vector3 lookAt, bool savePose)
        {
            Camera mainCamera = Camera.main;
            if (mainCamera == null)
            {
                DaLog.OnceWarn("preview-main-camera-missing", "Cannot apply main camera preview because Camera.main is null.");
                return;
            }

            EnsureMainCameraSupport();
            if (savePose && !_mainCameraPreviewActive)
            {
                _mainCameraSavedPosition = mainCamera.transform.position;
                _mainCameraSavedRotation = mainCamera.transform.rotation;
                _mainCameraSavedRect = mainCamera.rect;
                _mainCameraSavedFieldOfView = mainCamera.fieldOfView;
                _mainCameraSavedGodCam = _isGodCamField != null && _mainCameraMovement != null
                    ? _isGodCamField.GetValue(_mainCameraMovement)
                    : null;
                _mainCameraPreviewActive = true;
                DaLog.Info("Preview main camera state saved.");
            }

            mainCamera.transform.position = cameraPosition;
            mainCamera.transform.LookAt(lookAt, Vector3.up);
            mainCamera.fieldOfView = 24f;
            if (_isGodCamField != null && _mainCameraMovement != null)
            {
                _isGodCamField.SetValue(_mainCameraMovement, true);
            }
        }

        private void RestoreMainCameraPose()
        {
            if (!_mainCameraPreviewActive)
            {
                return;
            }

            Camera mainCamera = Camera.main;
            if (mainCamera != null)
            {
                mainCamera.transform.position = _mainCameraSavedPosition;
                mainCamera.transform.rotation = _mainCameraSavedRotation;
                mainCamera.rect = _mainCameraSavedRect;
                mainCamera.fieldOfView = _mainCameraSavedFieldOfView;
            }

            if (_isGodCamField != null && _mainCameraMovement != null)
            {
                _isGodCamField.SetValue(_mainCameraMovement, _mainCameraSavedGodCam ?? false);
            }

            _mainCameraPreviewActive = false;
            DaLog.Info("Preview main camera pose restored.");
        }

        private void ApplyPreviewCameraViewport(Rect screenRect)
        {
            if (_camera == null || Screen.width <= 0 || Screen.height <= 0)
            {
                return;
            }

            float x = Mathf.Clamp01(screenRect.xMin / Screen.width);
            float y = Mathf.Clamp01((Screen.height - screenRect.yMax) / Screen.height);
            float width = Mathf.Clamp01(screenRect.width / Screen.width);
            float height = Mathf.Clamp01(screenRect.height / Screen.height);
            Rect viewport = new Rect(x, y, width, height);
            if (_camera.rect != viewport)
            {
                _camera.rect = viewport;
                DaLog.Info("Preview camera viewport applied: " + viewport);
            }
        }

        private void DisablePreviewCamera()
        {
            if (_camera == null)
            {
                return;
            }

            _camera.enabled = false;
            _camera.targetTexture = null;
        }

        private void EnsureTexture(int width, int height)
        {
            if (_texture != null && Mathf.Approximately(_width, width) && Mathf.Approximately(_height, height))
            {
                return;
            }

            if (_texture != null)
            {
                _texture.Release();
                Destroy(_texture);
            }

            _width = width;
            _height = height;
            _texture = new RenderTexture(width, height, 16, RenderTextureFormat.ARGB32);
            _texture.name = "DreamyAscent_Preview";
            _texture.Create();
            _camera.targetTexture = _texture;
            _camera.aspect = width / (float)height;
            CopyMainCameraSettings();
        }

        private void RenderPreview()
        {
            Camera sourceCamera = Camera.main != null ? Camera.main : _camera;
            if (sourceCamera == null)
            {
                DaLog.OnceWarn("preview-no-camera", "Preview render skipped because no camera was available.");
                return;
            }

            EnsureMainCameraSupport();

            Vector3 basePosition;
            Vector3 baseLookAt;
            if (!TryLoadDefaultPanelPose(_segment, out basePosition, out baseLookAt))
            {
                BuildFallbackPose(_segment, out basePosition, out baseLookAt);
            }

            Vector3 lookAt = _orbitTarget;
            Quaternion orbit = Quaternion.Euler(_pitchOffset, _yawOffset, 0f);
            Vector3 offset = basePosition - baseLookAt;
            if (offset == Vector3.zero)
            {
                offset = new Vector3(0f, 40f, -80f);
            }

            Vector3 rotated = orbit * offset.normalized;
            float distance = offset.magnitude * _distanceScale;
            Vector3 cameraPosition = lookAt + rotated * distance;
            MaybeLogPreviewDiagnostic(sourceCamera, cameraPosition, lookAt, distance);
            if (sourceCamera == Camera.main)
            {
                RenderWithMainCamera(sourceCamera, cameraPosition, lookAt);
            }
            else
            {
                ApplyCameraPose(cameraPosition, lookAt, true);
                RenderCameraWithPreviewEnvironment(_camera, "Preview camera render failed.");
            }

            MaybeLogPreviewSample();
            _lastRenderedPreviewPosition = cameraPosition;
            _lastRenderedPreviewLookAt = lookAt;
            _lastRenderedPreviewFieldOfView = _previewFieldOfView;
            _nextPreviewAutoRefreshTime = Time.unscaledTime + PreviewAutoRefreshInterval;
        }

        private bool HasPreviewPoseChanged()
        {
            return _lastRenderedPreviewPosition != _previewPosition ||
                   _lastRenderedPreviewLookAt != _orbitTarget ||
                   !Mathf.Approximately(_lastRenderedPreviewFieldOfView, _previewFieldOfView);
        }

        private void EnsureMainCameraSupport()
        {
            if (_mainCameraSupportInitialized || _mainCameraSupportFailed)
            {
                return;
            }

            Camera mainCamera = Camera.main;
            if (mainCamera == null)
            {
                return;
            }

            try
            {
                _mainCamera = mainCamera;
                _mainCameraBridge = GetComponentByTypeName(mainCamera.gameObject, "MainCamera");
                _mainCameraMovement = GetComponentByTypeName(mainCamera.gameObject, "MainCameraMovement");

                if (_mainCameraMovement != null)
                {
                    _isGodCamField = _mainCameraMovement.GetType().GetField("isGodCam", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                }

                _cameraOverride = GetOrAddComponentByTypeName(mainCamera.gameObject, "CameraOverride");
                if (_cameraOverride != null)
                {
                    SetMemberValue(_cameraOverride, "fov", mainCamera.fieldOfView);
                }

                if (_mainCameraBridge != null && _cameraOverride != null)
                {
                    _setCameraOverrideMethod = _mainCameraBridge.GetType().GetMethod("SetCameraOverride", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (_setCameraOverrideMethod != null)
                    {
                        _setCameraOverrideMethod.Invoke(_mainCameraBridge, new object[] { _cameraOverride });
                    }
                }

                Type gamefeelType = FindTypeByName("GamefeelHandler");
                if (gamefeelType != null)
                {
                    Component gamefeel = FindFirstSceneComponentByType(gamefeelType);
                    if (gamefeel != null)
                    {
                        Component perlinShake = GetComponentInChildrenByTypeName(gamefeel.gameObject, "PerlinShake");
                        if (perlinShake != null)
                        {
                            perlinShake.gameObject.SetActive(false);
                        }
                    }
                }

                _mainCameraSupportInitialized = true;
                DaLog.Info("Preview main camera support initialized.");
            }
            catch (Exception ex)
            {
                _mainCameraSupportFailed = true;
                DaLog.Error("Failed to initialize main camera support.", ex);
            }
        }

        private void MaybeLogPreviewDiagnostic(Camera sourceCamera, Vector3 cameraPosition, Vector3 lookAt, float distance)
        {
            if (Time.unscaledTime < _nextPreviewDiagnosticTime)
            {
                return;
            }

            _nextPreviewDiagnosticTime = Time.unscaledTime + 5f;
            Bounds bounds;
            bool hasBounds = TryBuildSegmentBounds(_segment, out bounds);
            bool parentActive = _segment != null &&
                _segment.SourceSegment != null &&
                _segment.SourceSegment.segmentParent != null &&
                _segment.SourceSegment.segmentParent.activeSelf;

            DaLog.Info(string.Format(
                "Preview diagnostic: segment={0}, sourceCamera={1}, texture={2}x{3}, cameraPos={4}, lookAt={5}, distance={6:0.0}, cullingMask={7}, clearFlags={8}, renderFog={9}, fogMode={10}, fogDensity={11:0.0000}, parentActive={12}, hasBounds={13}, boundsCenter={14}, boundsSize={15}",
                _segment != null ? _segment.SegmentName : "null",
                sourceCamera != null ? sourceCamera.name : "null",
                _texture != null ? _texture.width : 0,
                _texture != null ? _texture.height : 0,
                cameraPosition,
                lookAt,
                distance,
                sourceCamera != null ? sourceCamera.cullingMask : 0,
                sourceCamera != null ? sourceCamera.clearFlags.ToString() : "null",
                RenderSettings.fog,
                RenderSettings.fogMode,
                RenderSettings.fogDensity,
                parentActive,
                hasBounds,
                hasBounds ? bounds.center : Vector3.zero,
                hasBounds ? bounds.size : Vector3.zero));
        }

        private void MaybeLogPreviewSample()
        {
            if (_texture == null || Time.unscaledTime < _nextPreviewSampleTime)
            {
                return;
            }

            _nextPreviewSampleTime = Time.unscaledTime + 5f;

            RenderTexture previous = RenderTexture.active;
            Texture2D sample = null;
            try
            {
                RenderTexture.active = _texture;
                sample = new Texture2D(1, 1, TextureFormat.RGBA32, false, true);
                sample.ReadPixels(new Rect(Mathf.FloorToInt(_texture.width * 0.5f), Mathf.FloorToInt(_texture.height * 0.5f), 1f, 1f), 0, 0);
                sample.Apply();
                Color color = sample.GetPixel(0, 0);
                DaLog.Info(string.Format(
                    "Preview sample: rgba=({0:0.000},{1:0.000},{2:0.000},{3:0.000}) tex={4}x{5}",
                    color.r,
                    color.g,
                    color.b,
                    color.a,
                    _texture.width,
                    _texture.height));
            }
            catch (Exception ex)
            {
                DaLog.Error("Preview texture sampling failed.", ex);
            }
            finally
            {
                RenderTexture.active = previous;
                if (sample != null)
                {
                    UnityEngine.Object.Destroy(sample);
                }
            }
        }

        private void ApplyCameraPose(Vector3 cameraPosition, Vector3 lookAt, bool dynamic)
        {
            CopyMainCameraSettings();
            _camera.transform.position = cameraPosition;
            _camera.transform.LookAt(lookAt, Vector3.up);
            _camera.fieldOfView = _previewFieldOfView;
        }

        private void RenderWithMainCamera(Camera sourceCamera, Vector3 cameraPosition, Vector3 lookAt)
        {
            if (sourceCamera == null || _texture == null)
            {
                return;
            }

            Vector3 oldPosition = sourceCamera.transform.position;
            Quaternion oldRotation = sourceCamera.transform.rotation;
            RenderTexture oldTargetTexture = sourceCamera.targetTexture;
            CameraClearFlags oldClearFlags = sourceCamera.clearFlags;
            Color oldBackgroundColor = sourceCamera.backgroundColor;
            int oldCullingMask = sourceCamera.cullingMask;
            float oldFieldOfView = sourceCamera.fieldOfView;
            float oldNearClip = sourceCamera.nearClipPlane;
            float oldFarClip = sourceCamera.farClipPlane;
            bool oldOrthographic = sourceCamera.orthographic;
            float oldOrthoSize = sourceCamera.orthographicSize;

            sourceCamera.transform.position = cameraPosition;
            sourceCamera.transform.LookAt(lookAt, Vector3.up);
            sourceCamera.targetTexture = _texture;
            sourceCamera.clearFlags = CameraClearFlags.SolidColor;
            sourceCamera.backgroundColor = new Color(0.68f, 0.74f, 0.76f, 1f);
            sourceCamera.cullingMask = -1;
            sourceCamera.fieldOfView = _previewFieldOfView;
            sourceCamera.nearClipPlane = 0.05f;
            sourceCamera.farClipPlane = 6000f;
            sourceCamera.orthographic = false;
            sourceCamera.orthographicSize = oldOrthoSize;
            if (_isGodCamField != null && _mainCameraMovement != null)
            {
                _isGodCamField.SetValue(_mainCameraMovement, true);
            }

            try
            {
                RenderCameraWithPreviewEnvironment(sourceCamera, "Main camera preview render failed.");
            }
            catch (System.Exception ex)
            {
                DaLog.Error("Main camera preview render failed.", ex);
            }
            finally
            {
                sourceCamera.targetTexture = oldTargetTexture;
                sourceCamera.transform.position = oldPosition;
                sourceCamera.transform.rotation = oldRotation;
                sourceCamera.clearFlags = oldClearFlags;
                sourceCamera.backgroundColor = oldBackgroundColor;
                sourceCamera.cullingMask = oldCullingMask;
                sourceCamera.fieldOfView = oldFieldOfView;
                sourceCamera.nearClipPlane = oldNearClip;
                sourceCamera.farClipPlane = oldFarClip;
                sourceCamera.orthographic = oldOrthographic;
                sourceCamera.orthographicSize = oldOrthoSize;
                if (_isGodCamField != null && _mainCameraMovement != null)
                {
                    _isGodCamField.SetValue(_mainCameraMovement, false);
                }
            }
        }

        private void CopyMainCameraSettings()
        {
            Camera mainCamera = Camera.main;
            if (mainCamera == null || _camera == null || mainCamera == _camera)
            {
                return;
            }

            _camera.cullingMask = mainCamera.cullingMask;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(0.68f, 0.74f, 0.76f, 1f);
            _camera.fieldOfView = _previewFieldOfView;
            _camera.nearClipPlane = 0.05f;
            _camera.farClipPlane = 6000f;
            _camera.orthographicSize = mainCamera.orthographicSize;
            _camera.renderingPath = mainCamera.renderingPath;
            _camera.targetTexture = null;
        }

        private void RenderCameraWithPreviewEnvironment(Camera camera, string failureMessage)
        {
            if (camera == null)
            {
                return;
            }

            bool oldFog = RenderSettings.fog;
            Color oldFogColor = RenderSettings.fogColor;
            float oldFogDensity = RenderSettings.fogDensity;
            FogMode oldFogMode = RenderSettings.fogMode;
            List<RendererState> stormRendererStates = null;

            try
            {
                if (!_previewFogIsolationLogged)
                {
                    _previewFogIsolationLogged = true;
                    DaLog.Info(string.Format(
                        "Preview render isolation enabled: sourceFog={0}, mode={1}, density={2:0.0000}, color={3}",
                        oldFog,
                        oldFogMode,
                        oldFogDensity,
                        oldFogColor));
                }

                RenderSettings.fog = false;
                SuppressStormVisualRenderers(ref stormRendererStates);
                camera.Render();
            }
            catch (Exception ex)
            {
                DaLog.Error(failureMessage, ex);
            }
            finally
            {
                RenderSettings.fog = oldFog;
                RenderSettings.fogColor = oldFogColor;
                RenderSettings.fogDensity = oldFogDensity;
                RenderSettings.fogMode = oldFogMode;
                RestoreRenderers(stormRendererStates);
            }
        }

        private void SuppressStormVisualRenderers(ref List<RendererState> states)
        {
            Type stormVisualType = FindTypeByName("StormVisual");
            if (stormVisualType == null)
            {
                return;
            }

            List<Component> visuals = FindSceneComponentsByType(stormVisualType);
            if (visuals == null || visuals.Count == 0)
            {
                return;
            }

            for (int visualIndex = 0; visualIndex < visuals.Count; visualIndex++)
            {
                Component visual = visuals[visualIndex];
                if (visual == null || visual.gameObject == null || !visual.gameObject.activeInHierarchy)
                {
                    continue;
                }

                Renderer[] renderers = visual.GetComponentsInChildren<Renderer>(true);
                for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
                {
                    Renderer renderer = renderers[rendererIndex];
                    if (renderer == null || !renderer.enabled)
                    {
                        continue;
                    }

                    if (states == null)
                    {
                        states = new List<RendererState>();
                    }

                    states.Add(new RendererState { Renderer = renderer, Enabled = true });
                    renderer.enabled = false;
                }
            }

            if (states != null && states.Count > 0 && !_previewStormIsolationLogged)
            {
                _previewStormIsolationLogged = true;
                DaLog.Info("Preview storm visual isolation enabled: disabled renderers=" + states.Count);
            }
        }

        private static void RestoreRenderers(List<RendererState> states)
        {
            if (states == null)
            {
                return;
            }

            for (int index = 0; index < states.Count; index++)
            {
                RendererState state = states[index];
                if (state.Renderer != null)
                {
                    state.Renderer.enabled = state.Enabled;
                }
            }
        }

        private void ApplySegmentVisibility(DaSegmentData target)
        {
            DaTerrainData terrain = DaTerrainExportService.LastExportedTerrain;
            if (terrain == null || terrain.Map == null || terrain.Map.Segments == null)
            {
                return;
            }

            _activeStateCache.Clear();
            _visibilityApplied = false;

            for (int index = 0; index < terrain.Map.Segments.Count; index++)
            {
                DaSegmentData segment = terrain.Map.Segments[index];
                MapHandler.MapSegment sourceSegment = segment != null ? segment.SourceSegment : null;
                if (sourceSegment == null || sourceSegment.segmentParent == null)
                {
                    continue;
                }

                bool shouldShow = IsSameSegment(segment, target);
                CacheActiveState(sourceSegment.segmentParent);
                sourceSegment.segmentParent.SetActive(shouldShow);

                if (sourceSegment.segmentCampfire != null)
                {
                    CacheActiveState(sourceSegment.segmentCampfire);
                    sourceSegment.segmentCampfire.SetActive(shouldShow);
                }
            }

            _visibilityApplied = true;
            DaLog.Info("Preview segment visibility applied for " + (target != null ? target.SegmentName : "null"));
        }

        private static bool IsSameSegment(DaSegmentData segment, DaSegmentData target)
        {
            if (segment == null || target == null)
            {
                return false;
            }

            if (ReferenceEquals(segment, target))
            {
                return true;
            }

            if (segment.SourceSegment != null && target.SourceSegment != null && ReferenceEquals(segment.SourceSegment, target.SourceSegment))
            {
                return true;
            }

            return segment.LevelSlot == target.LevelSlot &&
                   string.Equals(segment.SegmentName ?? string.Empty, target.SegmentName ?? string.Empty, StringComparison.Ordinal);
        }

        private void ApplyPreviewEnvironmentSuppression()
        {
            if (_previewEnvironmentSuppressionApplied)
            {
                return;
            }

            SuppressPreviewEnvironmentObject("Misc/Post Fog");
            SuppressPreviewEnvironmentObject("Post Fog");
            SuppressPreviewEnvironmentObject("FogSphereSystem");
            _previewEnvironmentSuppressionApplied = true;

            if (_previewEnvironmentStateCache.Count > 0)
            {
                DaLog.Info("Preview environment suppression applied: disabledObjects=" + _previewEnvironmentStateCache.Count);
            }
            else
            {
                DaLog.ThrottleInfo("preview-environment-no-targets", "Preview environment suppression found no active Post Fog or FogSphereSystem objects.", 5f);
            }
        }

        private void SuppressPreviewEnvironmentObject(string objectPath)
        {
            GameObject target = GameObject.Find(objectPath);
            if (target == null || _previewEnvironmentStateCache.ContainsKey(target))
            {
                return;
            }

            _previewEnvironmentStateCache[target] = target.activeSelf;
            target.SetActive(false);
        }

        private void RestorePreviewEnvironmentSuppression()
        {
            if (!_previewEnvironmentSuppressionApplied)
            {
                return;
            }

            foreach (KeyValuePair<GameObject, bool> pair in _previewEnvironmentStateCache)
            {
                if (pair.Key != null)
                {
                    pair.Key.SetActive(pair.Value);
                }
            }

            int count = _previewEnvironmentStateCache.Count;
            _previewEnvironmentStateCache.Clear();
            _previewEnvironmentSuppressionApplied = false;
            DaLog.Info("Preview environment suppression restored: objects=" + count);
        }

        private void RestoreSegmentVisibility()
        {
            if (!_visibilityApplied || _activeStateCache.Count == 0)
            {
                _visibilityApplied = false;
                return;
            }

            foreach (KeyValuePair<GameObject, bool> pair in _activeStateCache)
            {
                if (pair.Key != null)
                {
                    pair.Key.SetActive(pair.Value);
                }
            }

            _activeStateCache.Clear();
            _visibilityApplied = false;
            DaLog.Info("Preview segment visibility restored.");
        }

        private void CacheActiveState(GameObject target)
        {
            if (target == null || _activeStateCache.ContainsKey(target))
            {
                return;
            }

            _activeStateCache[target] = target.activeSelf;
        }

        private static bool TryLoadDefaultPanelPose(DaSegmentData segment, out Vector3 position, out Vector3 lookAt)
        {
            position = Vector3.zero;
            lookAt = Vector3.zero;

            if (segment != null &&
                !string.IsNullOrWhiteSpace(segment.SegmentName) &&
                DefaultPreviewPoses.TryGetValue(segment.SegmentName.Trim(), out PreviewPose pose))
            {
                position = pose.Position;
                lookAt = pose.LookAt;
                return true;
            }

            int index = GetPanelIndex(segment);
            if (index < 0 || index >= DefaultPanelOffsets.Length)
            {
                return false;
            }

            position = DefaultPanelOffsets[index];
            lookAt = ResolveLookAt(segment, index, position);
            return true;
        }

        private static int GetPanelIndex(DaSegmentData segment)
        {
            if (segment == null || string.IsNullOrWhiteSpace(segment.SegmentName))
            {
                return -1;
            }

            string name = segment.SegmentName;
            if (name.StartsWith("Beach", System.StringComparison.OrdinalIgnoreCase))
            {
                return 0;
            }

            if (name.StartsWith("Jungle", System.StringComparison.OrdinalIgnoreCase))
            {
                return 1;
            }

            if (name.StartsWith("Snow", System.StringComparison.OrdinalIgnoreCase) || name.StartsWith("Roots", System.StringComparison.OrdinalIgnoreCase))
            {
                return 2;
            }

            if (name.StartsWith("Desert", System.StringComparison.OrdinalIgnoreCase))
            {
                return 3;
            }

            if (name.StartsWith("Caldera", System.StringComparison.OrdinalIgnoreCase))
            {
                return 4;
            }

            if (name.StartsWith("Volcano", System.StringComparison.OrdinalIgnoreCase))
            {
                return 5;
            }

            return -1;
        }

        private static Vector3 ResolveLookAt(DaSegmentData segment, int index, Vector3 fallback)
        {
            Bounds bounds;
            if (TryBuildSegmentBounds(segment, out bounds))
            {
                return new Vector3(
                    bounds.center.x,
                    bounds.min.y + Mathf.Max(bounds.size.y * 0.28f, 20f),
                    bounds.center.z);
            }

            return fallback + Vector3.forward * 25f;
        }

        private static void BuildFallbackPose(DaSegmentData segment, out Vector3 position, out Vector3 lookAt)
        {
            Bounds bounds;
            if (!TryBuildSegmentBounds(segment, out bounds))
            {
                bounds = new Bounds(Vector3.zero, Vector3.one * 150f);
            }

            lookAt = bounds.center;
            position = lookAt + new Vector3(0f, 55f, -95f);
        }

        private static bool TryBuildSegmentBounds(DaSegmentData segment, out Bounds bounds)
        {
            bounds = new Bounds(Vector3.zero, Vector3.zero);
            if (segment != null && segment.SourceRoots != null && segment.SourceRoots.Count > 0)
            {
                bool hasBounds = false;
                for (int index = 0; index < segment.SourceRoots.Count; index++)
                {
                    Transform root = segment.SourceRoots[index];
                    if (root == null)
                    {
                        continue;
                    }

                    Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
                    for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
                    {
                        Renderer renderer = renderers[rendererIndex];
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
                }

                if (hasBounds && bounds.size != Vector3.zero)
                {
                    return true;
                }
            }

            return false;
        }

        private static Component GetComponentByTypeName(GameObject gameObject, string typeName)
        {
            Type type = FindTypeByName(typeName);
            return gameObject != null && type != null ? gameObject.GetComponent(type) : null;
        }

        private static Component GetComponentInChildrenByTypeName(GameObject gameObject, string typeName)
        {
            Type type = FindTypeByName(typeName);
            return gameObject != null && type != null ? gameObject.GetComponentInChildren(type, true) : null;
        }

        private static Component GetOrAddComponentByTypeName(GameObject gameObject, string typeName)
        {
            Type type = FindTypeByName(typeName);
            if (gameObject == null || type == null)
            {
                return null;
            }

            Component existing = gameObject.GetComponent(type);
            return existing != null ? existing : gameObject.AddComponent(type);
        }

        private static Type FindTypeByName(string typeName)
        {
            if (string.IsNullOrWhiteSpace(typeName))
            {
                return null;
            }

            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int index = 0; index < assemblies.Length; index++)
            {
                Type type = assemblies[index].GetType(typeName, false);
                if (type != null)
                {
                    return type;
                }
            }

            return null;
        }

        private static Component FindFirstSceneComponentByType(Type type)
        {
            List<Component> components = FindSceneComponentsByType(type);
            return components != null && components.Count > 0 ? components[0] : null;
        }

        private static List<Component> FindSceneComponentsByType(Type type)
        {
            if (type == null)
            {
                return null;
            }

            UnityEngine.Object[] objects = Resources.FindObjectsOfTypeAll(type);
            if (objects == null || objects.Length == 0)
            {
                return null;
            }

            List<Component> components = new List<Component>();
            for (int index = 0; index < objects.Length; index++)
            {
                Component component = objects[index] as Component;
                if (component == null || component.gameObject == null)
                {
                    continue;
                }

                if (!component.gameObject.scene.IsValid())
                {
                    continue;
                }

                components.Add(component);
            }

            return components;
        }

        private static void SetMemberValue(object target, string memberName, object value)
        {
            if (target == null || string.IsNullOrWhiteSpace(memberName))
            {
                return;
            }

            Type type = target.GetType();
            FieldInfo field = type.GetField(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field != null)
            {
                field.SetValue(target, value);
                return;
            }

            PropertyInfo property = type.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property != null && property.CanWrite)
            {
                property.SetValue(target, value, null);
            }
        }
    }
}


