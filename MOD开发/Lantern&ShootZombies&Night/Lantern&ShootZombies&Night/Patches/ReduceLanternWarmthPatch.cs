using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace Lantern_ShootZombies_Night.Patches
{
    /// <summary>
    /// [调试探针] 反射读 Thanks.Fog&amp;ColdControl 的运行时状态，
    /// 用于排查"开了但迷雾寒冷仍没拦住"之类的问题。
    /// 兼容 Thanks 1.0.1.x 和 1.0.3.x（字段改名）。
    /// 运行时目标类：FogColdControl.Plugin（未装则忽略）。
    ///
    /// 注意：1.0.3 起 depth 语义变为"仅标记是否在 SetSharderVars 执行期间"，
    /// depth>0 不再等同于"Thanks 拦冷生效"，要看 FogSup 实际值。
    /// </summary>
    internal static class ThanksFogColdControlProbe
    {
        private static bool _initialized = false;
        private static bool _available = false;
        private static Type _pluginType;
        private static PropertyInfo _modEnabledProp;
        private static PropertyInfo _fogSuppressProp;
        private static PropertyInfo _nightColdProp;
        private static FieldInfo _depthField;
        private static string _depthFieldName = "?";
        private static string _detectedVersion = "?";
    
        private static void TryInit()
        {
            if (_initialized) return;
            _initialized = true;
            try
            {
                _pluginType = AccessTools.TypeByName("FogColdControl.Plugin");
                if (_pluginType == null) return;
                var flags = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;
                _modEnabledProp = _pluginType.GetProperty("ModEnabled", flags);
                _fogSuppressProp = _pluginType.GetProperty("FogColdSuppression", flags);
                _nightColdProp = _pluginType.GetProperty("NightColdEnabled", flags);
    
                // 新名优先（1.0.3+），其次旧名（1.0.1.x）
                _depthField = _pluginType.GetField("_localFogStatusSourceDepth", BindingFlags.Static | BindingFlags.NonPublic);
                if (_depthField != null) { _depthFieldName = "Source(v1.0.3+)"; }
                else
                {
                    _depthField = _pluginType.GetField("_localFogStatusSuppressionDepth", BindingFlags.Static | BindingFlags.NonPublic);
                    if (_depthField != null) _depthFieldName = "Suppression(v1.0.1.x)";
                }
    
                // 读 assembly 版本号
                try { _detectedVersion = _pluginType.Assembly.GetName().Version?.ToString() ?? "?"; }
                catch { _detectedVersion = "?"; }
    
                _available = (_fogSuppressProp != null) && (_depthField != null);
            }
            catch { _available = false; }
        }
    
        public static bool IsInstalled { get { TryInit(); return _available; } }
    
        public static string Snapshot()
        {
            TryInit();
            if (!_available) return "NotInstalled";
            try
            {
                bool modVal = ReadConfigBool(_modEnabledProp?.GetValue(null));
                bool supVal = ReadConfigBool(_fogSuppressProp?.GetValue(null));
                // NightColdEnabled 语义：true=允许夜冷（不拦），false=拦夜冷
                // 探针只如实上报，语义由日志阅读者判断
                string nightVal = _nightColdProp != null
                    ? ReadConfigBool(_nightColdProp.GetValue(null)).ToString()
                    : "N/A";
                int depth = (int)(_depthField.GetValue(null) ?? 0);
                return $"v{_detectedVersion} ModEn={modVal}, FogSup={supVal}, NightCold={nightVal}, depth={depth}({_depthFieldName})";
            }
            catch (Exception ex) { return $"probeErr:{ex.Message}"; }
        }
    
        private static bool ReadConfigBool(object configEntry)
        {
            if (configEntry == null) return false;
            try
            {
                var valueProp = configEntry.GetType().GetProperty("Value");
                return (bool)(valueProp?.GetValue(configEntry) ?? false);
            }
            catch { return false; }
        }
    }

    /// <summary>
    /// 暖值倍率——机制一：拦截 CharacterAfflictions.AddStatus，
    /// 当 statusType 为 Cold 时按 (1 - Clamp01(multiplier)) 缩减受冷量。
    /// 仅在：夜晚 + 持点灯 + EnableWarmthReduction 开启 时生效。
    ///
    /// 机制二、三：灯笼热发射和场景暖区原本会驱散寒冷，
    /// 其总率远超受冷率导致中间倍率无效，因此功能开启时完全禁用（乘以0）。
    ///
    ///   multiplier=1   → 受冷×0   → 灯完全抵御寒冷
    ///   multiplier=0.5 → 受冷×0.5 → 半速受冷
    ///   multiplier=0   → 受冷×1   → 灯对寒冷无影响
    ///
    /// 注：PEAK 本体 FogSphere.SetSharderVars 在跑出雾圈时每帧会
    /// AddStatus(Cold, 0.0105*dt)，属于游戏原设计（迷雾惩罚），
    /// LSN 不额外处理——Thanks.Fog&amp;ColdControl 的 FogColdSuppression
    /// 也只拦 Fog.MakePlayerCold 这一条，FogSphere 路径两边都不管。
    /// </summary>
    /// <summary>
    /// 调试探针传递用 state。Sampled=false 表示本次不采样，Postfix 立即返回。
    /// </summary>
    internal struct ColdProbeState
    {
        public bool Sampled;
        public string Src;
        public float OriginalAmount;
        public float ReducedAmount;
        public float BeforeColdVal;
        public float Multiplier;
    }
    
    [HarmonyPatch(typeof(CharacterAfflictions), "AddStatus",
        new[] { typeof(CharacterAfflictions.STATUSTYPE), typeof(float), typeof(bool), typeof(bool), typeof(bool) })]
    internal class ReduceLanternWarmthPatch
    {
        private static float _logTimer = 10f;
    
        [HarmonyPrefix]
        public static void Prefix(CharacterAfflictions __instance,
            CharacterAfflictions.STATUSTYPE statusType,
            ref float amount,
            out ColdProbeState __state)
        {
            __state = default;
    
            if (statusType != CharacterAfflictions.STATUSTYPE.Cold) return;
    
            Character character = __instance.character;
            if (character == null || !character.IsLocal) return;
    
            // 夜冷/其他冷源：沿用原条件——夜晚 + 持点灯
            if (!Plugin.EnableWarmthReduction.Value) return;
            if (DayNightTracker.IsDaytime) return;
            if (LanternHelper.FindLitLanternSlot(character) == null) return;
    
            float multiplier = Plugin.LanternWarmthMultiplier.Value;
            float factor = Mathf.Clamp01(1f - multiplier);
            float original = amount;
            amount *= factor;
    
            // 记录采样点：Postfix 读 coldVal 差值以验证原方法是否真的执行
            TryBeginSample(__instance, original, multiplier, "Night", out __state);
            if (__state.Sampled) __state.ReducedAmount = amount;
        }
    
        private static void TryBeginSample(CharacterAfflictions instance, float original, float multiplier, string src, out ColdProbeState state)
        {
            state = default;
            // 简单节流：累计 10s 才允许一次采样，避免每帧读 coldVal
            if (_logTimer < 10f) return;
            state.Sampled = true;
            state.Src = src;
            state.OriginalAmount = original;
            state.ReducedAmount = original;
            state.Multiplier = multiplier;
            state.BeforeColdVal = instance.GetCurrentStatus(CharacterAfflictions.STATUSTYPE.Cold);
        }
    
        [HarmonyPostfix]
        public static void Postfix(CharacterAfflictions __instance, ColdProbeState __state)
        {
            _logTimer += Time.deltaTime;
            if (!__state.Sampled) return;
    
            _logTimer = 0f;
            float afterCold = __instance.GetCurrentStatus(CharacterAfflictions.STATUSTYPE.Cold);
            float delta = afterCold - __state.BeforeColdVal;
            // delta ≈ 0      → 原方法被拦（Thanks 或其他补丁 return false）
            // delta ≈ reduced → 原方法如期执行，我们的缩减生效
            // delta ≈ original → 我们的缩减被别人覆盖了
            // 阈值放松到 1e-4：orig 本身就只有 0.0001 量级，太严容易误判 BLOCKED
            const float EPS = 1e-4f;
            string verdict;
            if (Mathf.Abs(delta) < EPS) verdict = "BLOCKED";
            else if (Mathf.Abs(delta - __state.ReducedAmount) < EPS) verdict = "REDUCED_OK";
            else if (Mathf.Abs(delta - __state.OriginalAmount) < EPS) verdict = "FULL_NOT_REDUCED";
            else verdict = "PARTIAL";
    
            string thanks = ThanksFogColdControlProbe.Snapshot();
            Plugin.Log?.LogInfo($"[DEBUG] [WarmthReduction] src={__state.Src}, mul={__state.Multiplier:F2}, orig={__state.OriginalAmount:F4} → reduced={__state.ReducedAmount:F4}, actualDelta={delta:F4}, verdict={verdict}, coldVal={afterCold:F3}, Thanks=[{thanks}]");
        }
    }

    /// <summary>
    /// 暖值倍率——机制二（标记层）：标记 CharacterHeatEmission.Update 期间的来源，
    /// 供 SubtractColdMonitorPatch 精确识别本地玩家灯笼的热发射并将其归零。
    /// 功能开启时热发射完全禁用，只由机制一控制受冷程度。
    /// 队友仍能正常获得你灯笼的温暖。
    /// </summary>
    [HarmonyPatch(typeof(CharacterHeatEmission), "Update")]
    internal class HeatEmissionMultiplierPatch
    {
        private static readonly FieldInfo _characterField =
            AccessTools.Field(typeof(CharacterHeatEmission), "character");

        internal static bool InsideHeatEmission = false;
        internal static bool IsLocalSource = false;

        [HarmonyPrefix]
        public static void Prefix(CharacterHeatEmission __instance)
        {
            InsideHeatEmission = true;
            IsLocalSource = false;

            if (!Plugin.EnableWarmthReduction.Value) return;
            if (DayNightTracker.IsDaytime) return; // 白天不干预抗寒

            Character character = _characterField?.GetValue(__instance) as Character;
            if (character == null || !character.IsLocal) return;

            // 仅当玩家持有点燃的普通灯笼时才封堵热发射，
            // 治疗灯 (Lantern_Faerie) 和奶白金 (HealingDart) 等物品的回暖不受影响
            if (LanternHelper.FindLitLanternSlot(character) == null) return;

            IsLocalSource = true;
        }

        [HarmonyPostfix]
        public static void Postfix()
        {
            InsideHeatEmission = false;
            IsLocalSource = false;
        }
    }

    /// <summary>
    /// 暖值倍率——机制二（执行层）：拦截 SubtractStatus(Cold)，
    /// 当来源为本地玩家灯笼的热发射时，将 amount 归零。
    /// 热发射(~0.030/s)远超受冷率，若按 multiplier 缩放会导致中间值无效，
    /// 因此功能开启时完全禁用，只由机制一控制受冷程度。
    /// 在 SubtractStatus 层面精确拦截，队友不受影响。
    /// </summary>
    [HarmonyPatch(typeof(CharacterAfflictions), "SubtractStatus",
        new[] { typeof(CharacterAfflictions.STATUSTYPE), typeof(float), typeof(bool), typeof(bool) })]
    internal class SubtractColdMonitorPatch
    {
        private static float _logTimer = 10f;

        public static void Prefix(CharacterAfflictions __instance,
            CharacterAfflictions.STATUSTYPE statusType,
            ref float amount, bool fromRPC, bool decreasedNaturally)
        {
            if (statusType != CharacterAfflictions.STATUSTYPE.Cold) return;

            Character character = __instance.character;
            if (character == null || !character.IsLocal) return;

            // ── 机制二拦截：HeatEmission 去寒归零 ──
            if (!HeatEmissionMultiplierPatch.InsideHeatEmission
                || !HeatEmissionMultiplierPatch.IsLocalSource
                || !Plugin.EnableWarmthReduction.Value)
                return;
            if (DayNightTracker.IsDaytime) return;

            float original = amount;
            amount = 0f;

            _logTimer += Time.deltaTime;
            if (_logTimer >= 10f)
            {
                _logTimer = 0f;
                float coldVal2 = __instance.GetCurrentStatus(CharacterAfflictions.STATUSTYPE.Cold);
                Plugin.Log?.LogInfo($"[DEBUG] [HeatEmission] BLOCKED: subtract {original:F4} → 0, coldVal={coldVal2:F3}");
            }
        }
    }

    /// <summary>
    /// 暖值倍率——机制三：拦截 StatusField.Update，
    /// StatusField 是场景中的暖区组件，每帧对范围内玩家驱散寒冷。
    /// 其驱散率(~0.024/s)远超受冷率，若按 multiplier 缩放会导致中间值无效。
    /// 功能开启时完全禁用（乘0），只由机制一控制受冷程度。
    /// Prefix/Postfix 模式临时归零 statusAmountPerSecond，不永久修改。
    /// </summary>
    [HarmonyPatch(typeof(StatusField), "Update")]
    internal class StatusFieldColdPatch
    {
        private static float _logTimer = 10f;

        [HarmonyPrefix]
        public static void Prefix(StatusField __instance, out float __state)
        {
            __state = __instance.statusAmountPerSecond;

            // 只拦截 Cold 类型且提供暖意（负值 = 减少寒冷）的 StatusField
            if (__instance.statusType != CharacterAfflictions.STATUSTYPE.Cold) return;
            if (__instance.statusAmountPerSecond >= 0f) return;
            if (!Plugin.EnableWarmthReduction.Value) return;
            if (DayNightTracker.IsDaytime) return; // 白天不干预抗寒

            Character local = Character.localCharacter;
            if (local == null) return;

            // 只有持有点燃灯笼时才缩放，避免影响篝火/避难所等非灯笼暖区
            if (LanternHelper.FindLitLanternSlot(local) == null) return;

            float multiplier = 0f; // 功能开启时完全禁用暖区，只由机制一控制受冷
            __instance.statusAmountPerSecond *= multiplier;

            _logTimer += Time.deltaTime;
            if (_logTimer >= 10f)
            {
                _logTimer = 0f;
                string objName = __instance.gameObject != null ? __instance.gameObject.name : "null";
                Plugin.Log?.LogInfo($"[DEBUG] [StatusField] BLOCKED: obj='{objName}', rate {__state:F4} → 0, radius={__instance.radius:F1}");
            }
        }

        [HarmonyPostfix]
        public static void Postfix(StatusField __instance, float __state)
        {
            __instance.statusAmountPerSecond = __state;
        }
    }
}
