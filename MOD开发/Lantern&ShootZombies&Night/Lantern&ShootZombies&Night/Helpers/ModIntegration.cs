using System;
using UnityEngine;

namespace Lantern_ShootZombies_Night
{
    /// <summary>
    /// 前置 MOD 统一适配层：封装 BPR/SZ 反射交互，对外暴露简洁 API。
    /// 各模块通过 ModIntegration 访问前置 MOD 功能，无需了解反射细节。
    /// 
    /// 初始化顺序：Plugin.Awake() → ModIntegration.Initialize()
    /// （在 ReflectionCache.Initialize() 之后调用）
    /// </summary>
    internal static class ModIntegration
    {
        // ═══════════════════════════════════════════════════════════
        //  加载状态
        // ═══════════════════════════════════════════════════════════

        /// <summary>BlackPeakRemix 是否已加载。</summary>
        public static bool IsBprLoaded { get; private set; }

        /// <summary>ShootZombies 是否已加载。</summary>
        public static bool IsSzLoaded { get; private set; }

        // ── BPR SyncHelper 组件缓存（避免每次 GetComponent 查找）──
        private static Component _cachedSyncHelper;
        private static int _cachedSyncHelperItemId = -1;

        /// <summary>
        /// 初始化加载状态。在 Plugin.Awake() 中、ReflectionCache.Initialize() 之后调用。
        /// </summary>
        public static void Initialize()
        {
            IsBprLoaded = BepInEx.Bootstrap.Chainloader.PluginInfos
                .ContainsKey("HnskNoah.BlackPeakRemix");
            IsSzLoaded = BepInEx.Bootstrap.Chainloader.PluginInfos
                .ContainsKey("com.github.Thanks.ShootZombies");

            Plugin.Log?.LogInfo($"[ModIntegration] Initialized: BPR={IsBprLoaded}, SZ={IsSzLoaded}");
        }

        // ═══════════════════════════════════════════════════════════
        //  BlackPeakRemix 适配
        // ═══════════════════════════════════════════════════════════

        /// <summary>
        /// 检查灯笼物品是否处于 BPR 手电筒模式。
        /// 返回 true 表示 LanternSyncHelper.localInstanceMode == Flashlight (byte 1)。
        /// </summary>
        public static bool IsFlashlightActive(Item lanternItem)
        {
            if (!IsBprLoaded || lanternItem == null) return false;
            if (ReflectionCache.BprSyncHelperType == null ||
                ReflectionCache.BprSyncHelperModeField == null) return false;

            // 组件缓存（按物品 InstanceID 判断失效）
            int itemId = lanternItem.GetInstanceID();
            if (itemId != _cachedSyncHelperItemId)
            {
                _cachedSyncHelper = lanternItem.GetComponent(ReflectionCache.BprSyncHelperType);
                _cachedSyncHelperItemId = itemId;
            }

            if (_cachedSyncHelper == null) return false;

            try
            {
                object modeValue = ReflectionCache.BprSyncHelperModeField.GetValue(_cachedSyncHelper);
                if (modeValue == null) return false;
                // LanternState 枚举 (byte): Omni=0, Flashlight=1
                byte modeAsByte = (byte)Convert.ChangeType(modeValue, typeof(byte));
                return modeAsByte == 1;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 获取给定物品上的 BPR SyncHelper 组件（可能为 null）。
        /// 内部带缓存，按物品 InstanceID 刷新。
        /// </summary>
        public static Component GetSyncHelper(Item item)
        {
            if (!IsBprLoaded || item == null ||
                ReflectionCache.BprSyncHelperType == null) return null;

            int itemId = item.GetInstanceID();
            if (itemId != _cachedSyncHelperItemId)
            {
                _cachedSyncHelper = item.GetComponent(ReflectionCache.BprSyncHelperType);
                _cachedSyncHelperItemId = itemId;
            }
            return _cachedSyncHelper;
        }

        /// <summary>清除 SyncHelper 组件缓存（物品切换时调用）。</summary>
        public static void ClearSyncHelperCache()
        {
            _cachedSyncHelper = null;
            _cachedSyncHelperItemId = -1;
        }

        /// <summary>
        /// 读取 BPR_EnvironmentController.CurrentWeight（静态属性）。
        /// 返回 0 表示无黑暗，>0 表示 BPR 黑暗权重。
        /// </summary>
        public static float GetBprDarknessWeight()
        {
            if (!IsBprLoaded || ReflectionCache.BprCurrentWeightProp == null) return 0f;
            try
            {
                return (float)ReflectionCache.BprCurrentWeightProp.GetValue(null);
            }
            catch
            {
                return 0f;
            }
        }

        /// <summary>
        /// 读取 BPR_EnvironmentController.IsExtremeDarkMode（静态属性）。
        /// </summary>
        public static bool IsBprExtremeDark()
        {
            if (!IsBprLoaded || ReflectionCache.BprIsExtremeDarkProp == null) return false;
            try
            {
                return (bool)ReflectionCache.BprIsExtremeDarkProp.GetValue(null);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 综合判断 BPR 是否进入黑暗状态（CurrentWeight > 0 或 IsExtremeDark）。
        /// 供 DayNightTracker 等模块直接调用。
        /// </summary>
        public static bool IsBprDark()
        {
            if (!IsBprLoaded) return false;
            float weight = GetBprDarknessWeight();
            if (weight > 0f) return true;
            return IsBprExtremeDark();
        }

        // ═══════════════════════════════════════════════════════════
        //  ShootZombies 适配
        // ═══════════════════════════════════════════════════════════

        /// <summary>
        /// 检查 NightColdEnabled 配置值（ShootZombies 1.2.x 或 1.3+ Fog&ColdControl）。
        /// 返回 true = 夜间寒冷已启用（正常），false = 被关闭。
        /// 反射未拿到时默认返回 true（假设启用）。
        /// </summary>
        public static bool IsSzNightColdEnabled()
        {
            // 不再看 IsSzLoaded —— 1.3+ 配置在 FogClimb 里，SZ 可能没加载
            if (ReflectionCache.SzNightColdProp == null ||
                ReflectionCache.SzConfigValueGetter == null) return true;

            try
            {
                object configEntry = ReflectionCache.SzNightColdProp.GetValue(null);
                if (configEntry == null) return true;
                return (bool)ReflectionCache.SzConfigValueGetter.Invoke(configEntry, null);
            }
            catch
            {
                return true;
            }
        }

        /// <summary>NightColdEnabled 反射是否已就绪（SZ 1.2.x 或 FogClimb 中任一找到）。</summary>
        public static bool IsNightColdReflectionReady =>
            ReflectionCache.SzNightColdProp != null &&
            ReflectionCache.SzConfigValueGetter != null;
    }
}
