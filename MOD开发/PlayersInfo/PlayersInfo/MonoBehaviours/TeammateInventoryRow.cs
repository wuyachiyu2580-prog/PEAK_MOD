using System;
using PlayersInfo.Helpers;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PlayersInfo.MonoBehaviours
{
    /// <summary>
    /// 队友物品栏行：主 3 格 + 临时 1 格 + 背包内部 4 格（共 8 格，动态隐藏）。
    /// 自建 Image，不克隆原版 InventoryItemUI（HUDBuddy 克隆时回收 icon 导致消失）。
    /// 图标通过 IconSpriteCache 一次性把 Texture2D 转 Sprite 并缓存。
    /// 背包内部格子仅在 backpackSlot.hasBackpack == true 时可见。
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
            public Image icon;
            public TextMeshProUGUI countText;
            public int lastPrefabId;   // 只有变化时才重新取 Sprite
            public bool hidden;
        }

        private readonly SlotView[] _slots = new SlotView[TotalSlots];
        private float _nextRefreshTime;
        private const float RefreshInterval = 0.15f; // 降频刷新，减少 GC

        private static readonly Color BgEmpty = new Color(0f, 0f, 0f, 0.45f);
        private static readonly Color BgFilled = new Color(0f, 0f, 0f, 0.6f);
        private static readonly Color BgBackpackInner = new Color(0.2f, 0.15f, 0.05f, 0.55f);

        public static TeammateInventoryRow Build(RectTransform parent, float totalWidth, float height, float spacing)
        {
            var go = new GameObject("InventoryRow", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0.5f);
            rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.sizeDelta = new Vector2(totalWidth, height);

            var c = go.AddComponent<TeammateInventoryRow>();
            c.BuildSlots(rt, totalWidth, height, spacing);
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

                var iconGo = new GameObject("Icon", typeof(RectTransform));
                iconGo.transform.SetParent(srt, false);
                var iconImg = iconGo.AddComponent<Image>();
                iconImg.raycastTarget = false;
                iconImg.preserveAspect = true;
                iconImg.enabled = false;
                var irt = iconImg.rectTransform;
                irt.anchorMin = new Vector2(0.1f, 0.1f);
                irt.anchorMax = new Vector2(0.9f, 0.9f);
                irt.offsetMin = Vector2.zero;
                irt.offsetMax = Vector2.zero;

                var countGo = new GameObject("Count", typeof(RectTransform));
                countGo.transform.SetParent(srt, false);
                var countRt = countGo.GetComponent<RectTransform>();
                countRt.anchorMin = Vector2.zero;
                countRt.anchorMax = Vector2.one;
                countRt.offsetMin = new Vector2(0f, 0f);
                countRt.offsetMax = new Vector2(-1f, 0f);
                var countTxt = countGo.AddComponent<TextMeshProUGUI>();
                countTxt.alignment = TextAlignmentOptions.BottomRight;
                countTxt.fontSize = Mathf.Max(8f, slotSize * 0.45f);
                countTxt.fontStyle = FontStyles.Bold;
                countTxt.color = Color.white;
                countTxt.raycastTarget = false;
                countTxt.text = string.Empty;

                _slots[i] = new SlotView
                {
                    root = slotGo,
                    bg = bgImg,
                    icon = iconImg,
                    countText = countTxt,
                    lastPrefabId = 0,
                    hidden = false
                };
            }
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
            bool hasBackpack = bpSlot != null && bpSlot.hasBackpack;

            // 背包内部 4 格：仅 hasBackpack 时可见
            for (int j = 0; j < BackpackInnerCount; j++)
            {
                int idx = MainSlotCount + 1 + j;
                ItemSlot inner = null;
                if (hasBackpack && bpSlot != null && bpSlot.data != null)
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
                SetSlotVisible(idx, hasBackpack);
                if (hasBackpack) UpdateSlot(idx, inner, BgBackpackInner, BgBackpackInner);
            }
        }

        private BackpackSlot SafeGetBackpackSlot(Player p)
        {
            try { return p.backpackSlot; } catch { return null; }
        }

        private void ClearAllSlots()
        {
            for (int i = 0; i < _slots.Length; i++)
            {
                var sv = _slots[i];
                if (sv.icon != null) { sv.icon.enabled = false; sv.icon.sprite = null; }
                if (sv.bg != null) sv.bg.color = BgEmpty;
                if (sv.countText != null) sv.countText.text = string.Empty;
                sv.lastPrefabId = 0;
                _slots[i] = sv;
            }
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
            if (sv.bg != null) sv.bg.color = empty ? emptyBg : filledBg;

            if (empty)
            {
                if (sv.icon != null) { sv.icon.enabled = false; sv.icon.sprite = null; }
                if (sv.countText != null) sv.countText.text = string.Empty;
                sv.lastPrefabId = 0;
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
                }
                sv.lastPrefabId = id;
            }

            // 右下角百分比（对火把/信号弹/煤灯等"使用剩余"物品显示；普通物品不显示）
            string countStr = string.Empty;
            try
            {
                if (slot.data != null
                    && slot.data.TryGetDataEntry<FloatItemData>(DataEntryKey.UseRemainingPercentage, out var useData)
                    && useData != null && useData.Value > 0f && useData.Value < 0.999f)
                {
                    countStr = Mathf.RoundToInt(useData.Value * 100f).ToString() + "%";
                }
            }
            catch { }
            if (sv.countText != null) sv.countText.text = countStr;

            _slots[idx] = sv;
        }
    }
}
