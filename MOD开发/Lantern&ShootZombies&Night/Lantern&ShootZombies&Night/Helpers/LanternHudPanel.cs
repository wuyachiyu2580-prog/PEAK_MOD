using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Lantern_ShootZombies_Night
{
    /// <summary>
    /// 可视化 HUD 面板构建器：纯代码构建 UI 树 + 数据刷新。
    /// 由 LanternHud 管理生命周期。
    /// </summary>
    internal class LanternHudPanel
    {
        // ── UI 元素 ──
        private GameObject _root;
        private RectTransform _rootRect;
        private CanvasGroup _canvasGroup;
        private Image _fuelBarFill;
        private RectTransform _fuelBarFillRect;
        private TextMeshProUGUI _fuelText;
        private TextMeshProUGUI _multiplierText;
        private TextMeshProUGUI _statusText;
        private TextMeshProUGUI _upgradeText;

        // ── 常量 ──
        private const float Margin = 6f;
        private const float EdgeOffset = 10f;

        // ── 运行时字体缓存 ──
        private static TMP_FontAsset _cachedFont;

        // ── 值缓存：仅在数据变化时更新 UI 组件 ──
        private float _cachedFuelPct = -1f;
        private string _cachedFuelStr = "";
        private string _cachedMultStr = "";
        private string _cachedStatusStr = "";
        private string _cachedUpgradeStr = "";
        private bool _cachedVisible = true;

        /// <summary>面板是否已创建。</summary>
        public bool IsCreated => _root != null;

        /// <summary>
        /// 纯代码构建 UI 树。parent 是 Canvas 内的容器。
        /// </summary>
        public void Create(Transform parent, HudSizePreset sizePreset = HudSizePreset.Large)
        {
            if (_root != null) return;

            float panelW, panelH, barH, fSize;
            GetPresetParams(sizePreset, out panelW, out panelH, out barH, out fSize);

            // ── 从游戏已知支持中文的 UI 组件复制字体 ──
            if (_cachedFont == null)
            {
                // 优先: PlayerConnectionLog.text — 已知显示中文
                var pcl = Object.FindAnyObjectByType<PlayerConnectionLog>();
                if (pcl != null && pcl.text != null)
                    _cachedFont = pcl.text.font;

                // 备选: GUIManager 交互提示文字
                if (_cachedFont == null)
                {
                    var gui = GUIManager.instance;
                    if (gui != null && gui.interactNameText != null)
                        _cachedFont = gui.interactNameText.font;
                }

                Plugin.Log?.LogInfo($"[DEBUG] [HUD] Font resolved: {(_cachedFont != null ? _cachedFont.name : "NULL")}");
            }

            // ── Root (background) ──
            _root = new GameObject("LSN_HudPanel");
            _root.transform.SetParent(parent, false);
            _rootRect = _root.AddComponent<RectTransform>();
            _rootRect.sizeDelta = new Vector2(panelW, panelH);

            var bg = _root.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.6f);

            _canvasGroup = _root.AddComponent<CanvasGroup>();
            _canvasGroup.alpha = 1f;
            _canvasGroup.blocksRaycasts = false;
            _canvasGroup.interactable = false;

            // ── 布局 Y 坐标（从上往下）──
            float barY = panelH / 2f - Margin - barH / 2f;       // 燃料条
            float line2Y = barY - barH - 4f;                           // 倍率行
            float line3Y = line2Y - fSize - 4f;                          // 状态行
            float line4Y = line3Y - fSize - 4f;                          // 升级行

            // ── Fuel bar background (gray) ──
            var barBgGo = CreateChild(_root.transform, "FuelBarBG");
            var barBgRect = barBgGo.AddComponent<RectTransform>();
            barBgRect.anchoredPosition = new Vector2(0f, barY);
            barBgRect.sizeDelta = new Vector2(panelW - Margin * 2, barH);
            var barBgImg = barBgGo.AddComponent<Image>();
            barBgImg.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);

            // ── Fuel bar fill (green→yellow→red) ──
            var barFillGo = CreateChild(barBgGo.transform, "FuelBarFill");
            var barFillRect = barFillGo.AddComponent<RectTransform>();
            barFillRect.anchorMin = Vector2.zero;
            barFillRect.anchorMax = Vector2.one;
            barFillRect.sizeDelta = Vector2.zero;
            barFillRect.anchoredPosition = Vector2.zero;
            _fuelBarFillRect = barFillRect;
            _fuelBarFill = barFillGo.AddComponent<Image>();
            _fuelBarFill.color = Color.green;

            // ── Fuel text (overlay on bar, right-aligned) ──
            _fuelText = CreateTMP(_root.transform, "FuelText",
                new Vector2(0f, barY),
                new Vector2(panelW - Margin * 2, barH + 4f),
                TextAlignmentOptions.Right, fSize - 2f, _cachedFont);
            _fuelText.color = new Color(1f, 0.88f, 0.1f); // 金黄色，在绿/红/灰底色上均醒目

            // ── Multiplier line ──
            _multiplierText = CreateTMP(_root.transform, "MultiplierText",
                new Vector2(0f, line2Y),
                new Vector2(panelW - Margin * 2, fSize + 4f),
                TextAlignmentOptions.Center, fSize, _cachedFont);

            // ── Status line ──
            _statusText = CreateTMP(_root.transform, "StatusText",
                new Vector2(0f, line3Y),
                new Vector2(panelW - Margin * 2, fSize + 4f),
                TextAlignmentOptions.Center, fSize, _cachedFont);

            // ── Upgrade line ──
            _upgradeText = CreateTMP(_root.transform, "UpgradeText",
                new Vector2(0f, line4Y),
                new Vector2(panelW - Margin * 2, fSize + 4f),
                TextAlignmentOptions.Center, fSize, _cachedFont);
        }

        /// <summary>根据枚举设置面板锚点和位置（8方位）。</summary>
        public void SetPosition(HudPosition pos)
        {
            if (_rootRect == null) return;
            switch (pos)
            {
                case HudPosition.TopLeft:
                    SetAnchor(0f, 1f, 0f, 1f, EdgeOffset, -EdgeOffset);
                    break;
                case HudPosition.Top:
                    SetAnchor(0.5f, 1f, 0.5f, 1f, 0f, -EdgeOffset);
                    break;
                case HudPosition.TopRight:
                    SetAnchor(1f, 1f, 1f, 1f, -EdgeOffset, -EdgeOffset);
                    break;
                case HudPosition.Left:
                    SetAnchor(0f, 0.5f, 0f, 0.5f, EdgeOffset, 0f);
                    break;
                case HudPosition.Right:
                    SetAnchor(1f, 0.5f, 1f, 0.5f, -EdgeOffset, 0f);
                    break;
                case HudPosition.BottomLeft:
                    SetAnchor(0f, 0f, 0f, 0f, EdgeOffset, EdgeOffset);
                    break;
                case HudPosition.Bottom:
                    SetAnchor(0.5f, 0f, 0.5f, 0f, 0f, EdgeOffset);
                    break;
                case HudPosition.BottomRight:
                    SetAnchor(1f, 0f, 1f, 0f, -EdgeOffset, EdgeOffset);
                    break;
            }
        }

        /// <summary>读取游戏数据并更新 UI 显示（仅在数据变化时写入 UI 组件）。</summary>
        public void UpdateData()
        {
            if (_root == null) return;

            Character local = Character.localCharacter;
            if (local == null || (local.data != null && local.data.dead))
            {
                if (_cachedVisible)
                {
                    _cachedVisible = false;
                    _canvasGroup.alpha = 0f;
                }
                return;
            }
            if (!_cachedVisible)
            {
                _cachedVisible = true;
                _canvasGroup.alpha = 1f;
            }

            bool zh = LanguageHelper.IsChinese;

            // ── 燃料 ──
            ItemInstanceData liveData;
            ItemSlot litSlot = LanternHelper.FindLitLanternSlot(local, out liveData);
            ItemInstanceData dataForFuel = liveData ?? litSlot?.data;
            if (dataForFuel == null)
                LanternHelper.TryGetAnyLocalLanternData(local, out dataForFuel);

            LanternFuelSync.Snapshot syncedFuel = default;
            bool hasSyncedFuel = dataForFuel != null
                && LanternFuelSync.TryGetFresh(dataForFuel.guid, out syncedFuel);

            // 【10024 修复】FindLitLanternSlot 的背包分支只按名字匹配，
            // 背包里已被游戏 SnuffLantern 熏灭的灯笼也会被返回。
            // HUD 这里需要精准判定“真的在烧”，否则点灭后时间会冻住。
            bool reallyLit = false;
            if (litSlot != null && dataForFuel != null)
            {
                if (hasSyncedFuel)
                {
                    reallyLit = syncedFuel.Lit;
                }
                else
                {
                // 优先：FlareActive 标志（手持点燃有效）
                BoolItemData flareData;
                if (dataForFuel.TryGetDataEntry(DataEntryKey.FlareActive, out flareData) && flareData != null && flareData.Value)
                {
                    reallyLit = true;
                }
                else
                {
                    // 兄底：查世界实例的 lit 字段（BPR 模式切换等场景）
                    System.Guid g = dataForFuel.guid;
                    if (g != System.Guid.Empty)
                    {
                        Item worldItem = LanternHelper.FindWorldItemByGuid(g);
                        if (worldItem != null)
                        {
                            var lanComp = worldItem.GetComponent<Lantern>();
                            if (lanComp != null) reallyLit = ReflectionCache.GetLit(lanComp);
                        }
                    }
                }
            }
            }

            FloatItemData fuelData = null;
            if (reallyLit && dataForFuel != null
                && (hasSyncedFuel || dataForFuel.TryGetDataEntry(DataEntryKey.Fuel, out fuelData)))
            {
                float seconds = hasSyncedFuel ? syncedFuel.Fuel : fuelData.Value;
                float maxFuel = hasSyncedFuel ? syncedFuel.MaxFuel : GetMaxFuel(dataForFuel);
                float pct = maxFuel > 0f ? Mathf.Clamp01(seconds / maxFuel) : 0.5f;

                // 燃料条：仅在百分比变化超过 0.5% 时更新
                if (!MathExtensions.Approximately(pct, _cachedFuelPct) || Mathf.Abs(pct - _cachedFuelPct) > 0.005f)
                {
                    _cachedFuelPct = pct;
                    _fuelBarFillRect.anchorMax = new Vector2(pct, 1f);
                    _fuelBarFill.color = FuelColor(pct);
                }

                int min = Mathf.FloorToInt(seconds / 60f);
                int sec = Mathf.FloorToInt(seconds % 60f);
                string fuelStr = min > 0
                    ? (zh ? $"{min}分{sec}秒" : $"{min}m{sec}s")
                    : (zh ? $"{seconds:F1}秒" : $"{seconds:F1}s");

                if (fuelStr != _cachedFuelStr)
                {
                    _cachedFuelStr = fuelStr;
                    _fuelText.text = fuelStr;
                }
            }
            else
            {
                if (_cachedFuelPct != 0f)
                {
                    _cachedFuelPct = 0f;
                    _fuelBarFillRect.anchorMax = new Vector2(0f, 1f);
                    _fuelBarFill.color = new Color(0.4f, 0.4f, 0.4f, 0.6f);
                }
                string noLamp = zh ? "无灯" : "No Lamp";
                if (noLamp != _cachedFuelStr)
                {
                    _cachedFuelStr = noLamp;
                    _fuelText.text = noLamp;
                }
            }

            // ── 倍率行 ──
            float warmth = Plugin.LanternWarmthMultiplier.Value;
            float drain = LanternHelper.FuelDrainMultiplier;
            string warmthStr = $"<color=#FFFFFF>{(zh ? "回暖" : "Warm")} {warmth:F2}x</color>";
            string drainPart = Mathf.Approximately(drain, 1f) ? ""
                : $" <color=#FF6666>| {(zh ? "消耗" : "Drain")} {drain:F1}x</color>";

            string lastPart = "";
            string src = RestoreTracker.LastSource;
            if (!string.IsNullOrEmpty(src))
            {
                float lastW = RestoreTracker.LastWarmth;
                string srcName = GetRestoreSourceName(src, zh);
                string valStr = lastW > 0f ? $"+{lastW:F0}s" : (zh ? "满" : "MAX");
                lastPart = $" <color=#00FFFF>←{srcName}{valStr}</color>";
            }

            string multLine = warmthStr + drainPart + lastPart;
            if (multLine != _cachedMultStr)
            {
                _cachedMultStr = multLine;
                _multiplierText.text = multLine;
            }

            // ── 状态行 ──
            float reserve = LanternHelper.ReserveWarmth;
            string reserveStr = "";
            if ((int)Plugin.ReserveWarmthMax.Value > 0)
            {
                reserveStr = reserve > 0f
                    ? $"<color=#FFA500>{(zh ? "备用" : "Rsv")} {reserve:F1}s</color>"
                    : $"<color=#888888>{(zh ? "备用" : "Rsv")} 0</color>";
            }

            string dayNightStr = "";
            if (Plugin.ShowDayNightOnHud.Value)
                dayNightStr = DayNightTracker.FormatForHud(zh);

            // AutoRefill 活跃标记（1 秒内有 Tick）
            string refillStr = "";
            if (Plugin.AutoRefillEnabled != null && Plugin.AutoRefillEnabled.Value
                && Time.time - RestoreTracker.LastAutoRefillTime < 1f)
            {
                refillStr = zh ? "<color=#88FFAA>回血中…</color>" : "<color=#88FFAA>Refill…</color>";
            }

            string statusLine = "";
            if (reserveStr.Length > 0) statusLine = reserveStr;
            if (refillStr.Length > 0)
                statusLine = statusLine.Length > 0 ? statusLine + "  " + refillStr : refillStr;
            if (dayNightStr.Length > 0)
                statusLine = statusLine.Length > 0 ? statusLine + "  " + dayNightStr : dayNightStr;

            if (statusLine != _cachedStatusStr)
            {
                _cachedStatusStr = statusLine;
                _statusText.text = statusLine;
            }

            // ── 升级行 ──
            string upgStr = "";
            if (Plugin.EnableUpgradeSystem != null && Plugin.EnableUpgradeSystem.Value)
            {
                int capLv = LanternUpgradeSystem.CapacityLevel;
                int effLv = LanternUpgradeSystem.EfficiencyLevel;
                int pts = LanternUpgradeSystem.Points;
                int capCost = LanternUpgradeSystem.GetCapacityCost();
                int effCost = LanternUpgradeSystem.GetEfficiencyCost();
                string capNext = capLv >= LanternUpgradeSystem.MaxLevel ? "MAX" : $"\u2191{capCost}";
                string effNext = effLv >= LanternUpgradeSystem.MaxLevel ? "MAX" : $"\u2191{effCost}";
                upgStr = zh
                    ? $"<color=#00FF88>\u5bb9Lv{capLv}({capNext}) \u6548Lv{effLv}({effNext}) \u2605{pts}</color>"
                    : $"<color=#00FF88>Cap:{capLv}({capNext}) Eff:{effLv}({effNext}) \u2605{pts}</color>";
            }
            if (upgStr != _cachedUpgradeStr)
            {
                _cachedUpgradeStr = upgStr;
                _upgradeText.text = upgStr;
            }
        }

        /// <summary>销毁面板。</summary>
        public void Destroy()
        {
            if (_root != null)
            {
                Object.Destroy(_root);
                _root = null;
            }
        }

        // ═══════════════════ Private Helpers ═══════════════════

        private void SetAnchor(float ax, float ay, float px, float py, float offX, float offY)
        {
            _rootRect.anchorMin = new Vector2(ax, ay);
            _rootRect.anchorMax = new Vector2(ax, ay);
            _rootRect.pivot = new Vector2(px, py);
            _rootRect.anchoredPosition = new Vector2(offX, offY);
        }

        private static GameObject CreateChild(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go;
        }

        private static TextMeshProUGUI CreateTMP(Transform parent, string name,
            Vector2 anchoredPos, Vector2 size, TextAlignmentOptions align, float fontSize,
            TMP_FontAsset font = null)
        {
            var go = CreateChild(parent, name);
            var rect = go.AddComponent<RectTransform>();
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = size;
            var tmp = go.AddComponent<TextMeshProUGUI>();
            if (font != null) tmp.font = font;
            tmp.fontSize = fontSize;
            tmp.alignment = align;
            tmp.richText = true;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.overflowMode = TextOverflowModes.Overflow;
            return tmp;
        }

        /// <summary>根据百分比返回燃料条颜色（绿→黄→红渐变）。</summary>
        private static Color FuelColor(float pct)
        {
            if (pct > 0.5f)
                return Color.Lerp(Color.yellow, Color.green, (pct - 0.5f) * 2f);
            return Color.Lerp(Color.red, Color.yellow, pct * 2f);
        }

        /// <summary>通过世界实例获取灯笼最大燃料。</summary>
        private static float GetMaxFuel(ItemInstanceData data)
        {
            if (data == null) return 0f;
            System.Guid guid = data.guid;
            if (guid == System.Guid.Empty) return 0f;
            Item worldItem = LanternHelper.FindWorldItemByGuid(guid);
            if (worldItem != null)
            {
                var lantern = worldItem.GetComponent<Lantern>();
                if (lantern != null) return lantern.startingFuel;
            }
            return 0f;
        }

        /// <summary>尺寸预设参数映射。</summary>
        private static void GetPresetParams(HudSizePreset preset,
            out float panelW, out float panelH, out float barH, out float fontSize)
        {
            switch (preset)
            {
                case HudSizePreset.Small:
                    panelW = 240f; panelH = 62f; barH = 8f; fontSize = 11f;
                    return;
                case HudSizePreset.Medium:
                    panelW = 300f; panelH = 76f; barH = 10f; fontSize = 14f;
                    return;
                case HudSizePreset.Large:
                    panelW = 360f; panelH = 90f; barH = 12f; fontSize = 17f;
                    return;
                case HudSizePreset.ExtraLarge:
                    panelW = 440f; panelH = 108f; barH = 14f; fontSize = 20f;
                    return;
                default:
                    panelW = 360f; panelH = 90f; barH = 12f; fontSize = 17f;
                    return;
            }
        }

        /// <summary>回暖来源键转显示名。0.2.0 起仅保留 Hit/Bugle/Campfire 事件型；Reserve/AutoRefill 在状态行单独显示。</summary>
        private static string GetRestoreSourceName(string key, bool zh)
        {
            switch (key)
            {
                case "Hit":      return zh ? "击杀" : "Kill";
                case "Bugle":    return zh ? "号角" : "Bugle";
                case "Campfire": return zh ? "篝火" : "Fire";
                default:         return key;
            }
        }
    }
}
