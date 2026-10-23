using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace UprightCockroach;

// Versioned offline conversion format, deliberately independent of FBX and
// editor-version-specific AssetBundles. This reader performs no Unity allocation.
internal sealed class RoachModelData
{
    internal sealed class Bone
    {
        internal string Name, Driver, Parent;
        internal Matrix4x4 Rest, BindPose;
    }
    internal sealed class Part
    {
        internal string Name;
        internal int Category, Material;
        internal Vector3[] Vertices, Normals;
        internal Vector2[] UV;
        internal BoneWeight[] Weights;
        internal int[] Indices;
    }
    internal Bone[] Bones;
    internal byte[][] Textures;
    internal Part[] Parts;

    internal static RoachModelData Read(Stream stream)
    {
        using (var r = new BinaryReader(stream, Encoding.UTF8, true))
        {
            if (Encoding.ASCII.GetString(ReadExact(r, 8)) != "URCH0001") throw new InvalidDataException("Unknown roach model format.");
            var model = new RoachModelData();
            model.Bones = new Bone[Count(r, 1, 128)];
            var names = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < model.Bones.Length; i++)
            {
                var b = new Bone { Name = String(r), Driver = String(r), Parent = String(r), Rest = Matrix(r), BindPose = Matrix(r) };
                if (string.IsNullOrEmpty(b.Name) || !names.Add(b.Name)) throw new InvalidDataException("Duplicate/empty bone.");
                if (b.Driver.Length == 0 && !names.Contains(b.Parent)) throw new InvalidDataException("Missing parent bone.");
                model.Bones[i] = b;
            }
            model.Textures = new byte[Count(r, 1, 32)][];
            for (int i = 0; i < model.Textures.Length; i++) model.Textures[i] = ReadExact(r, Count(r, 16, 16 * 1024 * 1024));
            model.Parts = new Part[Count(r, 1, 64)];
            int totalVertices = 0, totalIndices = 0;
            for (int p = 0; p < model.Parts.Length; p++)
            {
                var part = new Part { Name = String(r), Category = Count(r, 0, 4), Material = Count(r, 0, model.Textures.Length - 1) };
                int vc = Count(r, 3, 200000), ic = Count(r, 3, 600000);
                totalVertices += vc; totalIndices += ic;
                if (ic % 3 != 0 || totalVertices > 500000 || totalIndices > 1500000) throw new InvalidDataException("Invalid mesh size.");
                part.Vertices = new Vector3[vc]; part.Normals = new Vector3[vc]; part.UV = new Vector2[vc]; part.Weights = new BoneWeight[vc];
                for (int v = 0; v < vc; v++)
                {
                    part.Vertices[v] = new Vector3(Float(r), Float(r), Float(r));
                    part.Normals[v] = new Vector3(Float(r), Float(r), Float(r));
                    part.UV[v] = new Vector2(Float(r), Float(r));
                    int b0 = Count(r, 0, model.Bones.Length - 1); float w0 = Float(r);
                    int b1 = Count(r, 0, model.Bones.Length - 1); float w1 = Float(r);
                    int b2 = Count(r, 0, model.Bones.Length - 1); float w2 = Float(r);
                    int b3 = Count(r, 0, model.Bones.Length - 1); float w3 = Float(r);
                    if (w0 < 0 || w1 < 0 || w2 < 0 || w3 < 0 || Math.Abs(w0 + w1 + w2 + w3 - 1f) > 0.0001f)
                        throw new InvalidDataException("Invalid skin weight.");
                    part.Weights[v] = new BoneWeight { boneIndex0 = b0, boneIndex1 = b1, boneIndex2 = b2, boneIndex3 = b3, weight0 = w0, weight1 = w1, weight2 = w2, weight3 = w3 };
                }
                part.Indices = new int[ic];
                for (int i = 0; i < ic; i++) part.Indices[i] = Count(r, 0, vc - 1);
                model.Parts[p] = part;
            }
            if (r.BaseStream.ReadByte() != -1) throw new InvalidDataException("Unexpected trailing model data.");
            return model;
        }
    }
    private static int Count(BinaryReader r, int min, int max)
    { int v = r.ReadInt32(); if (v < min || v > max) throw new InvalidDataException("Model value out of range."); return v; }
    private static float Float(BinaryReader r)
    { float v = r.ReadSingle(); if (float.IsNaN(v) || float.IsInfinity(v)) throw new InvalidDataException("Non-finite model coordinate."); return v; }
    private static byte[] ReadExact(BinaryReader r, int n)
    { var b = r.ReadBytes(n); if (b.Length != n) throw new EndOfStreamException(); return b; }
    private static string String(BinaryReader r) => Encoding.UTF8.GetString(ReadExact(r, Count(r, 0, 256)));
    private static Matrix4x4 Matrix(BinaryReader r)
    { var m = new Matrix4x4(); for (int i = 0; i < 4; i++) for (int j = 0; j < 4; j++) m[i,j] = Float(r); return m; }
}
