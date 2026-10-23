using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
public static class ImportProbe
{
    public static void Run()
    {
        try
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath("Assets/Source.fbx");
            importer.isReadable = true;
            importer.importAnimation = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.SaveAndReimport();
            var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Source.fbx");
            var instance = UnityEngine.Object.Instantiate(source);
            var report = new System.Text.StringBuilder();
            foreach(var mf in instance.GetComponentsInChildren<MeshFilter>())
            {
                var p = mf.sharedMesh.vertices.Select(v => mf.transform.TransformPoint(v)).ToArray();
                var b = new Bounds(p[0], Vector3.zero);
                foreach(var v in p) b.Encapsulate(v);
                report.AppendLine(mf.name + "|" + mf.sharedMesh.vertexCount + "|" + b.min.ToString("F4") + "|" + b.max.ToString("F4"));
            }
            File.WriteAllText(Path.Combine(Application.dataPath, "../import-probe.txt"), report.ToString());
            UnityEngine.Object.DestroyImmediate(instance);
            Debug.Log("ROACH_IMPORT_PROBE_OK");
            EditorApplication.Exit(0);
        }
        catch(Exception ex) { Debug.LogException(ex); EditorApplication.Exit(1); }
    }
}
