using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using System;
using System.IO;
using UnityEngine;

namespace UprightCockroach;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
public sealed class Plugin : BaseUnityPlugin
{
    public const string PluginGuid = "wuyachiyu.peak.uprightcockroach";
    public const string PluginName = "Upright Cockroach";
    public const string PluginVersion = "0.1.0";

    internal static ManualLogSource Log;
    internal static AntennaConfiguration LeftAntenna;
    internal static AntennaConfiguration RightAntenna;
    internal static ConfigEntry<bool> FirstPersonHeadVisible;
    internal static ConfigEntry<bool> FirstPersonShellVisible;
    internal static ConfigEntry<bool> FirstPersonAntennaVisible;
    internal static ConfigEntry<float> FirstPersonHeadScale;
    internal static ConfigEntry<float> FirstPersonShellScale;
    internal static ConfigVector FirstPersonHeadOffset;
    internal static ConfigVector FirstPersonShellOffset;
    internal static int ConfigurationRevision;
    internal static bool IsShuttingDown;
    private Harmony _harmony;
    private DateTime _configWriteTime;
    private float _nextConfigCheck;
    private bool _configReadWarning;

    private void Awake()
    {
        Log = Logger;
        IsShuttingDown = false;
        // Keep the established section names so existing player configs migrate
        // without losing antenna placement when the real model replaces primitives.
        LeftAntenna = new AntennaConfiguration(Config, "Antenna.Left", -1f, true);
        RightAntenna = new AntennaConfiguration(Config, "Antenna.Right", 1f, true);
        FirstPersonHeadVisible = Config.Bind("FirstPerson", "HeadVisible", true,
            "Show a small cockroach head at the edge of the local first-person view / 本地第一人称显示缩小后的蟑螂头部。\nSet false to hide it.");
        FirstPersonShellVisible = Config.Bind("FirstPerson", "WingShellVisible", true,
            "Show wing cases in the local first-person view / 本地第一人称显示翅壳。\nSet false to hide them.");
        FirstPersonAntennaVisible = Config.Bind("FirstPerson", "AntennaVisible", true,
            "Show shortened antennae in the local first-person view / 本地第一人称显示缩短后的触须。\nSet false to hide them.");
        FirstPersonHeadScale = Config.Bind("FirstPerson", "HeadScale", 0.32f,
            new ConfigDescription("First-person head scale / 第一人称头部缩放。", new AcceptableValueRange<float>(0.05f, 1f)));
        FirstPersonShellScale = Config.Bind("FirstPerson", "WingShellScale", 0.9f,
            new ConfigDescription("First-person wing-case scale / 第一人称翅壳缩放。", new AcceptableValueRange<float>(0.05f, 1.5f)));
        FirstPersonHeadOffset = new ConfigVector(Config, "FirstPerson", "HeadOffset", new Vector3(-0.32f, -0.3f, 0.06f), 3f,
            "Head renderer offset in character local space / 头部相对角色局部坐标的偏移。");
        FirstPersonShellOffset = new ConfigVector(Config, "FirstPerson", "WingShellOffset", Vector3.zero, 3f,
            "Wing-case renderer offset in character local space / 翅壳相对角色局部坐标的偏移。");
        Config.SettingChanged += OnSettingChanged;
        Config.Save();
        _configWriteTime = File.GetLastWriteTimeUtc(Config.ConfigFilePath);
        ConfigurationRevision++;
        _harmony = new Harmony(PluginGuid);
        _harmony.PatchAll(typeof(Plugin).Assembly);
        Logger.LogInfo($"{PluginName} {PluginVersion} loaded; client-side visual replacement enabled.");
        Logger.LogInfo($"Antenna configuration: {Config.ConfigFilePath}; file changes are reloaded once per second.");
    }

    private void OnSettingChanged(object sender, BepInEx.Configuration.SettingChangedEventArgs args)
    {
        ConfigurationRevision++;
    }

    private void Update()
    {
        if (Time.unscaledTime < _nextConfigCheck) return;
        _nextConfigCheck = Time.unscaledTime + 1f;
        try
        {
            if (!File.Exists(Config.ConfigFilePath)) return;
            DateTime stamp = File.GetLastWriteTimeUtc(Config.ConfigFilePath);
            if (stamp == _configWriteTime) return;
            _configWriteTime = stamp;
            Config.Reload();
            ConfigurationRevision++;
            _configReadWarning = false;
        }
        catch (Exception ex)
        {
            if (!_configReadWarning) Logger.LogWarning($"Could not reload antenna config: {ex.Message}");
            _configReadWarning = true;
        }
    }

    private void OnDestroy()
    {
        IsShuttingDown = true;
        Config.SettingChanged -= OnSettingChanged;
        _harmony?.UnpatchSelf();
        foreach (var visual in UnityEngine.Object.FindObjectsByType<UprightCockroachVisual>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            Destroy(visual);
        RoachModelAsset.Shutdown();
    }
}
