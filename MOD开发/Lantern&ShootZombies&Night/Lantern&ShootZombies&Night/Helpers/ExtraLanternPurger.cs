using System;
using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;
using Zorro.Core;

namespace Lantern_ShootZombies_Night
{
    /// <summary>
    /// 软封：本地玩家身上最多保留 1 盏非仙子灯，
    /// 多出的（第 2+ 把）按“临时槽优先 + 燃料最多优先”策略自动销毁。
    ///
    /// 设计：
    /// - 周期 1s 扫描一次，性能忽略
    /// - 检测到多余灯后等 300ms 再销毁（避免刚捡起就爆的体验）
    /// - 仙子灯（Faerie）完全豁免
    /// - 销毁“燃料最多”的那盏：新冒出来的副本通常是满燃料（BPR 重建/拾取），
    ///   玩家手上一直在用的那盏燃料少 —— 保留老灯，销毁新副本，避免反复刷循环
    /// </summary>
    internal static class ExtraLanternPurger
    {
        private const float ScanInterval = 1f;
        private const float GraceBuffer = 0.3f;
        private const float HeartbeatLogInterval = 30f; // 每 30s 打一次心跳（找不到灯也能确认 Tick 在跑）

        private static float _lastScanTime = -999f;
        private static float _firstOverCountTime = -1f;
        private static float _lastHeartbeatLogTime = -999f;

        // 幽灵槽位日志节流：同一个 guid 只警告一次（避免 worldItem 找不到的日志刷屏）
        private static readonly HashSet<Guid> _warnedMissingGuids = new HashSet<Guid>();

        // 权限不足跳过日志节流：同一个 viewID 只警告一次（联机时别人的灯避免刷屏）
        private static readonly HashSet<int> _warnedNoPermissionViewIds = new HashSet<int>();

        /// <summary>
        /// 场景切换时清理日志节流集合，避免跨局无限增长。
        /// </summary>
        public static void ResetAccumulatedState()
        {
            _warnedMissingGuids.Clear();
            _warnedNoPermissionViewIds.Clear();
        }

        public static void Tick()
        {
            if (Plugin.PurgeExtraLanterns == null || !Plugin.PurgeExtraLanterns.Value) return;
            if (Time.time - _lastScanTime < ScanInterval) return;
            _lastScanTime = Time.time;

            Character local = Character.localCharacter;
            if (local == null || local.player == null)
            {
                if (Time.time - _lastHeartbeatLogTime > HeartbeatLogInterval)
                {
                    _lastHeartbeatLogTime = Time.time;
                    Plugin.Log?.LogInfo($"[DEBUG] [ExtraLanternPurger] skip: localCharacter/player null");
                }
                return;
            }
            if (local.data == null || local.data.dead)
            {
                if (Time.time - _lastHeartbeatLogTime > HeartbeatLogInterval)
                {
                    _lastHeartbeatLogTime = Time.time;
                    Plugin.Log?.LogInfo($"[DEBUG] [ExtraLanternPurger] skip: data null or dead");
                }
                return;
            }

            List<Candidate> lanterns = CollectPlayerLanterns(local);

            // 场景中实际存在的活跃灯数（诊断用：如果 player=1 但 scene>>1，说明有灯在地上/别人手里，Purger 不管它们）
            int sceneLanternCount = CountSceneLanterns();

            // 心跳：没灯/只有1盏时每30s输出一次，证明 Tick 在跑
            if (lanterns.Count <= 1)
            {
                if (_firstOverCountTime >= 0f)
                    Plugin.Log?.LogInfo($"[DEBUG] [ExtraLanternPurger] count back to {lanterns.Count}, grace cleared");
                _firstOverCountTime = -1f;
                if (Time.time - _lastHeartbeatLogTime > HeartbeatLogInterval)
                {
                    _lastHeartbeatLogTime = Time.time;
                    string playerName = local.characterName ?? "unknown";
                    Plugin.Log?.LogInfo($"[DEBUG] [ExtraLanternPurger] heartbeat: {lanterns.Count} lantern(s) on player '{playerName}', scene total={sceneLanternCount}, isMaster={PhotonNetwork.IsMasterClient}");
                }
                return;
            }

            // 500ms 缓冲：刚出现多余灯时先记个时间戳，下次 scan 再看是否仍多余
            if (_firstOverCountTime < 0f)
            {
                _firstOverCountTime = Time.time;
                Plugin.Log?.LogInfo($"[DEBUG] [ExtraLanternPurger] detected {lanterns.Count} lanterns (scene total={sceneLanternCount}), grace {GraceBuffer:F1}s");
                // 详细列出每盏灯的位置/燃料/归属
                for (int i = 0; i < lanterns.Count; i++)
                {
                    var c = lanterns[i];
                    bool mine = c.WorldItem != null && c.WorldItem.photonView != null && c.WorldItem.photonView.IsMine;
                    int viewId = c.WorldItem != null && c.WorldItem.photonView != null ? c.WorldItem.photonView.ViewID : -1;
                    Plugin.Log?.LogInfo($"[DEBUG] [ExtraLanternPurger]   #{i}: loc={c.Location}, fuel={c.Fuel:F1}, temp={c.IsTempSlot}, mine={mine}, viewID={viewId}");
                }
                return;
            }
            if (Time.time - _firstOverCountTime < GraceBuffer) return;

            // 排序：tempSlot 优先 → 燃料最多优先（销毁新副本，保留老灯）
            // 思路：玩家一直在用的灯燃料必然少于刚生成/刚捡起的满燃料副本，
            // 所以销毁 fuel 大的那盏 = 销毁新来的，玩家体感稳定
            Player localPlayer = local.player;
            lanterns.Sort((a, b) =>
            {
                bool aDestroyable = CanDestroy(a);
                bool bDestroyable = CanDestroy(b);
                if (aDestroyable != bDestroyable) return bDestroyable.CompareTo(aDestroyable);
                if (a.IsTempSlot != b.IsTempSlot) return b.IsTempSlot.CompareTo(a.IsTempSlot);
                return b.Fuel.CompareTo(a.Fuel); // 降序：fuel 多的排前面 → 被优先销毁
            });

            int excess = lanterns.Count - 1;
            int purged = 0;
            for (int i = 0; i < excess; i++)
            {
                var c = lanterns[i];
                if (c.WorldItem == null) continue;
                int viewId = c.WorldItem.photonView != null ? c.WorldItem.photonView.ViewID : -1;
                bool isMine = c.WorldItem.photonView != null && c.WorldItem.photonView.IsMine;
                bool isMaster = PhotonNetwork.IsMasterClient;
                if (!TryDestroy(c, localPlayer))
                {
                    Plugin.Log?.LogWarning($"[ExtraLanternPurger] cannot destroy lantern (loc={c.Location}, fuel={c.Fuel:F1}, viewID={viewId}, mine={isMine}, master={isMaster})");
                    continue;
                }
                // 联机上下文：路径（清理哪类槽）/ viewID / 所有权 / 是否 Master
                string path = !c.PlayerSlotId.HasValue ? "Backpack"
                              : (c.PlayerSlotId.Value == 250 ? "TempFull" : $"Hand#{c.PlayerSlotId.Value}");
                Plugin.Log?.LogInfo($"[ExtraLanternPurger] destroyed extra lantern: loc={c.Location}, fuel={c.Fuel:F1}, temp={c.IsTempSlot}, viewID={viewId}, mine={isMine}, master={isMaster}, path={path}");
                purged++;
            }

            _firstOverCountTime = -1f;
            if (purged > 0)
                Plugin.Log?.LogInfo($"[ExtraLanternPurger] purged {purged} extra lantern(s)");
        }

        private struct Candidate
        {
            public Item WorldItem;
            public float Fuel;
            public bool IsTempSlot;
            public string Location; // 诊断用：Hand#0 / Hand#1 / Hand#2 / TempFull / Backpack#N
            // Player.GetItemSlot 能直接用的 slotID，切需要走 Player.EmptySlot 清手持槽 data：
            // Hand#0/1/2 → 0/1/2，TempFull → 250，Backpack#N → null（走 ClearDataFromBackpack 清背包内槽）
            public byte? PlayerSlotId;
        }

        /// <summary>
        /// 数当前场景中所有活跃的非仙子灯（包括地上/别人手里/正在销毁的）。
        /// 诊断用：对比 player 身上灯数和场景灯总数的差别。
        /// </summary>
        private static int CountSceneLanterns()
        {
            int n = 0;
            foreach (var lantern in UnityEngine.Object.FindObjectsByType<Lantern>(FindObjectsSortMode.None))
            {
                if (lantern == null) continue;
                if (LanternHelper.IsSpecialLantern(lantern)) continue;
                n++;
            }
            return n;
        }

        /// <summary>
        /// 收集本地玩家身上的所有“非仙子灯”：手持 0-2 + tempFullSlot + 背包。
        /// </summary>
        private static List<Candidate> CollectPlayerLanterns(Character character)
        {
            var list = new List<Candidate>();
            Player player = character.player;
        
            // 1. 手持 0-2——slotID 就是数组下标
            if (player.itemSlots != null)
            {
                for (int i = 0; i < player.itemSlots.Length; i++)
                    TryAdd(player.itemSlots[i], false, $"Hand#{i}", (byte?)(byte)i, list);
            }
        
            // 2. tempFullSlot（3槽满时创建，Player.GetItemSlot(250) 拿到的就是它）
            TryAdd(player.tempFullSlot, true, "TempFull", (byte?)250, list);
        
            // 3. 背包内槽位——不是 Player 顶级槽，走 ClearDataFromBackpack 处理，所以 slotId 为 null
            if (player.backpackSlot != null && player.backpackSlot.hasBackpack
                && player.backpackSlot.data != null
                && player.backpackSlot.data.TryGetDataEntry<BackpackData>(DataEntryKey.BackpackData, out BackpackData bpData)
                && bpData != null && bpData.itemSlots != null)
            {
                for (int i = 0; i < bpData.itemSlots.Length; i++)
                    TryAdd(bpData.itemSlots[i], false, $"Backpack#{i}", null, list);
            }
        
            return list;
        }
        
        private static void TryAdd(ItemSlot slot, bool isTemp, string location, byte? playerSlotId, List<Candidate> list)
        {
            if (slot == null || slot.IsEmpty() || slot.prefab == null || slot.data == null) return;
            if (slot.prefab.name != "Lantern") return;
            if (slot.prefab.name.Contains("Faerie")) return; // 双保险（仙子灯 prefab 名一般带 Faerie）
        
            Item worldItem = LanternHelper.FindWorldItemByGuid(slot.data.guid);
            if (worldItem == null)
            {
                // 同一个 guid 只警告一次（持久静默），避免幽灵槽位刷屏
                // 注意：故意不 Remove——BPR 模式切换会让 guid 反复出现，Remove 会导致日志前述刷屏
                if (_warnedMissingGuids.Add(slot.data.guid))
                    Plugin.Log?.LogInfo($"[DEBUG] [ExtraLanternPurger] skip {location}: worldItem not found for guid={slot.data.guid} (silenced permanently for this guid)");
                return;
            }
        
            Lantern lantern = worldItem.GetComponent<Lantern>();
            if (lantern == null) return;
            if (LanternHelper.IsSpecialLantern(lantern)) return;
        
            float fuel = 0f;
            if (worldItem.data.TryGetDataEntry(DataEntryKey.Fuel, out FloatItemData fuelData))
                fuel = fuelData.Value;
        
            list.Add(new Candidate { WorldItem = worldItem, Fuel = fuel, IsTempSlot = isTemp, Location = location, PlayerSlotId = playerSlotId });
        }

        private static bool TryDestroy(Candidate c, Player player)
        {
            Item item = c.WorldItem;
            if (item == null || item.photonView == null) return false;
            // PhotonNetwork.Destroy 要求 IsMine 或 MasterClient，否则放弃
            if (!item.photonView.IsMine && !PhotonNetwork.IsMasterClient)
            {
                // 联机下可能出现：灯要转移到我身上（中间瞬间被 Collect 算上身）但 ownership 还在对方
                // 同一 viewID 只记一次，避免刷屏
                int viewId = item.photonView.ViewID;
                if (_warnedNoPermissionViewIds.Add(viewId))
                    Plugin.Log?.LogInfo($"[DEBUG] [ExtraLanternPurger] skip destroy viewID={viewId} loc={c.Location}: not mine and not master (silenced for this viewID)");
                return false;
            }
            try
            {
                // 关键一：如果灯在背包内槽位，先走游戏原生清理背包槽位（EmptyOut + RPC 同步）。
                // 否则光 PhotonNetwork.Destroy 会留下幽灵槽位——data.guid 还在但 worldItem 没了，
                // 导致背包其他灯也拿不出来。手持灯会 no-op（内部 IsNone 检查）。
                bool wasInBackpack = !item.backpackReference.IsNone;
                item.ClearDataFromBackpack();
                if (wasInBackpack)
                    Plugin.Log?.LogInfo($"[DEBUG] [ExtraLanternPurger] ClearDataFromBackpack invoked (loc={c.Location}, viewID={item.photonView.ViewID})");

                // 关键二：如果灯在玩家顶级槽（手持 0/1/2 或 tempFull 250），走 Player.EmptySlot
                // 原生流程。ItemSlot.EmptyOut() 只清 prefab=null 并不清 data，Player.EmptySlot 会
                // 跟着 SyncInventoryRPC 广播空槽状态——这样手持 UI 格子才不会有幽灵残影。
                if (c.PlayerSlotId.HasValue && player != null)
                {
                    try
                    {
                        player.EmptySlot(Optionable<byte>.Some(c.PlayerSlotId.Value));
                        Plugin.Log?.LogInfo($"[DEBUG] [ExtraLanternPurger] Player.EmptySlot({c.PlayerSlotId.Value}) invoked (loc={c.Location}, SyncInventoryRPC broadcast)");
                    }
                    catch (System.Exception exSlot)
                    {
                        Plugin.Log?.LogWarning($"[ExtraLanternPurger] EmptySlot({c.PlayerSlotId.Value}) failed: {exSlot.Message}");
                    }
                }

                PhotonNetwork.Destroy(item.gameObject);
                return true;
            }
            catch (System.Exception ex)
            {
                Plugin.Log?.LogWarning($"[ExtraLanternPurger] Destroy failed: {ex.Message}");
                return false;
            }
        }

        private static bool CanDestroy(Candidate c)
        {
            Item item = c.WorldItem;
            return item != null
                && item.photonView != null
                && (item.photonView.IsMine || PhotonNetwork.IsMasterClient);
        }
    }
}
