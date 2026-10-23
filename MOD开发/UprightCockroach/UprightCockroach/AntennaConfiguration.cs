using BepInEx.Configuration;
using UnityEngine;

namespace UprightCockroach;

/// <summary>All coordinates are in the chosen attachment bone's local space.</summary>
internal sealed class AntennaConfiguration
{
    internal readonly ConfigEntry<string> AttachmentBone;
    internal readonly ConfigVector Position;
    internal readonly ConfigVector Rotation;
    internal readonly ConfigVector FirstPersonOffset;
    internal readonly ConfigVector LowerPosition;
    internal readonly ConfigVector LowerRotation;
    internal readonly ConfigVector UpperPosition;
    internal readonly ConfigVector UpperRotation;
    internal readonly ConfigEntry<float> LengthScale;
    internal readonly ConfigEntry<float> ThicknessScale;
    internal readonly ConfigEntry<float> FirstPersonLengthScale;
    internal readonly ConfigEntry<float> FirstPersonThicknessScale;

    internal AntennaConfiguration(ConfigFile file, string section, float side, bool importedModel = false)
    {
        AttachmentBone = file.Bind(section, "AttachmentBone", "Head",
            "Exact Scout bone name / 挂点骨骼名。Unknown names fall back to Head.");
        Position = new ConfigVector(file, section, "Position", Vector3.zero, 5f,
            "Root offset in attachment-bone local units / 相对挂点骨骼的局部坐标偏移。");
        Rotation = new ConfigVector(file, section, "Rotation", Vector3.zero, 360f,
            "Root Euler rotation in degrees / 触须根部局部旋转，单位度。");
        FirstPersonOffset = new ConfigVector(file, section, "FirstPersonOffset", new Vector3(0.24f * side, -0.22f, 0.08f), 5f,
            "Additional root offset for the local player's first-person view / 本地第一人称额外偏移。");
        LowerPosition = new ConfigVector(file, section, "LowerPosition", importedModel ? Vector3.zero : new Vector3(0.16f * side, 0.20f, 0.08f), 5f,
            "Lower bone position offset from imported bind pose / 下段骨相对导入默认姿态的位置偏移。");
        LowerRotation = new ConfigVector(file, section, "LowerRotation", importedModel ? Vector3.zero : new Vector3(0f, side * 24f, side * -18f), 360f,
            "Lower bone rotation offset in degrees / 下段骨局部旋转偏移，单位度。");
        UpperPosition = new ConfigVector(file, section, "UpperPosition", importedModel ? Vector3.zero : new Vector3(0.22f * side, 0.43f, 0.18f), 5f,
            "Upper bone position offset relative to lower bone / 上段骨相对下段骨的局部位置偏移。");
        UpperRotation = new ConfigVector(file, section, "UpperRotation", importedModel ? Vector3.zero : new Vector3(0f, side * 20f, side * 14f), 360f,
            "Upper bone rotation offset in degrees / 上段骨局部旋转偏移，单位度。");
        LengthScale = file.Bind(section, "LengthScale", 1f, new ConfigDescription(
            "Antenna chain length multiplier / 触须骨链长度倍率。",
            new AcceptableValueRange<float>(0.05f, 5f)));
        ThicknessScale = file.Bind(section, "ThicknessScale", 1f, new ConfigDescription(
            "Antenna thickness multiplier / 触须粗细倍率。",
            new AcceptableValueRange<float>(0.05f, 5f)));
        FirstPersonLengthScale = file.Bind(section, "FirstPersonLengthScale", 0.3f, new ConfigDescription(
            "Length multiplier for the local first-person view / 本地第一人称触须长度倍率。",
            new AcceptableValueRange<float>(0.05f, 2f)));
        FirstPersonThicknessScale = file.Bind(section, "FirstPersonThicknessScale", 0.55f, new ConfigDescription(
            "Thickness multiplier for the local first-person view / 本地第一人称触须粗细倍率。",
            new AcceptableValueRange<float>(0.05f, 2f)));
    }

    internal static float Finite(float value, float fallback)
    {
        return float.IsNaN(value) || float.IsInfinity(value) ? fallback : value;
    }
}

internal sealed class ConfigVector
{
    private readonly ConfigEntry<float> _x, _y, _z;
    private readonly Vector3 _fallback;

    internal ConfigVector(ConfigFile file, string section, string prefix, Vector3 initial, float limit, string description)
    {
        _fallback = initial;
        _x = file.Bind(section, prefix + "X", initial.x, new ConfigDescription(description + " X", new AcceptableValueRange<float>(-limit, limit)));
        _y = file.Bind(section, prefix + "Y", initial.y, new ConfigDescription(description + " Y", new AcceptableValueRange<float>(-limit, limit)));
        _z = file.Bind(section, prefix + "Z", initial.z, new ConfigDescription(description + " Z", new AcceptableValueRange<float>(-limit, limit)));
    }

    internal Vector3 Value => new Vector3(AntennaConfiguration.Finite(_x.Value, _fallback.x),
        AntennaConfiguration.Finite(_y.Value, _fallback.y), AntennaConfiguration.Finite(_z.Value, _fallback.z));
}
