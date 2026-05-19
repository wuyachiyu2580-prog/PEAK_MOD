using UnityEngine;

namespace Lantern_ShootZombies_Night
{
    /// <summary>
    /// HUD 编排器：挂到游戏 hudCanvas 显示可视化面板，通过 ModConfig 开关、位置、尺寸预设。
    /// </summary>
    internal static class LanternHud
    {
        private static bool _active;
        private static HudPosition _activePos;
        private static HudSizePreset _activeSize;
        private static LanternHudPanel _panel;
        private static float _tickTimer;
        private const float TickInterval = 0.25f;

        /// <summary>每帧由 Plugin.Update 调用。</summary>
        public static void Tick()
        {
            bool desired = Plugin.EnableHud.Value;
            var desiredPos = Plugin.HudPos.Value;
            var desiredSize = Plugin.HudSize.Value;

            // 1. 开关变化
            if (desired != _active)
            {
                if (desired)
                {
                    _active = true;
                    _activePos = desiredPos;
                    _activeSize = desiredSize;
                    Plugin.Log?.LogInfo("[DEBUG] [HUD] Enabled — waiting for GUIManager...");
                }
                else
                {
                    DestroyAll();
                    Plugin.Log?.LogInfo("[DEBUG] [HUD] Disabled");
                    return;
                }
            }

            if (!_active) return;

            // 2. 等待 GUIManager 就绪后创建面板
            if (_panel == null || !_panel.IsCreated)
            {
                TryAttachToGameHud();
                if (_panel == null || !_panel.IsCreated) return;
            }

            // 3. 位置变化
            if (desiredPos != _activePos)
            {
                _activePos = desiredPos;
                _panel.SetPosition(_activePos);
                Plugin.Log?.LogInfo($"[DEBUG] [HUD] Position → {_activePos}");
            }

            // 4. 尺寸预设变化 → 重建
            if (desiredSize != _activeSize)
            {
                _activeSize = desiredSize;
                DestroyPanel();
                TryAttachToGameHud();
                Plugin.Log?.LogInfo($"[DEBUG] [HUD] Size → {_activeSize}");
                return;
            }

            // 5. 定时刷新数据 (4Hz)
            _tickTimer += Time.deltaTime;
            if (_tickTimer >= TickInterval)
            {
                _tickTimer = 0f;
                _panel.UpdateData();
            }
        }

        // ══════════ 创建 / 销毁 ══════════

        private static void TryAttachToGameHud()
        {
            var guiMgr = GUIManager.instance;
            if (guiMgr == null || guiMgr.hudCanvas == null) return;

            _panel = new LanternHudPanel();
            _panel.Create(guiMgr.hudCanvas.transform, _activeSize);
            _panel.SetPosition(_activePos);
            Plugin.Log?.LogInfo($"[DEBUG] [HUD] Panel attached to hudCanvas at {_activePos}, size={_activeSize}");
        }

        private static void DestroyPanel()
        {
            if (_panel != null)
            {
                _panel.Destroy();
                _panel = null;
            }
            _tickTimer = 0f;
        }

        private static void DestroyAll()
        {
            DestroyPanel();
            _active = false;
        }
    }
}
