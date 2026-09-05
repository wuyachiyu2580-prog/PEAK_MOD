using System;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace StateKeeper
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class StateKeeperPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.local.statekeeper";
        public const string PluginName = "StateKeeper";
        public const string PluginVersion = "0.1.0";

        internal static BepInEx.Logging.ManualLogSource Log { get; private set; }
        internal static RunCollector Collector { get; private set; }
        private ConfigEntry<bool> _enabled;
        private ConfigEntry<KeyCode> _favoriteHotkey;
        private ConfigEntry<bool> _debugLogging;
        private ConfigEntry<float> _staminaEventThreshold;
        private Harmony _harmony;

        private void Awake()
        {
            Log = Logger;
            try
            {
                _enabled = Config.Bind("General", "Enabled", true, "Enable PEAK expedition data collection.");
                _favoriteHotkey = Config.Bind("General", "FavoriteHotkey", KeyCode.F8,
                    "Favorite or unfavorite the latest completed run.");
                _debugLogging = Config.Bind("Advanced", "DebugLogging", false,
                    "Write detailed collector diagnostics to the BepInEx log.");
                _staminaEventThreshold = Config.Bind("Advanced", "StaminaEventThreshold", 0.01f,
                    new ConfigDescription(
                        "Record an immediate StaminaChanged event when the absolute change is at least this value. The 5Hz stamina samples are not affected.",
                        new AcceptableValueRange<float>(0f, 1f)));

                var store = new RunStore();
                Collector = gameObject.AddComponent<RunCollector>();
                Collector.Initialize(store);
                Collector.CollectionEnabled = _enabled.Value;
                _harmony = new Harmony(PluginGuid);
                _harmony.PatchAll();
                LogInfo("Loaded. Data path: " + store.RootPath);
            }
            catch (Exception ex)
            {
                LogException("Awake", ex);
            }
        }

        private void Update()
        {
            if (_enabled == null || _favoriteHotkey == null) return;
            if (Collector != null) Collector.CollectionEnabled = _enabled.Value;
            if (_enabled.Value && Input.GetKeyDown(_favoriteHotkey.Value) && Collector != null)
                Collector.ToggleFavoriteLatest();
        }

        internal static void LogInfo(string message)
        {
            if (Log != null) Log.LogInfo(message);
        }

        internal static void LogException(string area, Exception ex)
        {
            if (Log != null) Log.LogError(area + " failed: " + ex);
        }

        internal static bool IsDebugLogging
        {
            get { return Instance != null && Instance._debugLogging != null && Instance._debugLogging.Value; }
        }

        internal static float StaminaEventThreshold
        {
            get
            {
                if (Instance == null || Instance._staminaEventThreshold == null) return 0.01f;
                return Mathf.Max(0f, Instance._staminaEventThreshold.Value);
            }
        }

        private static StateKeeperPlugin Instance { get; set; }

        private void OnEnable()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            try { _harmony?.UnpatchSelf(); } catch { }
            if (Instance == this) Instance = null;
            if (Collector != null) Collector = null;
        }
    }
}
