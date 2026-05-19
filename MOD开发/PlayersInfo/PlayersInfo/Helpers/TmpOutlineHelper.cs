using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace PlayersInfo.Helpers
{
    internal static class TmpOutlineHelper
    {
        public const float DefaultWidth = 0.08f;

        private static readonly Dictionary<string, Material> s_materials = new Dictionary<string, Material>(16);

        public static void Apply(TMP_Text text, float width, Color32 color)
        {
            if (text == null) return;

            try
            {
                width = Mathf.Clamp(width, 0f, 1f);
                var baseMaterial = text.fontSharedMaterial;
                if (baseMaterial != null)
                {
                    int widthKey = Mathf.RoundToInt(width * 10000f);
                    int colorKey = (color.r << 24) | (color.g << 16) | (color.b << 8) | color.a;
                    string key = baseMaterial.GetInstanceID() + ":" + widthKey + ":" + colorKey;

                    if (!s_materials.TryGetValue(key, out var material) || material == null)
                    {
                        material = new Material(baseMaterial);
                        material.name = "PlayersInfo_TMP_Outline_" + widthKey + "_" + colorKey.ToString("X8");
                        material.hideFlags = HideFlags.DontSave;
                        material.SetFloat("_OutlineWidth", width);
                        material.SetColor("_OutlineColor", color);
                        s_materials[key] = material;
                    }

                    text.fontSharedMaterial = material;
                    text.SetMaterialDirty();
                    text.SetVerticesDirty();
                }
                else
                {
                    text.outlineWidth = width;
                    text.outlineColor = color;
                }
            }
            catch
            {
                // Visual polish should never break HUD construction.
            }
        }
    }
}
