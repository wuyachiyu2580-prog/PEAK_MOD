using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Photon.Pun;
using UnityEngine;

namespace Lantern_ShootZombies_Night.Patches
{
    /// <summary>
    /// ShootZombies 性能修复（瘦身版 · 2026-04）：
    ///
    /// SZ 新版已经自己修了以下两个性能坑：
    ///   - InventoryItemUiPatch.SetItemPostfix 加了 IsBlowgunLike 门禁，非吹箭物品快速退出
    ///   - ItemUIDataPatch.ForceRefreshVisibleUi 改为优先 RefreshTrackedUi() 快速路径，
    ///     FallbackScanVisibleUiOnce 一辈子只跑 1 次
    /// 所以 LSN 原来的 SetItem 脏检测 / ForceRefreshVisibleUi 500ms 节流已失去意义，本次一并删除。
    ///
    /// 当前仅保留一项：
    ///   ZombieDeathPatch 反射消除 — SZ 未修，依然通过 GetField+GetValue 反射读 character.data.dead
    ///   （它俩都是公开字段，完全多余）。替换后每僵尸每帧节省 ~2μs，僵尸多的时候有明显收益。
    /// </summary>
    internal static class ShootZombiesPerformanceFix
    {
        // ─── 僵尸死亡跟踪（替代 SZ 原始 HashSet）───
        private static readonly HashSet<GameObject> _processedZombies = new HashSet<GameObject>();

        // ─── Character 组件缓存（避免每帧 GetComponent）───
        private static readonly Dictionary<int, Character> _zombieCharCache = new Dictionary<int, Character>();

        // ─── ZombieSpawner 反射缓存 ───
        private static MethodInfo _removeZombieMethod;
        private static bool _zombieReflectionInit;

        private static bool _applied;

        /// <summary>应用 ShootZombies 性能补丁。在 Plugin.Awake 中调用。</summary>
        internal static void Apply(Harmony harmony)
        {
            if (_applied) return;

            try
            {
                // ═══ ZombieDeathPatch 反射消除 ═══
                if (IsUpstreamZombieDeathPatchOptimized())
                {
                    Plugin.Log?.LogInfo("[PerfFix] SZ.ZombieDeathPatch already optimized upstream; skip replacement");
                    _applied = true;
                    return;
                }

                var zombieUpdate = AccessTools.Method(typeof(MushroomZombie), "Update");
                var szZombiePostfix = AccessTools.Method(
                    typeof(ShootZombies.ZombieDeathPatch), "Postfix");

                if (zombieUpdate != null && szZombiePostfix != null)
                {
                    harmony.Unpatch(zombieUpdate, szZombiePostfix);
                    harmony.Patch(zombieUpdate,
                        postfix: new HarmonyMethod(
                            AccessTools.Method(typeof(ShootZombiesPerformanceFix),
                                nameof(OptimizedZombiePostfix))));
                    Plugin.Log?.LogInfo("[PerfFix] Replaced SZ.ZombieDeathPatch — removed redundant reflection");
                }
                else
                {
                    Plugin.Log?.LogWarning($"[PerfFix] Cannot replace SZ.ZombieDeathPatch " +
                        $"(update={zombieUpdate != null}, postfix={szZombiePostfix != null})");
                }

                _applied = true;
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogError($"[PerfFix] ShootZombiesPerformanceFix.Apply failed: {ex}");
            }
        }

        // ════════════════════════════════════════════════════════════
        //  优化版僵尸死亡检测 Postfix
        //  原版在每帧 MushroomZombie.Update Postfix 中通过反射读取
        //  Character.data 和 CharacterData.dead —— 两个都是公开字段，
        //  无需 GetField + GetValue 反射链。消除冗余反射后每僵尸每帧节省 ~2μs。
        // ════════════════════════════════════════════════════════════
        private static void OptimizedZombiePostfix(MushroomZombie __instance)
        {
            if (__instance == null) return;

            try
            {
                GameObject go = __instance.gameObject;
                if (go == null || _processedZombies.Contains(go))
                    return;

                // ★ 缓存 Character 组件，避免每帧 GetComponent ★
                int zid = __instance.GetInstanceID();
                Character character;
                if (!_zombieCharCache.TryGetValue(zid, out character) || character == null)
                {
                    character = __instance.GetComponent<Character>();
                    if (character == null) return;
                    _zombieCharCache[zid] = character;
                }

                // ★ 直接访问公开字段，无需反射 ★
                // 原版先用直接访问判断一次，再用反射重做一次 —— 完全冗余
                bool isDead = character.data != null && character.data.dead;

                if (isDead)
                {
                    _processedZombies.Add(go);

                    // 清理 Character 缓存
                    _zombieCharCache.Remove(zid);

                    // 保持原始行为：Add 后立即 Remove（原 SZ 代码的设计逻辑）
                    if (go != null)
                    {
                        _processedZombies.Remove(go);
                        bool destroyedBySpawner = DestroyZombieSafe(go);

                        if (!destroyedBySpawner && PhotonNetwork.IsMasterClient)
                        {
                            PhotonView pv = go.GetComponent<PhotonView>();
                            if (pv != null)
                                PhotonNetwork.Destroy(pv);
                            else
                                PhotonNetwork.Destroy(go);
                        }
                    }
                }
            }
            catch { }
        }

        /// <summary>
        /// 安全调用 ZombieSpawner.RemoveZombie — 该类可能在游戏或 SZ 命名空间中，
        /// 首次调用时通过反射定位方法并缓存。
        /// </summary>
        private static bool IsUpstreamZombieDeathPatchOptimized()
        {
            try
            {
                // ShootZombies 1.3.4+ already caches Character and routes removal through DestroyZombie().
                return AccessTools.Method(typeof(ShootZombies.ZombieSpawner), "DestroyZombie") != null &&
                    AccessTools.Method(typeof(ShootZombies.ZombieDeathPatch), "ClearCaches") != null;
            }
            catch
            {
                return false;
            }
        }

        private static bool DestroyZombieSafe(GameObject go)
        {
            if (!_zombieReflectionInit)
            {
                _zombieReflectionInit = true;
                try
                {
                    foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        var type = asm.GetType("ZombieSpawner")
                            ?? asm.GetType("ShootZombies.ZombieSpawner");
                        if (type != null)
                        {
                            _removeZombieMethod = type.GetMethod("DestroyZombie",
                                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                                ?? type.GetMethod("RemoveZombie",
                                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                            Plugin.Log?.LogInfo($"[PerfFix] ZombieSpawner destroy/remove resolved: {_removeZombieMethod != null}");
                            break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Plugin.Log?.LogWarning($"[PerfFix] DestroyZombieSafe reflection failed: {ex.Message}");
                }
            }

            _removeZombieMethod?.Invoke(null, new object[] { go });
            return _removeZombieMethod != null &&
                string.Equals(_removeZombieMethod.Name, "DestroyZombie", StringComparison.Ordinal);
        }
    }
}
