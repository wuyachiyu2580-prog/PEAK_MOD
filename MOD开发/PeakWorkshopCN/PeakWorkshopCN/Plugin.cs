using System;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PeakWorkshopCN
{
    [BepInPlugin(Guid, "PeakWorkshopCN", "0.1.0")]
    [BepInDependency(WorkshopGuid, BepInDependency.DependencyFlags.HardDependency)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Guid = "com.wuyachiyu.peakworkshopcn";
        internal const string WorkshopGuid = "com.peakcode.peakworkshop";
        internal static Plugin Instance;
        internal static bool Chinese => Instance != null && Instance._enabled.Value && Instance._ready;
        private ConfigEntry<bool> _enabled;
        private Harmony _harmony;
        private bool _ready;

        private void Awake()
        {
            Instance = this;
            _enabled = Config.Bind("General", "Enabled", true, "启用 Peak Workshop 中文界面补丁；关闭后恢复原有英文或德文显示。");
            try
            {
                var original = Chainloader.PluginInfos[WorkshopGuid];
                if (original.Metadata.Version != new Version(0, 2, 0))
                {
                    Logger.LogWarning("Only Peak Workshop 0.2.0 is supported by this build; localization was not enabled.");
                    return;
                }
                _harmony = new Harmony(Guid);
                WorkshopPatches.Install(_harmony, original.Instance.GetType().Assembly);
                ConfigurationLocalization.Install(_harmony, original.Instance.Config);
                FontPatches.Install(_harmony, original.Instance.GetType().Assembly);
                _ready = true;
                _enabled.SettingChanged += OnEnabledChanged;
                LocalizedText.OnLangugageChanged += Refresh;
                SceneManager.sceneLoaded += OnSceneLoaded;
                Refresh();
                Logger.LogInfo("PeakWorkshopCN 0.1.0: " + WorkshopPatches.PatchedSites +
                    " fixed text sites, " + WorkshopPatches.DynamicSites + " dynamic call sites, " +
                    ConfigurationLocalization.Count + " configuration descriptions. Partial test build.");
            }
            catch (Exception ex)
            {
                _ready = false;
                _harmony?.UnpatchSelf();
                FontPatches.Dispose();
                Logger.LogError("Localization initialization failed: " + ex);
            }
        }

        private void OnEnabledChanged(object sender, EventArgs args) => Refresh();
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Refresh();

        private void Refresh()
        {
            try
            {
                FontPatches.Restore();
                FontPatches.Invalidate();
                FontPatches.RefreshTracked();
                WorkshopPatches.NotifyLanguageChange();
            }
            catch (Exception ex) { Warn("UI refresh: " + ex.Message); }
        }

        internal static void Warn(string message) => Instance?.Logger.LogWarning(message);

        private void OnDestroy()
        {
            _ready = false;
            if (_enabled != null) _enabled.SettingChanged -= OnEnabledChanged;
            LocalizedText.OnLangugageChanged -= Refresh;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            FontPatches.Dispose();
            WorkshopPatches.NotifyLanguageChange();
            _harmony?.UnpatchSelf();
            Instance = null;
        }
    }
}
