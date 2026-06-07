using System.Collections.Generic;
using UnityEngine;

namespace PlayersInfo.Helpers
{
    /// <summary>
    /// ItemPrefab → Sprite 缓存。游戏 Item.UIData.icon 是 Texture2D，转 Sprite 成本高，缓存一次复用。
    /// 场景切换时清空（SceneManager.sceneLoaded 触发）。
    /// </summary>
    internal static class IconSpriteCache
    {
        // 以 Item 实例的 InstanceID 作为 key（Item 是 ScriptableObject/MonoBehaviour prefab，InstanceID 稳定）
        private static readonly Dictionary<int, Sprite> _cache = new Dictionary<int, Sprite>();

        public static Sprite Get(Item itemPrefab)
        {
            if (itemPrefab == null) return null;
            int id = itemPrefab.GetInstanceID();
            if (_cache.TryGetValue(id, out Sprite cached) && cached != null) return cached;

            Texture2D tex = null;
            try { tex = itemPrefab.UIData?.GetIcon(); }
            catch { /* UIData.GetIcon 内部访问单例可能空引用，忽略即可 */ }
            if (tex == null)
            {
                try { tex = itemPrefab.UIData?.icon; } catch { }
            }
            if (tex == null) return null;

            Sprite sp = Sprite.Create(tex,
                new Rect(0, 0, tex.width, tex.height),
                new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            sp.name = itemPrefab.name + "_ModIcon";
            // 避免被 Unity 序列化体系报告为场景中的未保存资源，同时使 Destroy 走不走都能安全释放。
            sp.hideFlags = HideFlags.DontSave;
            _cache[id] = sp;
            return sp;
        }

        /// <summary>
        /// 场景切换 / MOD 关闭时调用。不仅清字典，还要 Destroy 每个 Sprite 实例避免资源累积。
        /// Sprite.Create 出来的实例不占用原 Texture，Destroy 后不会破坏游戏原资源。
        /// </summary>
        public static void Clear()
        {
            foreach (var kv in _cache)
            {
                var sp = kv.Value;
                if (sp == null) continue;
                try { Object.Destroy(sp); }
                catch { /* 序列化资源 / 跨场景 Sprite 可能拒绝 Destroy，忽略即可 */ }
            }
            _cache.Clear();
        }
    }
}
