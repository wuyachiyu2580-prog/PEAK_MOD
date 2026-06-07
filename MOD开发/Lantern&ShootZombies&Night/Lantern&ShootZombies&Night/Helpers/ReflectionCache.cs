using System;
using System.Reflection;
using BepInEx.Logging;
using HarmonyLib;

namespace Lantern_ShootZombies_Night
{
    /// <summary>
    /// 全局反射缓存：在 Plugin.Awake() 中一次性初始化所有反射信息，
    /// 替代各模块首次 Tick 时的延迟初始化，避免分散的反射代码和重复查找。
    /// 
    /// 对于热路径字段（Lantern.lit, Lantern.fuel）使用 AccessTools.FieldRefAccess
    /// 生成强类型委托，消除每次访问的装箱/拆箱开销。
    /// </summary>
    internal static class ReflectionCache
    {
        // ═══════════════════════════════════════════════════════════
        //  Lantern 核心字段（热路径 — 使用 FieldRef 强类型委托）
        // ═══════════════════════════════════════════════════════════

        /// <summary>Lantern.lit (private bool) — 每帧多次访问。</summary>
        public static AccessTools.FieldRef<Lantern, bool> LanternLit;

        /// <summary>Lantern.fuel (private float) — 每帧多次读写。</summary>
        public static AccessTools.FieldRef<Lantern, float> LanternFuel;

        /// <summary>FieldInfo 后备：当 FieldRef 不可用时使用（容错）。</summary>
        public static FieldInfo LanternLitField;
        public static FieldInfo LanternFuelField;

        // ═══════════════════════════════════════════════════════════
        //  BlackPeakRemix 反射（可选 — BPR 未加载时全部为 null）
        // ═══════════════════════════════════════════════════════════

        /// <summary>BlackPeakRemix.LanternSyncHelper 类型。</summary>
        public static Type BprSyncHelperType;

        /// <summary>LanternSyncHelper.localInstanceMode 字段。</summary>
        public static FieldInfo BprSyncHelperModeField;

        /// <summary>LanternSyncHelper.Init(Lantern) 方法。</summary>
        public static MethodInfo BprSyncHelperInitMethod;

        /// <summary>LanternState.Omni 枚举值。</summary>
        public static object BprStateOmni;

        /// <summary>LanternState.Flashlight 枚举值。</summary>
        public static object BprStateFlashlight;

        /// <summary>BlackPeakRemix.BlackPeakRemix.Log 字段（internal static）。</summary>
        public static FieldInfo BprLogField;

        /// <summary>BPR_EnvironmentController.CurrentWeight 属性。</summary>
        public static PropertyInfo BprCurrentWeightProp;

        /// <summary>BPR_EnvironmentController.IsExtremeDarkMode 属性。</summary>
        public static PropertyInfo BprIsExtremeDarkProp;

        // ═══════════════════════════════════════════════════════════
        //  ShootZombies 反射（可选 — SZ 未加载时全部为 null）
        // ═══════════════════════════════════════════════════════════

        /// <summary>ShootZombies.Plugin.NightColdEnabled 静态属性。</summary>
        public static PropertyInfo SzNightColdProp;

        /// <summary>ConfigEntry&lt;bool&gt;.Value 的 getter 方法。</summary>
        public static MethodInfo SzConfigValueGetter;

        // ═══════════════════════════════════════════════════════════
        //  游戏内部反射
        // ═══════════════════════════════════════════════════════════

        /// <summary>LocalizedText.CURRENT_LANGUAGE 静态字段。</summary>
        public static FieldInfo LocalizedTextLanguageField;

        private static bool _gameReflectionDone;

        // ═══════════════════════════════════════════════════════════
        //  初始化
        // ═══════════════════════════════════════════════════════════

        /// <summary>
        /// 提前初始化游戏内部反射（LocalizedText 等）。
        /// 在 Plugin.Awake() 中语言检测前调用，因为语言检测需要 LocalizedTextLanguageField。
        /// </summary>
        public static void InitGameReflectionEarly()
        {
            if (_gameReflectionDone) return;
            InitGameReflection(Plugin.Log);
        }

        /// <summary>
        /// 一次性初始化所有反射缓存。在 Plugin.Awake() 中调用。
        /// </summary>
        public static void Initialize(bool bprLoaded, bool szLoaded)
        {
            ManualLogSource log = Plugin.Log;

            // ── Lantern 核心字段 ──
            InitLanternFields(log);

            // ── BPR 反射 ──
            if (bprLoaded)
                InitBprReflection(log);

            // ── ShootZombies 反射 ──
            // 1.3+ 拆分后 NightColdEnabled 可能在 FogClimb，无论 szLoaded 是否为 true 都尝试
            InitSzReflection(log);

            // ── 游戏内部 ──
            InitGameReflection(log);

            log?.LogInfo("[ReflectionCache] Initialization complete");
        }

        private static void InitLanternFields(ManualLogSource log)
        {
            // FieldRef 强类型委托（热路径优化）
            try
            {
                LanternLit = AccessTools.FieldRefAccess<Lantern, bool>("lit");
                log?.LogInfo("[ReflectionCache] Lantern.lit FieldRef OK");
            }
            catch (Exception ex)
            {
                log?.LogWarning($"[ReflectionCache] Lantern.lit FieldRef FAILED: {ex.Message}");
            }

            try
            {
                LanternFuel = AccessTools.FieldRefAccess<Lantern, float>("fuel");
                log?.LogInfo("[ReflectionCache] Lantern.fuel FieldRef OK");
            }
            catch (Exception ex)
            {
                log?.LogWarning($"[ReflectionCache] Lantern.fuel FieldRef FAILED: {ex.Message}");
            }

            // FieldInfo 后备
            LanternLitField = typeof(Lantern).GetField("lit",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            LanternFuelField = typeof(Lantern).GetField("fuel",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        }

        private static void InitBprReflection(ManualLogSource log)
        {
            try
            {
                var bprAsm = typeof(BlackPeakRemix.BlackPeakRemix).Assembly;

                // LanternSyncHelper
                BprSyncHelperType = bprAsm.GetType("BlackPeakRemix.LanternSyncHelper");
                if (BprSyncHelperType != null)
                {
                    BprSyncHelperModeField = BprSyncHelperType.GetField("localInstanceMode",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    BprSyncHelperInitMethod = BprSyncHelperType.GetMethod("Init",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                }

                // LanternState 枚举
                var stateType = bprAsm.GetType("BlackPeakRemix.LanternState");
                if (stateType != null)
                {
                    BprStateOmni = Enum.Parse(stateType, "Omni");
                    BprStateFlashlight = Enum.Parse(stateType, "Flashlight");
                }

                // BPR.Log (internal static)
                BprLogField = typeof(BlackPeakRemix.BlackPeakRemix).GetField("Log",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

                // BPR_EnvironmentController
                Type envType = bprAsm.GetType("BlackPeakRemix.BPR_EnvironmentController");
                if (envType != null)
                {
                    BprCurrentWeightProp = envType.GetProperty("CurrentWeight",
                        BindingFlags.Static | BindingFlags.Public);
                    BprIsExtremeDarkProp = envType.GetProperty("IsExtremeDarkMode",
                        BindingFlags.Static | BindingFlags.Public);
                }

                log?.LogInfo($"[ReflectionCache] BPR: syncHelper={BprSyncHelperType != null}, " +
                    $"mode={BprSyncHelperModeField != null}, init={BprSyncHelperInitMethod != null}, " +
                    $"states={BprStateOmni != null}/{BprStateFlashlight != null}, " +
                    $"env.weight={BprCurrentWeightProp != null}, env.dark={BprIsExtremeDarkProp != null}");
            }
            catch (Exception ex)
            {
                log?.LogWarning($"[ReflectionCache] BPR reflection failed: {ex.Message}");
            }
        }

        private static void InitSzReflection(ManualLogSource log)
        {
            // ShootZombies 1.2.x: NightColdEnabled 在 SZ Plugin 里
            // ShootZombies 1.3.0+: Fog/Cold 分拆到独立 MOD Thanks-FogAndColdControl（GUID: com.github.Thanks.FogClimb）
            // 先试 SZ 本体，fallback 到 FogClimb
            if (TryLoadNightColdFromPlugin("com.github.Thanks.ShootZombies", "SZ", log))
                return;

            TryLoadNightColdFromPlugin("com.github.Thanks.FogClimb", "FogClimb", log);
        }

        /// <summary>尝试从指定 GUID 的 Plugin 反射取 NightColdEnabled。成功返回 true。</summary>
        private static bool TryLoadNightColdFromPlugin(string guid, string tag, ManualLogSource log)
        {
            try
            {
                BepInEx.PluginInfo info;
                if (!BepInEx.Bootstrap.Chainloader.PluginInfos.TryGetValue(guid, out info)
                    || info?.Instance == null)
                {
                    log?.LogInfo($"[ReflectionCache] {tag}: plugin '{guid}' not loaded");
                    return false;
                }

                Type pluginType = info.Instance.GetType();
                PropertyInfo prop = pluginType.GetProperty("NightColdEnabled",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

                if (prop == null)
                {
                    log?.LogInfo($"[ReflectionCache] {tag}: NightColdEnabled property not found on {pluginType.Name}");
                    return false;
                }

                Type ceType = prop.PropertyType;
                PropertyInfo valueProp = ceType.GetProperty("Value",
                    BindingFlags.Instance | BindingFlags.Public);
                MethodInfo getter = valueProp?.GetGetMethod();

                if (getter == null)
                {
                    log?.LogWarning($"[ReflectionCache] {tag}: NightColdEnabled found but Value getter missing");
                    return false;
                }

                SzNightColdProp = prop;
                SzConfigValueGetter = getter;
                log?.LogInfo($"[ReflectionCache] {tag}: NightColdEnabled reflection OK (source={tag})");
                return true;
            }
            catch (Exception ex)
            {
                log?.LogWarning($"[ReflectionCache] {tag}: reflection failed: {ex.Message}");
                return false;
            }
        }

        private static void InitGameReflection(ManualLogSource log)
        {
            if (_gameReflectionDone) return;
            _gameReflectionDone = true;
            try
            {
                Type localizedTextType = typeof(Item).Assembly.GetType("LocalizedText");
                if (localizedTextType != null)
                {
                    LocalizedTextLanguageField = localizedTextType.GetField("CURRENT_LANGUAGE",
                        BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                }
                log?.LogInfo($"[ReflectionCache] Game: LocalizedText={localizedTextType != null}, " +
                    $"langField={LocalizedTextLanguageField != null}");
            }
            catch (Exception ex)
            {
                log?.LogWarning($"[ReflectionCache] Game reflection failed: {ex.Message}");
            }
        }

        // ═══════════════════════════════════════════════════════════
        //  便捷读取方法（带安全检查）
        // ═══════════════════════════════════════════════════════════

        /// <summary>安全读取 Lantern.lit 值。FieldRef 不可用时回退到 FieldInfo。</summary>
        public static bool GetLit(Lantern instance)
        {
            if (instance == null) return false;
            if (LanternLit != null) return LanternLit(instance);
            if (LanternLitField != null) return (bool)(LanternLitField.GetValue(instance) ?? false);
            return false;
        }

        /// <summary>安全设置 Lantern.lit 值。</summary>
        public static void SetLit(Lantern instance, bool value)
        {
            if (instance == null) return;
            if (LanternLit != null) { LanternLit(instance) = value; return; }
            LanternLitField?.SetValue(instance, value);
        }

        /// <summary>安全读取 Lantern.fuel 值。</summary>
        public static float GetFuel(Lantern instance)
        {
            if (instance == null) return 0f;
            if (LanternFuel != null) return LanternFuel(instance);
            if (LanternFuelField != null) return (float)(LanternFuelField.GetValue(instance) ?? 0f);
            return 0f;
        }

        /// <summary>安全设置 Lantern.fuel 值。</summary>
        public static void SetFuel(Lantern instance, float value)
        {
            if (instance == null) return;
            if (LanternFuel != null) { LanternFuel(instance) = value; return; }
            LanternFuelField?.SetValue(instance, value);
        }
    }
}
