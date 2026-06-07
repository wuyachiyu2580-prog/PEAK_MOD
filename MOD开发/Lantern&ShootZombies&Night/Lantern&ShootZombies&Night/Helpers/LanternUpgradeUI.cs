using Photon.Pun;
using UnityEngine;

namespace Lantern_ShootZombies_Night
{
    /// <summary>
    /// 灯笼升级 IMGUI 菜单。按快捷键打开/关闭。
    /// 显示当前升级点数、等级和升级按钮。
    /// </summary>
    internal class LanternUpgradeUI : MonoBehaviour
    {
        private GUIStyle _boxStyle;
        private GUIStyle _titleStyle;
        private GUIStyle _labelStyle;
        private GUIStyle _buttonStyle;
        private GUIStyle _pointsStyle;
        private GUIStyle _hintStyle;
        private Texture2D _bgTex;
        private bool _stylesInit;

        void OnGUI()
        {
            if (!LanternUpgradeSystem.IsMenuOpen) return;
            if (Plugin.EnableUpgradeSystem == null || !Plugin.EnableUpgradeSystem.Value) return;

            EnsureStyles();

            bool zh = LanguageHelper.IsChinese;
            bool hostOnly = LanternUpgradeSystem.IsHostAuthoritative();

            float w = 280f;
            float h = hostOnly ? 250f : 220f;
            float x = (Screen.width - w) * 0.5f;
            float y = (Screen.height - h) * 0.5f;

            // 背景
            GUI.DrawTexture(new Rect(x, y, w, h), _bgTex);

            float cx = x + 15f;
            float cy = y + 10f;
            float lineH = 26f;
            float bw = w - 30f;

            // 标题
            GUI.Label(new Rect(cx, cy, bw, lineH),
                zh ? "灯笼升级" : "Lantern Upgrade", _titleStyle);
            cy += lineH + 4f;

            // 点数
            GUI.Label(new Rect(cx, cy, bw, lineH),
                string.Format(zh ? "升级点数: {0}" : "Points: {0}", LanternUpgradeSystem.Points),
                _pointsStyle);
            cy += lineH + 8f;

            // 容量升级
            int capLv = LanternUpgradeSystem.CapacityLevel;
            bool capMax = capLv >= LanternUpgradeSystem.MaxLevel;
            string capLabel = capMax
                ? string.Format(zh ? "容量 Lv{0} (MAX)  ×{1:F1}" : "Capacity Lv{0} (MAX)  ×{1:F1}",
                    capLv, LanternUpgradeSystem.CapacityMultiplier)
                : string.Format(zh ? "容量 Lv{0} → Lv{1}  (×{2:F1})  费用: {3}" : "Capacity Lv{0}→{1}  (×{2:F1})  Cost: {3}",
                    capLv, capLv + 1, LanternUpgradeSystem.CapacityMultiplier + 0.2f,
                    LanternUpgradeSystem.GetCapacityCost());

            GUI.Label(new Rect(cx, cy, bw, lineH), capLabel, _labelStyle);
            cy += lineH;

            GUI.enabled = !capMax && LanternUpgradeSystem.Points >= LanternUpgradeSystem.GetCapacityCost() && !hostOnly;
            if (GUI.Button(new Rect(cx, cy, bw, 28f),
                zh ? "升级容量" : "Upgrade Capacity", _buttonStyle))
            {
                LanternUpgradeSystem.RequestUpgrade(0); // UpgradeCapacity
            }
            GUI.enabled = true;
            cy += 34f;

            // 效率升级
            int effLv = LanternUpgradeSystem.EfficiencyLevel;
            bool effMax = effLv >= LanternUpgradeSystem.MaxLevel;
            string effLabel = effMax
                ? string.Format(zh ? "效率 Lv{0} (MAX)  ×{1:F2}" : "Efficiency Lv{0} (MAX)  ×{1:F2}",
                    effLv, LanternUpgradeSystem.EfficiencyMultiplier)
                : string.Format(zh ? "效率 Lv{0} → Lv{1}  (×{2:F2})  费用: {3}" : "Efficiency Lv{0}→{1}  (×{2:F2})  Cost: {3}",
                    effLv, effLv + 1, LanternUpgradeSystem.EfficiencyMultiplier - 0.08f,
                    LanternUpgradeSystem.GetEfficiencyCost());

            GUI.Label(new Rect(cx, cy, bw, lineH), effLabel, _labelStyle);
            cy += lineH;

            GUI.enabled = !effMax && LanternUpgradeSystem.Points >= LanternUpgradeSystem.GetEfficiencyCost() && !hostOnly;
            if (GUI.Button(new Rect(cx, cy, bw, 28f),
                zh ? "升级效率" : "Upgrade Efficiency", _buttonStyle))
            {
                LanternUpgradeSystem.RequestUpgrade(1); // UpgradeEfficiency
            }
            GUI.enabled = true;

            // 非房主提示
            if (hostOnly)
            {
                cy += 34f;
                GUI.Label(new Rect(cx, cy, bw, lineH),
                    zh ? "⚠ 仅房主可升级" : "⚠ Host only", _hintStyle);
            }
        }

        private void EnsureStyles()
        {
            if (_stylesInit) return;
            _stylesInit = true;

            _bgTex = new Texture2D(1, 1);
            _bgTex.SetPixel(0, 0, new Color(0.05f, 0.05f, 0.1f, 0.88f));
            _bgTex.Apply();

            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _titleStyle.normal.textColor = new Color(1f, 0.85f, 0.3f);

            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                alignment = TextAnchor.MiddleLeft
            };
            _labelStyle.normal.textColor = Color.white;

            _pointsStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _pointsStyle.normal.textColor = new Color(0.4f, 1f, 0.4f);

            _hintStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Italic,
                alignment = TextAnchor.MiddleCenter
            };
            _hintStyle.normal.textColor = new Color(1f, 0.6f, 0.3f);

            _buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 14 };

            _boxStyle = new GUIStyle(GUI.skin.box);
        }

        void OnDestroy()
        {
            if (_bgTex != null)
            {
                Destroy(_bgTex);
                _bgTex = null;
            }
        }
    }
}
