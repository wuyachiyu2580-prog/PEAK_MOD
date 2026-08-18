using System;
using PlayersInfo.Helpers;
using UnityEngine;
using UnityEngine.UI;

namespace PlayersInfo.MonoBehaviours
{
    /// <summary>
    /// 队友物品栏行：主 3 格 + 临时 1 格 + 背包有效容量（最多 4 格）。
    /// 自建 Image，不克隆原版 InventoryItemUI（HUDBuddy 克隆时回收 icon 导致消失）。
    /// 图标通过 IconSpriteCache 一次性把 Texture2D 转 Sprite 并缓存。
    /// 背包内部格子仅在 backpackSlot.IsEmpty() == false 时可见。
    /// </summary>
    internal class TeammateInventoryRow : MonoBehaviour
    {
        public Character Target;

        private const int MainSlotCount = 3;
        private const int BackpackInnerCount = 4;
        private const int TotalSlots = MainSlotCount + 1 /*temp*/ + BackpackInnerCount;

        // 每个槽的可视元素
        private struct SlotView
        {
            public GameObject root;
            public Image bg;
            public Outline selectedOutline;
            public Image icon;
            public Image durabilityTrack;
            public Image durabilityFill;
            public int lastPrefabId;   // 只有变化时才重新取 Sprite
            public int lastCookedAmount;
            public float lastDurability;
            public bool hidden;
        }

        private readonly SlotView[] _slots = new SlotView[TotalSlots];
        private bool _showJetpackFuel;
        private GameObject _jetpackFuelRoot;
        private Image _jetpackFuelTrack;
        private Image _jetpackFuelFill;
        private float _lastJetpackFuel = -1f;
        private float _rowWidth;
        private float _nextRefreshTime;
        private const float RefreshInterval = 0.15f; // 降频刷新，减少 GC
        private const float JetpackFuelOffsetX = 122f;
        private const float JetpackFuelWidth = 320f;

        private static readonly Color BgEmpty = new Color(0f, 0f, 0f, 0.45f);
        private static readonly Color BgFilled = new Color(0f, 0f, 0f, 0.6f);
        private static readonly Color BgBackpackInner = new Color(0.2f, 0.15f, 0.05f, 0.55f);

        public static TeammateInventoryRow Build(RectTransform parent, float totalWidth, float height,
            float spacing, bool showJetpackFuel)
        {
            var go = new GameObject("InventoryRow", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0.5f);
            rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.sizeDelta = new Vector2(totalWidth, height);

            var c = go.AddComponent<TeammateInventoryRow>();
            c._showJetpackFuel = showJetpackFuel;
            c._rowWidth = totalWidth;
            c.BuildSlots(rt, totalWidth, height, spacing);
            if (showJetpackFuel) c.BuildJetpackFuelBar(rt);
            return c;
        }

        private void BuildSlots(RectTransform root, float totalWidth, float height, float spacing)
        {
            // 布局：N 格横向均分，主3 后留一点间隙，背包槽后也留一点（视觉分组）
            float slotSize = height; // 正方形格
            float contentWidth = TotalSlots * slotSize + (TotalSlots - 1) * spacing;
            float startX = 0f;
            // 若 contentWidth 超 totalWidth，按比例缩小 slotSize
            if (contentWidth > totalWidth)
            {
                slotSize = (totalWidth - (TotalSlots - 1) * spacing) / TotalSlots;
                slotSize = Mathf.Max(slotSize, 12f);
            }
            for (int i = 0; i < TotalSlots; i++)
            {
                var slotGo = new GameObject($"Slot{i}", typeof(RectTransform));
                slotGo.transform.SetParent(root, false);
                var srt = slotGo.GetComponent<RectTransform>();
                srt.anchorMin = new Vector2(0f, 0.5f);
                srt.anchorMax = new Vector2(0f, 0.5f);
                srt.pivot = new Vector2(0f, 0.5f);
                srt.sizeDelta = new Vector2(slotSize, slotSize);
                srt.anchoredPosition = new Vector2(startX + i * (slotSize + spacing), 0f);

                var bgGo = new GameObject("Bg", typeof(RectTransform));
                bgGo.transform.SetParent(srt, false);
                var bgImg = bgGo.AddComponent<Image>();
                bgImg.color = BgEmpty;
                bgImg.raycastTarget = false;
                var brt = bgImg.rectTransform;
                brt.anchorMin = Vector2.zero;
                brt.anchorMax = Vector2.one;
                brt.offsetMin = Vector2.zero;
                brt.offsetMax = Vector2.zero;
                var outline = bgGo.AddComponent<Outline>();
                outline.effectColor = new Color(1f, 0.9f, 0.25f, 0.95f);
                outline.effectDistance = new Vector2(1.5f, 1.5f);
                outline.useGraphicAlpha = false;
                outline.enabled = false;

                var iconGo = new GameObject("Icon", typeof(RectTransform));
                iconGo.transform.SetParent(srt, false);
                var iconImg = iconGo.AddComponent<Image>();
                iconImg.raycastTarget = false;
                iconImg.preserveAspect = true;
                iconImg.enabled = false;
                var irt = iconImg.rectTransform;
                irt.anchorMin = new Vector2(0.1f, 0.16f);
                irt.anchorMax = new Vector2(0.9f, 0.92f);
                irt.offsetMin = Vector2.zero;
                irt.offsetMax = Vector2.zero;

                var trackGo = new GameObject("Durability", typeof(RectTransform));
                trackGo.transform.SetParent(srt, false);
                trackGo.transform.SetAsFirstSibling();
                var trackRt = trackGo.GetComponent<RectTransform>();
                trackRt.anchorMin = new Vector2(0f, 0f);
                trackRt.anchorMax = new Vector2(1f, 0f);
                trackRt.pivot = new Vector2(0.5f, 0f);
                trackRt.offsetMin = new Vector2(2f, 1f);
                trackRt.offsetMax = new Vector2(-2f, 0f);
                trackRt.sizeDelta = new Vector2(0f, 3f);
                var trackImg = trackGo.AddComponent<Image>();
                trackImg.sprite = IconSpriteCache.GetWhiteSprite();
                trackImg.type = Image.Type.Simple;
                trackImg.color = new Color(0f, 0f, 0f, 0.65f);
                trackImg.raycastTarget = false;

                var fillGo = new GameObject("Fill", typeof(RectTransform));
                fillGo.transform.SetParent(trackGo.transform, false);
                var fillRt = fillGo.GetComponent<RectTransform>();
                fillRt.anchorMin = Vector2.zero;
                fillRt.anchorMax = Vector2.one;
                fillRt.offsetMin = Vector2.zero;
                fillRt.offsetMax = Vector2.zero;
                var fillImg = fillGo.AddComponent<Image>();
                fillImg.sprite = IconSpriteCache.GetWhiteSprite();
                fillImg.type = Image.Type.Filled;
                fillImg.fillMethod = Image.FillMethod.Horizontal;
                fillImg.fillOrigin = 0;
                fillImg.fillAmount = 1f;
                fillImg.color = Color.green;
                fillImg.raycastTarget = false;
                trackGo.SetActive(false);

                _slots[i] = new SlotView
                {
                    root = slotGo,
                    bg = bgImg,
                    selectedOutline = outline,
                    icon = iconImg,
                    durabilityTrack = trackImg,
                    durabilityFill = fillImg,
                    lastPrefabId = 0,
                    lastCookedAmount = int.MinValue,
                    lastDurability = -1f,
                    hidden = false
                };
            }
        }

        private void BuildJetpackFuelBar(RectTransform root)
        {
            var fuelGo = new GameObject("JetpackFuel", typeof(RectTransform));
            fuelGo.transform.SetParent(root, false);
            var fuelRt = fuelGo.GetComponent<RectTransform>();
            fuelRt.anchorMin = fuelRt.anchorMax = new Vector2(0f, 0.5f);
            fuelRt.pivot = new Vector2(0f, 0.5f);
            fuelRt.sizeDelta = new Vector2(Mathf.Min(JetpackFuelWidth,
                Mathf.Max(20f, _rowWidth - JetpackFuelOffsetX)), 14f);
            fuelRt.anchoredPosition = new Vector2(JetpackFuelOffsetX, 0f);

            _jetpackFuelTrack = fuelGo.AddComponent<Image>();
            _jetpackFuelTrack.sprite = IconSpriteCache.GetWhiteSprite();
            _jetpackFuelTrack.color = new Color(0.06f, 0.08f, 0.05f, 0.94f);
            _jetpackFuelTrack.raycastTarget = false;
            var outline = fuelGo.AddComponent<Outline>();
            outline.effectColor = new Color(0.88f, 0.84f, 0.65f, 0.95f);
            outline.effectDistance = new Vector2(1.5f, 1.5f);
            outline.useGraphicAlpha = false;

            var fillGo = new GameObject("Fill", typeof(RectTransform));
            fillGo.transform.SetParent(fuelGo.transform, false);
            var fillRt = fillGo.GetComponent<RectTransform>();
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = Vector2.one;
            fillRt.offsetMin = new Vector2(2f, 2f);
            fillRt.offsetMax = new Vector2(-2f, -2f);
            _jetpackFuelFill = fillGo.AddComponent<Image>();
            _jetpackFuelFill.sprite = IconSpriteCache.GetWhiteSprite();
            _jetpackFuelFill.type = Image.Type.Filled;
            _jetpackFuelFill.fillMethod = Image.FillMethod.Horizontal;
            _jetpackFuelFill.fillOrigin = 0;
            _jetpackFuelFill.fillAmount = 1f;
            _jetpackFuelFill.raycastTarget = false;

            _jetpackFuelRoot = fuelGo;
            fuelGo.SetActive(false);
        }

        private void LateUpdate()
        {
            if (Time.unscaledTime < _nextRefreshTime) return;
            _nextRefreshTime = Time.unscaledTime + RefreshInterval;
            try { RefreshAll(); }
            catch (Exception ex) { PluginLogger.ThrottleError("inv_refresh", "Inventory refresh failed: " + ex.Message); }
        }

        private void RefreshAll()
        {
            if (Target == null || Target.Equals(null) || Target.player == null)
            {
                ClearAllSlots();
                return;
            }
            var p = Target.player;

            // 主 3 格
            for (int i = 0; i < MainSlotCount; i++)
            {
                ItemSlot s = null;
                try { s = (p.itemSlots != null && i < p.itemSlots.Length) ? p.itemSlots[i] : null; } catch { }
                UpdateSlot(i, s, BgEmpty, BgFilled);
            }

            // 临时格
            ItemSlot temp = null;
            try { temp = p.tempFullSlot; } catch { }
            UpdateSlot(MainSlotCount, temp, BgEmpty, BgFilled);

            var bpSlot = SafeGetBackpackSlot(p);
            bool hasBackpack = bpSlot != null && !bpSlot.IsEmpty();
            int backpackSlotCount = hasBackpack ? GetBackpackSlotCount(bpSlot) : 0;

            // BackpackData 固定保存 4 格，但原版用 slotCount 控制实际容量。
            for (int j = 0; j < BackpackInnerCount; j++)
            {
                int idx = MainSlotCount + 1 + j;
                ItemSlot inner = null;
                bool slotAvailable = hasBackpack && j < backpackSlotCount;
                if (slotAvailable && bpSlot.data != null)
                {
                    try
                    {
                        if (bpSlot.data.TryGetDataEntry<BackpackData>(DataEntryKey.BackpackData, out var bpData)
                            && bpData != null && bpData.itemSlots != null && j < bpData.itemSlots.Length)
                        {
                            inner = bpData.itemSlots[j];
                        }
                    }
                    catch { /* 某些客户端可能还没同步完 */ }
                }
                SetSlotVisible(idx, slotAvailable);
                if (slotAvailable) UpdateSlot(idx, inner, BgBackpackInner, BgBackpackInner);
            }

            UpdateJetpackFuel(bpSlot);
        }

        private BackpackSlot SafeGetBackpackSlot(Player p)
        {
            try { return p.backpackSlot; } catch { return null; }
        }

        private int GetBackpackSlotCount(BackpackSlot slot)
        {
            if (slot == null || slot.IsEmpty()) return 0;
            try
            {
                if (slot.prefab is Backpack backpack)
                    return Mathf.Clamp(backpack.slotCount, 0, BackpackInnerCount);
            }
            catch { }
            try
            {
                if (Target != null && Target.refs != null && Target.refs.backpackHandler != null)
                {
                    var visuals = Target.refs.backpackHandler.activeBackpackVisuals;
                    if (visuals != null)
                        return Mathf.Clamp(visuals.slotCount, 0, BackpackInnerCount);
                }
            }
            catch { }

            switch (slot.backpackType)
            {
                case BackpackSlot.BackpackType.Backpack: return 4;
                case BackpackSlot.BackpackType.Fannypack: return 2;
                default: return 0;
            }
        }

        private void UpdateJetpackFuel(BackpackSlot slot)
        {
            if (!_showJetpackFuel || _jetpackFuelRoot == null)
                return;

            float fuel = -1f;
            bool isJetpack = slot != null && !slot.IsEmpty()
                && slot.backpackType == BackpackSlot.BackpackType.Jetpack;
            if (isJetpack && slot.data != null)
            {
                try
                {
                    if (slot.data.TryGetDataEntry<FloatItemData>(DataEntryKey.UseRemainingPercentage, out var useData)
                        && useData != null)
                    {
                        fuel = Mathf.Clamp01(useData.Value);
                    }
                    else if (slot.data.TryGetDataEntry<FloatItemData>(DataEntryKey.Fuel, out var fuelData)
                        && fuelData != null)
                    {
                        fuel = Mathf.Clamp01(fuelData.Value / 100f);
                    }
                }
                catch { fuel = -1f; }
            }

            bool visible = fuel >= 0f;
            if (_jetpackFuelRoot.activeSelf != visible)
                _jetpackFuelRoot.SetActive(visible);
            if (!visible)
            {
                _lastJetpackFuel = -1f;
                return;
            }
            if (Mathf.Abs(_lastJetpackFuel - fuel) < 0.001f) return;

            _lastJetpackFuel = fuel;
            if (_jetpackFuelTrack != null)
                _jetpackFuelTrack.color = fuel <= 0.001f
                    ? new Color(0.32f, 0.03f, 0.02f, 0.92f)
                    : new Color(0.02f, 0.04f, 0.05f, 0.88f);
            if (_jetpackFuelFill != null)
            {
                _jetpackFuelFill.fillAmount = fuel;
                _jetpackFuelFill.color = fuel <= 0.2f
                    ? new Color(1f, 0.24f, 0.12f)
                    : fuel <= 0.5f
                        ? new Color(1f, 0.78f, 0.12f)
                        : new Color(0.45f, 1f, 0.25f);
            }
        }

        private void ClearAllSlots()
        {
            for (int i = 0; i < _slots.Length; i++)
            {
                var sv = _slots[i];
                if (sv.icon != null) { sv.icon.enabled = false; sv.icon.sprite = null; }
                if (sv.bg != null) sv.bg.color = BgEmpty;
                if (sv.selectedOutline != null) sv.selectedOutline.enabled = false;
                if (sv.durabilityTrack != null && sv.durabilityTrack.gameObject.activeSelf)
                    sv.durabilityTrack.gameObject.SetActive(false);
                if (sv.durabilityFill != null) sv.durabilityFill.fillAmount = 1f;
                sv.lastPrefabId = 0;
                sv.lastCookedAmount = int.MinValue;
                sv.lastDurability = -1f;
                _slots[i] = sv;
            }
            if (_jetpackFuelRoot != null && _jetpackFuelRoot.activeSelf)
                _jetpackFuelRoot.SetActive(false);
            _lastJetpackFuel = -1f;
        }

        private void SetSlotVisible(int idx, bool visible)
        {
            if (idx < 0 || idx >= _slots.Length) return;
            var sv = _slots[idx];
            if (sv.root == null) return;
            if (sv.hidden == !visible) return;
            sv.hidden = !visible;
            sv.root.SetActive(visible);
            _slots[idx] = sv;
        }

        private void UpdateSlot(int idx, ItemSlot slot, Color emptyBg, Color filledBg)
        {
            if (idx < 0 || idx >= _slots.Length) return;
            var sv = _slots[idx];
            if (sv.root == null) return;

            bool empty = slot == null || slot.IsEmpty() || slot.prefab == null;
            Color desiredBg = empty ? emptyBg : filledBg;
            if (sv.bg != null && sv.bg.color != desiredBg) sv.bg.color = desiredBg;
            bool selected = !empty && IsSelected(slot);
            if (sv.selectedOutline != null && sv.selectedOutline.enabled != selected)
                sv.selectedOutline.enabled = selected;

            if (empty)
            {
                if (sv.icon != null) { sv.icon.enabled = false; sv.icon.sprite = null; }
                if (sv.icon != null) sv.icon.color = Color.white;
                if (sv.selectedOutline != null) sv.selectedOutline.enabled = false;
                if (sv.durabilityTrack != null && sv.durabilityTrack.gameObject.activeSelf)
                    sv.durabilityTrack.gameObject.SetActive(false);
                if (sv.durabilityFill != null) sv.durabilityFill.fillAmount = 1f;
                sv.lastPrefabId = 0;
                sv.lastCookedAmount = int.MinValue;
                sv.lastDurability = -1f;
                _slots[idx] = sv;
                return;
            }

            int id = slot.prefab.GetInstanceID();
            if (id != sv.lastPrefabId)
            {
                Sprite sp = IconSpriteCache.Get(slot.prefab);
                if (sv.icon != null)
                {
                    sv.icon.sprite = sp;
                    sv.icon.enabled = sp != null;
                    sv.icon.color = Color.white;
                }
                sv.lastPrefabId = id;
            }

            UpdateCookedColor(ref sv, slot);
            UpdateDurability(ref sv, slot);

            _slots[idx] = sv;
        }

        private bool IsSelected(ItemSlot slot)
        {
            try
            {
                return Target != null && Target.refs != null && Target.refs.items != null
                    && Target.refs.items.currentSelectedSlot.IsSome
                    && Target.refs.items.currentSelectedSlot.Value == slot.itemSlotID;
            }
            catch { return false; }
        }

        private static void UpdateCookedColor(ref SlotView sv, ItemSlot slot)
        {
            int cookedAmount = 0;
            bool hasCookedAmount = false;
            try
            {
                if (slot.data != null
                    && slot.data.TryGetDataEntry<IntItemData>(DataEntryKey.CookedAmount, out var cookedData)
                    && cookedData != null)
                {
                    cookedAmount = cookedData.Value;
                    hasCookedAmount = true;
                }
            }
            catch { }

            if (sv.lastCookedAmount == cookedAmount) return;
            sv.lastCookedAmount = cookedAmount;
            if (sv.icon != null)
                sv.icon.color = hasCookedAmount ? ItemCooking.GetCookColor(cookedAmount) : Color.white;
        }

        private static void UpdateDurability(ref SlotView sv, ItemSlot slot)
        {
            bool hiddenByItem = false;
            try { hiddenByItem = slot.prefab.UIData != null && slot.prefab.UIData.hideFuel; } catch { }

            float durability = -1f;
            if (!hiddenByItem && slot.data != null)
            {
                try
                {
                    if (slot.data.TryGetDataEntry<FloatItemData>(DataEntryKey.UseRemainingPercentage, out var useData)
                        && useData != null)
                    {
                        durability = Mathf.Clamp01(useData.Value);
                    }
                    else if (slot.prefab.totalUses > 0
                        && slot.data.TryGetDataEntry<OptionableIntItemData>(DataEntryKey.ItemUses, out var usesData)
                        && usesData != null && usesData.HasData)
                    {
                        durability = Mathf.Clamp01((float)usesData.Value / slot.prefab.totalUses);
                    }
                }
                catch { durability = -1f; }
            }

            if (durability < 0f)
            {
                if (sv.durabilityTrack != null && sv.durabilityTrack.gameObject.activeSelf)
                    sv.durabilityTrack.gameObject.SetActive(false);
                sv.lastDurability = -1f;
                return;
            }

            if (sv.durabilityTrack != null && !sv.durabilityTrack.gameObject.activeSelf)
                sv.durabilityTrack.gameObject.SetActive(true);
            if (Mathf.Abs(sv.lastDurability - durability) < 0.001f) return;

            sv.lastDurability = durability;
            if (sv.durabilityFill == null) return;
            sv.durabilityFill.fillAmount = durability;
            sv.durabilityFill.color = durability <= 0.2f
                ? new Color(1f, 0.18f, 0.12f)
                : durability <= 0.5f
                    ? new Color(1f, 0.78f, 0.12f)
                    : new Color(0.2f, 0.9f, 0.3f);
        }
    }
}
