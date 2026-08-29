using BepInEx;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace OldPC
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [DefaultExecutionOrder(10000)]
    public sealed class OldPCPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.wuyachiyu.OldPC";
        public const string PluginName = "OldPC";
        public const string PluginVersion = "0.0.1";

        private const string MaxFogKey = "MAXFOG";
        private const string UseFogKey = "USEFOG";
        private const string CloseFogKey = "CloseDistanceMod";

        private ConfigEntry<bool> _enabled;
        private ConfigEntry<KeyCode> _toggleKey;
        private ConfigEntry<float> _telescopeFov;
        private ConfigEntry<float> _retroRenderScale;
        private ConfigEntry<float> _retroShadowDistance;
        private ConfigEntry<float> _retroLodBias;
        private ConfigEntry<int> _retroTextureLimit;
        private ConfigEntry<int> _retroPixelLightCount;
        private ConfigEntry<float> _retroFogDistance;
        private ConfigEntry<float> _retroCloseFog;
        private ConfigEntry<bool> _screenEffects;
        private ConfigEntry<float> _noiseStrength;
        private ConfigEntry<float> _scanlineStrength;

        private bool _telescopeActive;
        private UniversalRenderPipelineAsset _pipelineAsset;
        private bool _pipelineCaptured;
        private float _originalRenderScale;
        private UpscalingFilterSelection _originalUpscalingFilter;
        private float _originalShadowDistance;
        private int _originalShadowCascadeCount;
        private float _originalLodBias;
        private int _originalTextureLimit;
        private int _originalPixelLightCount;
        private AnisotropicFiltering _originalAnisotropicFiltering;
        private Color _originalFogColor;
        private bool _fogColorCaptured;
        private bool _cameraCaptured;
        private float _originalCameraFov;
        private Texture2D _whiteTexture;
        private Texture2D _noiseTexture;
        private Texture2D _scanlineTexture;

        private void Awake()
        {
            _enabled = Config.Bind("General", "Enabled", true, "Enable the OldPC visual mode.");
            _toggleKey = Config.Bind("General", "TelescopeToggleKey", KeyCode.F8, "Toggle the telescope clarity mode.");
            _telescopeFov = Config.Bind("General", "TelescopeFov", 22f, "Field of view used while telescope mode is active.");

            _retroRenderScale = Config.Bind("Retro", "RenderScale", 0.45f, "Render scale used for the retro mode.");
            _retroShadowDistance = Config.Bind("Retro", "ShadowDistance", 80f, "Shadow distance used for the retro mode.");
            _retroLodBias = Config.Bind("Retro", "LodBias", 0.7f, "LOD bias used for the retro mode.");
            _retroTextureLimit = Config.Bind("Retro", "TextureLimit", 1, "Global texture mip limit used for the retro mode.");
            _retroPixelLightCount = Config.Bind("Retro", "PixelLightCount", 1, "Pixel light count used for the retro mode.");
            _retroFogDistance = Config.Bind("Retro", "FogDistance", 110f, "Fog distance used for the retro mode.");
            _retroCloseFog = Config.Bind("Retro", "CloseFog", 4f, "Close fog modifier used for the retro mode.");
            _screenEffects = Config.Bind("Retro", "ScreenEffects", true, "Add the local CRT-style screen overlay.");
            _noiseStrength = Config.Bind("Retro", "NoiseStrength", 0.08f, "CRT noise overlay opacity.");
            _scanlineStrength = Config.Bind("Retro", "ScanlineStrength", 0.06f, "CRT scanline overlay opacity.");

            CapturePipelineDefaults();
            _whiteTexture = BuildSolidTexture(Color.white);
            _noiseTexture = BuildNoiseTexture(128);
            _scanlineTexture = BuildScanlineTexture();

            Logger.LogInfo(PluginName + " v" + PluginVersion + " loaded. Press " + _toggleKey.Value + " to toggle telescope clarity mode.");
        }

        private void Update()
        {
            if (!_enabled.Value)
            {
                return;
            }

            if (Input.GetKeyDown(_toggleKey.Value))
            {
                _telescopeActive = !_telescopeActive;
                Logger.LogInfo(_telescopeActive
                    ? "OldPC telescope clarity mode enabled."
                    : "OldPC telescope clarity mode disabled.");
            }
        }

        private void LateUpdate()
        {
            if (!_enabled.Value)
            {
                RestorePipelineDefaults();
                RestoreCameraDefaults();
                RestoreFogColor();
                return;
            }

            if (!_pipelineCaptured)
            {
                CapturePipelineDefaults();
            }

            if (!_fogColorCaptured)
            {
                _originalFogColor = RenderSettings.fogColor;
                _fogColorCaptured = true;
            }

            if (!_cameraCaptured)
            {
                Camera camera = Camera.main;
                if (camera != null)
                {
                    _originalCameraFov = camera.fieldOfView;
                    _cameraCaptured = true;
                }
            }

            ApplyActivePreset();
        }

        private void OnDisable()
        {
            RestorePipelineDefaults();
            RestoreCameraDefaults();
            RestoreFogColor();

            DestroyTexture(ref _whiteTexture);
            DestroyTexture(ref _noiseTexture);
            DestroyTexture(ref _scanlineTexture);
        }

        private void OnGUI()
        {
            if (!_enabled.Value || _telescopeActive || !_screenEffects.Value || Event.current.type != EventType.Repaint)
            {
                return;
            }

            Color previousColor = GUI.color;
            GUI.depth = 10000;

            GUI.color = new Color(0.08f, 0.14f, 0.10f, 0.045f);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), _whiteTexture, ScaleMode.StretchToFill);

            GUI.color = new Color(1f, 1f, 1f, Mathf.Clamp01(_noiseStrength.Value));
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), _noiseTexture, ScaleMode.StretchToFill);

            GUI.color = new Color(0.35f, 0.5f, 0.4f, Mathf.Clamp01(_scanlineStrength.Value));
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), _scanlineTexture, ScaleMode.StretchToFill);

            GUI.color = previousColor;
        }

        private void OnDestroy()
        {
            RestorePipelineDefaults();
            RestoreCameraDefaults();
            RestoreFogColor();
        }

        private void CapturePipelineDefaults()
        {
            if (_pipelineCaptured)
            {
                return;
            }

            _pipelineAsset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (_pipelineAsset == null)
            {
                return;
            }

            _originalRenderScale = _pipelineAsset.renderScale;
            _originalUpscalingFilter = _pipelineAsset.upscalingFilter;
            _originalShadowDistance = _pipelineAsset.shadowDistance;
            _originalShadowCascadeCount = _pipelineAsset.shadowCascadeCount;

            _originalLodBias = QualitySettings.lodBias;
            _originalTextureLimit = QualitySettings.globalTextureMipmapLimit;
            _originalPixelLightCount = QualitySettings.pixelLightCount;
            _originalAnisotropicFiltering = QualitySettings.anisotropicFiltering;

            _pipelineCaptured = true;
        }

        private void ApplyActivePreset()
        {
            if (_pipelineAsset != null)
            {
                if (_telescopeActive)
                {
                    _pipelineAsset.renderScale = Mathf.Max(0.95f, _originalRenderScale);
                    _pipelineAsset.upscalingFilter = _originalUpscalingFilter;
                    _pipelineAsset.shadowDistance = _originalShadowDistance;
                    _pipelineAsset.shadowCascadeCount = _originalShadowCascadeCount;
                }
                else
                {
                    _pipelineAsset.renderScale = Mathf.Clamp(_retroRenderScale.Value, 0.1f, 1f);
                    _pipelineAsset.upscalingFilter = UpscalingFilterSelection.Linear;
                    _pipelineAsset.shadowDistance = Mathf.Max(0f, _retroShadowDistance.Value);
                    _pipelineAsset.shadowCascadeCount = 1;
                }
            }

            if (_telescopeActive)
            {
                QualitySettings.lodBias = _originalLodBias;
                QualitySettings.globalTextureMipmapLimit = _originalTextureLimit;
                QualitySettings.pixelLightCount = _originalPixelLightCount;
                QualitySettings.anisotropicFiltering = _originalAnisotropicFiltering;
                Shader.SetGlobalFloat(MaxFogKey, 800f);
                Shader.SetGlobalFloat(CloseFogKey, 1f);
                Shader.SetGlobalFloat(UseFogKey, 1f);
                RenderSettings.fogColor = _originalFogColor;

                Camera camera = Camera.main;
                if (camera != null)
                {
                    camera.fieldOfView = Mathf.Clamp(_telescopeFov.Value, 10f, 90f);
                }
            }
            else
            {
                QualitySettings.lodBias = Mathf.Clamp(_retroLodBias.Value, 0.1f, 1f);
                QualitySettings.globalTextureMipmapLimit = Mathf.Max(0, _retroTextureLimit.Value);
                QualitySettings.pixelLightCount = Mathf.Max(0, _retroPixelLightCount.Value);
                QualitySettings.anisotropicFiltering = AnisotropicFiltering.Disable;
                Shader.SetGlobalFloat(MaxFogKey, Mathf.Max(10f, _retroFogDistance.Value));
                Shader.SetGlobalFloat(CloseFogKey, Mathf.Max(1f, _retroCloseFog.Value));
                Shader.SetGlobalFloat(UseFogKey, 1f);
                RenderSettings.fogColor = Color.Lerp(_originalFogColor, new Color(0.12f, 0.12f, 0.12f, _originalFogColor.a), 0.55f);
            }
        }

        private void RestorePipelineDefaults()
        {
            if (!_pipelineCaptured || _pipelineAsset == null)
            {
                return;
            }

            _pipelineAsset.renderScale = _originalRenderScale;
            _pipelineAsset.upscalingFilter = _originalUpscalingFilter;
            _pipelineAsset.shadowDistance = _originalShadowDistance;
            _pipelineAsset.shadowCascadeCount = _originalShadowCascadeCount;
            QualitySettings.lodBias = _originalLodBias;
            QualitySettings.globalTextureMipmapLimit = _originalTextureLimit;
            QualitySettings.pixelLightCount = _originalPixelLightCount;
            QualitySettings.anisotropicFiltering = _originalAnisotropicFiltering;
        }

        private void RestoreCameraDefaults()
        {
            if (!_cameraCaptured)
            {
                return;
            }

            Camera camera = Camera.main;
            if (camera != null)
            {
                camera.fieldOfView = _originalCameraFov;
            }
        }

        private void RestoreFogColor()
        {
            if (_fogColorCaptured)
            {
                RenderSettings.fogColor = _originalFogColor;
            }
        }

        private static Texture2D BuildSolidTexture(Color color)
        {
            Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false, true);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Point;
            texture.SetPixel(0, 0, color);
            texture.Apply(false, true);
            return texture;
        }

        private static Texture2D BuildNoiseTexture(int size)
        {
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false, true);
            texture.wrapMode = TextureWrapMode.Repeat;
            texture.filterMode = FilterMode.Point;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int seed = (x * 37) ^ (y * 73) ^ (x * y * 17);
                    byte value = (byte)(80 + (seed & 63));
                    texture.SetPixel(x, y, new Color32(value, value, value, 255));
                }
            }

            texture.Apply(false, true);
            return texture;
        }

        private static Texture2D BuildScanlineTexture()
        {
            Texture2D texture = new Texture2D(1, 4, TextureFormat.RGBA32, false, true);
            texture.wrapMode = TextureWrapMode.Repeat;
            texture.filterMode = FilterMode.Point;
            texture.SetPixel(0, 0, new Color32(255, 255, 255, 255));
            texture.SetPixel(0, 1, new Color32(255, 255, 255, 0));
            texture.SetPixel(0, 2, new Color32(255, 255, 255, 255));
            texture.SetPixel(0, 3, new Color32(255, 255, 255, 0));
            texture.Apply(false, true);
            return texture;
        }

        private static void DestroyTexture(ref Texture2D texture)
        {
            if (texture != null)
            {
                Destroy(texture);
                texture = null;
            }
        }
    }
}
