using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;

namespace UprightCockroach;

internal sealed class RoachModelAsset
{
    internal RoachModelData Data;
    internal Mesh[] Meshes;
    internal Material[] Materials;
    private Texture2D[] _textures;
    private static RoachModelAsset _shared;
    private static bool _failed;

    internal static RoachModelAsset Get()
    {
        if (_shared != null) return _shared;
        if (_failed) throw new InvalidOperationException("Model asset previously failed to load.");
        var result = new RoachModelAsset();
        try
        {
            using (var s = typeof(Plugin).Assembly.GetManifestResourceStream("UprightCockroach.Model"))
            { if (s == null) throw new InvalidOperationException("Embedded model missing."); result.Data = RoachModelData.Read(s); }
            result.CreateResources();
            _shared = result;
            Plugin.Log?.LogInfo($"Loaded textured FBX conversion: {result.Meshes.Length} meshes, {result.Data.Bones.Length} bones, {result.Materials.Length} materials.");
            return result;
        }
        catch { result.Dispose(); _failed = true; throw; }
    }
    private void CreateResources()
    {
        // Game Unity modules use netstandard 2.1; bind this entry point at runtime
        // to keep the plugin's established net472/BepInEx ABI.
        var imageType = Type.GetType("UnityEngine.ImageConversion, UnityEngine.ImageConversionModule", true);
        var loadImage = (Func<Texture2D, byte[], bool, bool>)Delegate.CreateDelegate(typeof(Func<Texture2D, byte[], bool, bool>),
            imageType.GetMethod("LoadImage", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(Texture2D), typeof(byte[]), typeof(bool) }, null));
        // W/Character ignores ordinary texture/color properties. Require a shader
        // with a real texture slot; never silently display a white placeholder.
        Shader shader = null;
        string[] candidates = GraphicsSettings.currentRenderPipeline != null
            ? new[] { "Universal Render Pipeline/Lit", "Universal Render Pipeline/Simple Lit", "Universal Render Pipeline/Unlit" }
            : new[] { "Standard", "Legacy Shaders/Diffuse", "Unlit/Texture" };
        foreach (string name in candidates)
        { var candidate = Shader.Find(name); if (candidate != null && candidate.isSupported) { shader = candidate; break; } }
        if (shader == null) throw new InvalidOperationException("No supported textured shader found.");
        Materials = new Material[Data.Textures.Length]; _textures = new Texture2D[Materials.Length];
        for (int i = 0; i < Materials.Length; i++)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, true); _textures[i] = texture;
            if (!loadImage(texture, Data.Textures[i], false)) throw new InvalidOperationException("Invalid model texture.");
            texture.name = "RoachTexture_" + i; texture.wrapMode = TextureWrapMode.Repeat; texture.filterMode = FilterMode.Trilinear; texture.anisoLevel = 2;
            texture.Apply(true, true);
            var material = new Material(shader) { name = "RoachMaterial_" + i }; Materials[i] = material;
            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
            if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", texture);
            if (!material.HasProperty("_BaseMap") && !material.HasProperty("_MainTex")) throw new InvalidOperationException("Shader has no texture slot.");
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
            if (material.HasProperty("_Color")) material.SetColor("_Color", Color.white);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0f);
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", 0.2f);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.2f);
            // The FBX conversion changes handedness while matching Scout axes.
            // Keep both sides visible until the source winding is authored again.
            if (material.HasProperty("_Cull")) material.SetFloat("_Cull", 0f);
            if (material.HasProperty("_AlphaClip")) material.SetFloat("_AlphaClip", 0f);
        }
        var binds = new Matrix4x4[Data.Bones.Length];
        for (int i = 0; i < binds.Length; i++) binds[i] = Data.Bones[i].BindPose;
        Meshes = new Mesh[Data.Parts.Length];
        for (int i = 0; i < Meshes.Length; i++)
        {
            var p = Data.Parts[i]; var m = new Mesh { name = p.Name, indexFormat = p.Vertices.Length > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 }; Meshes[i] = m;
            m.vertices = p.Vertices; m.normals = p.Normals; m.uv = p.UV; m.boneWeights = p.Weights; m.bindposes = binds; m.triangles = p.Indices;
            m.RecalculateBounds();
        }
        Plugin.Log?.LogInfo("Roach texture shader: " + shader.name);
    }
    private void Dispose()
    {
        if (Meshes != null) foreach (var x in Meshes) if (x != null) UnityEngine.Object.Destroy(x);
        if (Materials != null) foreach (var x in Materials) if (x != null) UnityEngine.Object.Destroy(x);
        if (_textures != null) foreach (var x in _textures) if (x != null) UnityEngine.Object.Destroy(x);
    }
    internal static void Shutdown() { _shared?.Dispose(); _shared = null; _failed = false; }
}
