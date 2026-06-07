using BepInEx.Configuration;
using UnityEngine;

namespace Lantern_ShootZombies_Night
{
    /// <summary>
    /// Float ConfigEntry step-snapping extension.
    /// Call .WithStep(step) after Config.Bind() to make the value snap
    /// to the nearest multiple of <paramref name="step"/> whenever it changes.
    /// </summary>
    internal static class ConfigStepHelper
    {
        /// <summary>
        /// Register a SettingChanged handler that rounds the value to the nearest step.
        /// Also snaps the initial loaded value.
        /// </summary>
        public static ConfigEntry<float> WithStep(this ConfigEntry<float> entry, float step)
        {
            // Handler: snap on every change (slider drag, file load, network sync, etc.)
            entry.SettingChanged += (sender, args) =>
            {
                float v = entry.Value;
                float snapped = Snap(v, step, entry);
                if (Mathf.Abs(v - snapped) > step * 0.001f)
                    entry.Value = snapped;
            };

            // Snap the initial value that was loaded from config file
            float init = entry.Value;
            float initSnapped = Snap(init, step, entry);
            if (Mathf.Abs(init - initSnapped) > step * 0.001f)
                entry.Value = initSnapped;

            return entry;
        }

        private static float Snap(float value, float step, ConfigEntry<float> entry)
        {
            float snapped = Mathf.Round(value / step) * step;
            // Respect AcceptableValueRange if present
            AcceptableValueRange<float> range =
                entry.Description?.AcceptableValues as AcceptableValueRange<float>;
            if (range != null)
                snapped = Mathf.Clamp(snapped, range.MinValue, range.MaxValue);
            return snapped;
        }
    }
}
