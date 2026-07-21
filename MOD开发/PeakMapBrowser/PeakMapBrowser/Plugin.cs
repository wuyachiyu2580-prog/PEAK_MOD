using BepInEx;
using BepInEx.Configuration;
using UnityEngine;

namespace PeakMapBrowser
{
    [BepInPlugin("com.wuyachiyu.peakmapbrowser", "PEAK Map Browser", "0.1.0")]
    public sealed class Plugin : BaseUnityPlugin
    {
        private PeakMapWindow _window;
        private ConfigEntry<string> _apiBaseUrl;
        private ConfigEntry<string> _language;
        private ConfigEntry<int> _pageSize;
        private ConfigEntry<KeyCode> _toggleKey;

        private void Awake()
        {
            _apiBaseUrl = Config.Bind("General", "ApiBaseUrl", "https://peakmap.top", "PEAK map site API base URL.");
            _language = Config.Bind("General", "Language", "auto", "auto follows the game's Unity Localization language; zh or en forces a language.");
            _pageSize = Config.Bind("General", "PageSize", 12, "Maps per API page. API allows 1-50.");
            _toggleKey = Config.Bind("General", "ToggleKey", KeyCode.Slash, "Open/close PEAK Map Browser.");

            string toggleKeyLabel = FormatKeyName(_toggleKey.Value);
            _window = new PeakMapWindow(this, Logger, _apiBaseUrl.Value, _language.Value, Mathf.Clamp(_pageSize.Value, 1, 50), toggleKeyLabel);
            Logger.LogInfo("PEAK Map Browser loaded. Press " + toggleKeyLabel + " to open.");
        }

        private void Update()
        {
            if (Input.GetKeyDown(_toggleKey.Value))
            {
                _window.Toggle();
            }

            _window.Update();
        }

        private void OnDestroy()
        {
            if (_window != null)
            {
                _window.Dispose();
            }
        }

        private void OnGUI()
        {
            _window.Draw();
        }

        private static string FormatKeyName(KeyCode key)
        {
            return key == KeyCode.Slash ? "/" : key.ToString();
        }
    }
}
