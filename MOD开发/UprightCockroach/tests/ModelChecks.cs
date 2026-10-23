using System;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx.Configuration;
using UprightCockroach;

internal static class ModelChecks
{
    static int checks;
    static void Check(bool value, string name) { if (!value) throw new Exception(name); checks++; }
    static void Reject(byte[] data, string name)
    {
        bool rejected = false;
        try { using (var s = new MemoryStream(data)) RoachModelData.Read(s); }
        catch (InvalidDataException) { rejected = true; }
        catch (IOException) { rejected = true; }
        Check(rejected, name);
    }
    static void Main(string[] args)
    {
        try { Run(args); }
        catch(Exception ex) { Console.WriteLine("FAIL after "+checks+" checks: "+ex.GetType().FullName+" / "+ex.Message); Environment.ExitCode=1; }
    }
    static void Run(string[] args)
    {
        Console.WriteLine("Reading converted model");
        var bytes = File.ReadAllBytes(args[0]);
        RoachModelData model;
        using (var s = new MemoryStream(bytes)) model = RoachModelData.Read(s);
        Check(model.Parts.Length == 24, "Cleaned model has 24 parts");
        Check(model.Bones.Length == 26, "16 Scout, 6 extra-arm and 4 antenna bones");
        Check(model.Textures.Length == 8, "Eight used diffuse textures");
        Check(model.Parts.Sum(p => p.Indices.Length / 3) == 91600, "Triangle count preserved after exclusions");
        Check(model.Parts.Count(p => p.Category == 2) == 2, "Both antenna meshes retained");
        Check(model.Parts.Count(p => p.Category == 3) == 6, "Head meshes are a separate first-person group");
        Check(model.Parts.Count(p => p.Category == 4) == 5, "Wing-case meshes are a separate first-person group");
        foreach (string part in new[] { "17", "20", "21", "23" })
            Check(model.Parts.Any(p => p.Category == 1 && p.Name.EndsWith(part)), "Retain arm " + part);
        Check(!model.Parts.Any(p => new[] { "09", "26", "27", "28", "29" }.Any(p.Name.EndsWith)), "Excluded microphone, floor and all grass");
        foreach (var b in model.Bones)
        {
            double error = 0;
            for (int r = 0; r < 4; r++) for (int c = 0; c < 4; c++)
            { double v=0; for(int k=0;k<4;k++)v+=b.Rest[r,k]*b.BindPose[k,c]; error=Math.Max(error,Math.Abs(v-(r==c?1:0))); }
            Check(error<0.00001, "Float32 bind inverse: " + b.Name);
        }
        Reject(bytes.Take(bytes.Length-1).ToArray(), "Reject truncation");
        var bad=(byte[])bytes.Clone(); bad[0]=0; Reject(bad, "Reject invalid header");
        bad=(byte[])bytes.Clone(); Array.Copy(BitConverter.GetBytes(int.MaxValue),0,bad,8,4); Reject(bad, "Reject unbounded bone count");
        Reject(bytes.Concat(new byte[]{1}).ToArray(), "Reject trailing bytes");
        var assembly = Assembly.LoadFile(Path.GetFullPath(args[1]));
        using(var resource=assembly.GetManifestResourceStream("UprightCockroach.Model"))
        using(var copy=new MemoryStream())
        { Check(resource!=null, "Model embedded in built DLL"); resource.CopyTo(copy); Check(bytes.SequenceEqual(copy.ToArray()), "DLL embeds exact validated package"); }
        string folder=Path.GetFullPath(args[2]); Directory.CreateDirectory(folder);
        string configPath=Path.Combine(folder,"wuyachiyu.peak.uprightcockroach.cfg");
        File.WriteAllText(configPath,"[Antenna.Left]\nPositionX = 0.31\n[Model.Antenna.Right]\nFirstPersonOffsetZ = 0.15\n");
        var config=new ConfigFile(configPath,false);
        var left=new AntennaConfiguration(config,"Model.Antenna.Left",-1,true);
        var right=new AntennaConfiguration(config,"Model.Antenna.Right",1,true);
        Check(left.Position.Value.x==0 && left.LowerPosition.Value.y==0 && right.UpperRotation.Value.z==0, "Imported bind pose starts with zero offsets");
        Check(right.FirstPersonOffset.Value.z==0.15f, "Imported first-person offset preserved");
        config.Save(); Check(File.ReadAllText(configPath).Contains("0.31"), "Old primitive config retained");
        File.WriteAllText(configPath,"[Model.Antenna.Left]\nPositionX = 0.25\nLowerRotationZ = 12\nLengthScale = 999\n[Model.Antenna.Right]\nPositionX = -0.1\n"); config.Reload();
        Check(left.Position.Value.x==0.25f && right.Position.Value.x==-0.1f, "Live reload remains independent");
        Check(left.LowerRotation.Value.z==12 && left.LengthScale.Value==5, "Imported bone rotation and size bounds");
        string defaultsPath=Path.Combine(folder,"imported-defaults.cfg");
        if(File.Exists(defaultsPath))File.Delete(defaultsPath);
        var defaults=new ConfigFile(defaultsPath,false); new AntennaConfiguration(defaults,"Model.Antenna.Left",-1,true); new AntennaConfiguration(defaults,"Model.Antenna.Right",1,true); defaults.Save();
        Console.WriteLine($"PASS {checks} model/config checks; {model.Parts.Sum(p=>p.Vertices.Length)} vertices; {bytes.Length} package bytes");
    }
}
