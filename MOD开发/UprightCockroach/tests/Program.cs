using System;
using System.IO;
using BepInEx.Configuration;
using UprightCockroach;

internal static class Program
{
    private static int checks;
    private static void Check(bool condition, string name)
    {
        if (!condition) throw new Exception(name);
        checks++;
    }

    private static void Main(string[] args)
    {
        string folder = args[0];
        Directory.CreateDirectory(folder);
        string testPath = Path.Combine(folder, "roundtrip.cfg");
        File.WriteAllText(testPath, "[Antenna.Left]\nPositionX = 0.31\nAttachmentBone = Torso\n[Antenna.Right]\nPositionX = -0.27\nFirstPersonOffsetZ = 0.15\n");
        var file = new ConfigFile(testPath, false);
        var left = new AntennaConfiguration(file, "Antenna.Left", -1);
        var right = new AntennaConfiguration(file, "Antenna.Right", 1);
        Check(left.Position.Value.x == 0.31f, "Existing left input survives binding");
        Check(right.Position.Value.x == -0.27f, "Left/right independent");
        Check(left.AttachmentBone.Value == "Torso", "Bone input preserved");
        Check(right.FirstPersonOffset.Value.z == 0.15f, "First-person offset preserved");
        Check(left.LowerPosition.Value.x == -0.16f && right.LowerPosition.Value.x == 0.16f, "Mirrored default positions");
        Check(left.UpperRotation.Value.z == -14f && right.UpperRotation.Value.z == 14f, "Mirrored default rotations");
        Check(left.LengthScale.Value == 1f && right.ThicknessScale.Value == 1f, "Default size unchanged");
        Check(left.FirstPersonLengthScale.Value == 0.3f && right.FirstPersonThicknessScale.Value == 0.55f, "First-person antenna defaults are compact");
        int changes = 0;
        file.SettingChanged += (s,e) => changes++;
        file.SaveOnConfigSet = false;
        File.WriteAllText(testPath, "[Antenna.Left]\nPositionX = 0.45\nLengthScale = 999\nThicknessScale = -2\n[Antenna.Right]\nPositionX = -0.27\n");
        file.Reload();
        Check(left.Position.Value.x == 0.45f && right.Position.Value.x == -0.27f, "Disk reload updates existing entries");
        Check(changes > 0, "Reload notifies consumers");
        Check(left.LengthScale.Value == 5f && left.ThicknessScale.Value == 0.05f, "Out-of-range sizes clamp");
        Check(AntennaConfiguration.Finite(float.NaN, 1f) == 1f, "NaN guarded");
        Check(AntennaConfiguration.Finite(float.PositiveInfinity, 1f) == 1f, "Infinity guarded");
        string defaultsPath = Path.Combine(folder, "wuyachiyu.peak.uprightcockroach.cfg");
        if (File.Exists(defaultsPath)) File.Delete(defaultsPath);
        var defaults = new ConfigFile(defaultsPath, false);
        new AntennaConfiguration(defaults, "Antenna.Left", -1);
        new AntennaConfiguration(defaults, "Antenna.Right", 1);
        defaults.Save();
        Check(defaults.Count == 52, "Expected 52 entries serialized");
        Console.WriteLine($"PASS {checks} configuration checks; defaults: {defaultsPath}");
    }
}
