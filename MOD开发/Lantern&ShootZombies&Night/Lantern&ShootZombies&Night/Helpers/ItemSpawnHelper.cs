using Photon.Pun;
using UnityEngine;

namespace Lantern_ShootZombies_Night
{
    /// <summary>
    /// 快捷键（F8）生成灯/号角/背包：按不同权限策略发放。
    ///
    /// 分工：
    /// - **灯**：每个玩家本地查自己身上（手持+tempSlot+背包），没有"非仙子灯"才刷。
    ///   刷出来的灯 Fuel=0（防止"用完一盏刷一盏满的"空手套白狼），需要靠
    ///   AutoRefill / 打僵尸 / 号角大招回血。
    /// - **号角**：仅房主能刷，且必须全场查无（所有玩家身上 + 场景独立 BugleSFX），
    ///   防止单爬玩家把号角扔野外锁死房主的 F8 补刷。
    /// - **背包**：每个玩家本地查，没有才刷（原有行为）。
    /// </summary>
    internal static class ItemSpawnHelper
    {
        private static float _lastSpawnTime = -999f;
        private const float SpawnCooldown = 2f;

        /// <summary>快捷键触发入口。</summary>
        public static void TrySpawnMissingItems()
        {
            Character local = Character.localCharacter;
            if (local == null || local.data == null || local.data.dead) return;
            if (!PhotonNetwork.IsConnected) return;

            if (Time.time - _lastSpawnTime < SpawnCooldown) return;

            bool didAnything = false;

            // ── 1. 灯（每人自己查自己）──
            if (!HasNonFaerieLantern(local))
            {
                if (SpawnEmptyLantern())
                {
                    Plugin.Log?.LogInfo("[F8] Spawned empty lantern");
                    didAnything = true;
                }
            }
            else
            {
                Plugin.Log?.LogInfo("[DEBUG] [ItemSpawn] lantern already present on this player");
            }

            // ── 2. 背包（每人自己查自己）──
            if (!HasItemAnywhere(local, "Backpack"))
            {
                if (SpawnAndPickup("Backpack"))
                {
                    Plugin.Log?.LogInfo("[F8] Spawned Backpack for player");
                    didAnything = true;
                }
            }

            // ── 3. 号角（仅房主 + 全场查 + 要求房主有空常规槽）──
            // 注意：绝对不能让号角进 tempFullSlot——PEAK 游戏本体对临时槽物品
            // 在死亡/换场景/被击飞时存在 holderCharacter 脱钩 bug，这时
            // StrayBugleCleaner 会误杀，tempFullSlot 留孤儿引用导致号角卡住拿不出来。
            if (PhotonNetwork.IsMasterClient)
            {
                if (!HasFreeRegularSlot(local))
                {
                    Plugin.Log?.LogInfo("[F8] Bugle spawn skipped: no free regular slot (avoid tempFullSlot stuck bug). Empty a slot (0/1/2) first.");
                }
                else if (!AnyBugleInWorld())
                {
                    if (SpawnAndPickup("Bugle"))
                    {
                        Plugin.Log?.LogInfo("[F8] Spawned Bugle (master, world-wide none)");
                        didAnything = true;
                    }
                }
                else
                {
                    Plugin.Log?.LogInfo("[DEBUG] [ItemSpawn] Bugle already exists somewhere, skip");
                }
            }
            else
            {
                Plugin.Log?.LogInfo("[DEBUG] [ItemSpawn] not master client, bugle spawn skipped");
            }

            if (didAnything) _lastSpawnTime = Time.time;
        }

        // ─────────────────────────────────────────────────
        // 查询：玩家身上是否有某类物品
        // ─────────────────────────────────────────────────

        /// <summary>
        /// 玩家身上是否有非仙子灯（Faerie 豁免）。
        /// 覆盖：手持(0-2) + tempSlot(250) + 背包4格。
        /// </summary>
        public static bool HasNonFaerieLantern(Character character)
        {
            if (character == null || character.player == null) return false;
            Player player = character.player;

            if (player.itemSlots != null)
            {
                foreach (var slot in player.itemSlots)
                    if (IsNonFaerieLantern(slot)) return true;
            }

            if (IsNonFaerieLantern(player.tempFullSlot)) return true;

            if (player.backpackSlot != null && player.backpackSlot.hasBackpack
                && player.backpackSlot.data != null
                && player.backpackSlot.data.TryGetDataEntry<BackpackData>(DataEntryKey.BackpackData, out BackpackData bpData)
                && bpData != null && bpData.itemSlots != null)
            {
                foreach (var slot in bpData.itemSlots)
                    if (IsNonFaerieLantern(slot)) return true;
            }

            return false;
        }

        /// <summary>
        /// 通用查询：玩家是否持有指定 prefab 名的物品。
        /// </summary>
        public static bool HasItemAnywhere(Character character, string itemName)
        {
            if (character == null || character.player == null) return false;
            Player player = character.player;

            if (itemName == "Backpack")
                return player.backpackSlot != null && player.backpackSlot.hasBackpack;

            if (player.itemSlots != null)
            {
                foreach (var slot in player.itemSlots)
                    if (IsSlotItem(slot, itemName)) return true;
            }

            if (IsSlotItem(player.tempFullSlot, itemName)) return true;

            if (player.backpackSlot != null && player.backpackSlot.hasBackpack
                && player.backpackSlot.data != null
                && player.backpackSlot.data.TryGetDataEntry<BackpackData>(DataEntryKey.BackpackData, out BackpackData bpData)
                && bpData != null && bpData.itemSlots != null)
            {
                foreach (var slot in bpData.itemSlots)
                    if (IsSlotItem(slot, itemName)) return true;
            }

            return false;
        }

        /// <summary>
        /// 全场查号角：遍历所有玩家身上 + 场景独立 BugleSFX。
        /// 仅房主需要，因为 F8 刷号角仅房主触发。
        /// </summary>
        public static bool AnyBugleInWorld()
        {
            // 1. 任何玩家身上
            if (Character.AllCharacters != null)
            {
                foreach (var ch in Character.AllCharacters)
                {
                    if (ch == null) continue;
                    if (HasItemAnywhere(ch, "Bugle")) return true;
                }
            }

            // 2. 场景独立 BugleSFX（掉落在地上、被 kinematic 移出列表等情况）
            BugleSFX[] scene = UnityEngine.Object.FindObjectsByType<BugleSFX>(FindObjectsSortMode.None);
            if (scene != null && scene.Length > 0) return true;

            return false;
        }

        // ─────────────────────────────────────────────────
        // 生成
        // ─────────────────────────────────────────────────

        /// <summary>生成一盏空燃料灯给 local。</summary>
        private static bool SpawnEmptyLantern()
        {
            Character local = Character.localCharacter;
            if (local == null) return false;

            if (!ItemDatabase.TryGetItem("Lantern", out Item prefab))
            {
                Plugin.Log?.LogWarning("[DEBUG] [ItemSpawn] 'Lantern' not found in ItemDatabase");
                return false;
            }

            Vector3 pos = local.Center + Vector3.up * 0.3f;
            GameObject go = PhotonNetwork.Instantiate("0_Items/" + prefab.name, pos, Quaternion.identity);
            if (go == null)
            {
                Plugin.Log?.LogWarning($"[ItemSpawn] PhotonNetwork.Instantiate returned null for Lantern (inRoom={PhotonNetwork.InRoom}, isMaster={PhotonNetwork.IsMasterClient})");
                return false;
            }

            Item spawnedItem = go.GetComponent<Item>();
            if (spawnedItem == null) return false;

            int spawnViewId = spawnedItem.photonView != null ? spawnedItem.photonView.ViewID : -1;
            Plugin.Log?.LogInfo($"[ItemSpawn] Spawned Lantern: viewID={spawnViewId}, pos={pos}, isMaster={PhotonNetwork.IsMasterClient}");

            // 立即把 Fuel 设为 0（防止刷满血灯的套路）
            // 如果 data 还没初始化（OnInstanceDataSet 未运行），尝试添加一个 Fuel 条目
            if (spawnedItem.data != null)
            {
                if (spawnedItem.data.TryGetDataEntry(DataEntryKey.Fuel, out FloatItemData fuelData))
                {
                    fuelData.Value = 0f;
                }
                else
                {
                    // OnInstanceDataSet 还没跑，我们自己注册一个
                    FloatItemData newFuel = spawnedItem.data.RegisterNewEntry<FloatItemData>(DataEntryKey.Fuel);
                    if (newFuel != null) newFuel.Value = 0f;
                }

                if (spawnedItem.photonView != null)
                {
                    spawnedItem.photonView.RPC("SetItemInstanceDataRPC", RpcTarget.Others,
                        new object[] { spawnedItem.data });
                }
            }

            // 并同步把世界实例 Lantern.fuel 字段也打 0
            Lantern lantern = spawnedItem.GetComponent<Lantern>();
            if (lantern != null)
            {
                ReflectionCache.SetFuel(lantern, 0f);
                // 确保灯是灭的（刚 spawn 的灯可能默认 lit=true）
                if (ReflectionCache.GetLit(lantern) && lantern.photonView != null && lantern.photonView.IsMine)
                {
                    lantern.photonView.RPC("LightLanternRPC", RpcTarget.All, new object[] { false });
                }
            }

            PhotonView localPV = local.GetComponent<PhotonView>();
            if (localPV != null) spawnedItem.RequestPickup(localPV);
            return true;
        }

        /// <summary>在玩家脚下生成物品并请求拾取（号角/背包通用）。</summary>
        private static bool SpawnAndPickup(string itemName)
        {
            Character local = Character.localCharacter;
            if (local == null) return false;

            if (!ItemDatabase.TryGetItem(itemName, out Item prefab))
            {
                Plugin.Log?.LogWarning($"[DEBUG] [ItemSpawn] '{itemName}' not found in ItemDatabase");
                return false;
            }

            Vector3 pos = local.Center + Vector3.up * 0.3f;
            GameObject go = PhotonNetwork.Instantiate("0_Items/" + prefab.name, pos, Quaternion.identity);
            if (go == null)
            {
                Plugin.Log?.LogWarning($"[ItemSpawn] PhotonNetwork.Instantiate returned null for '{itemName}' (inRoom={PhotonNetwork.InRoom}, isMaster={PhotonNetwork.IsMasterClient})");
                return false;
            }

            Item spawnedItem = go.GetComponent<Item>();
            if (spawnedItem == null) return false;

            int spawnViewId = spawnedItem.photonView != null ? spawnedItem.photonView.ViewID : -1;
            Plugin.Log?.LogInfo($"[ItemSpawn] Spawned '{itemName}': viewID={spawnViewId}, pos={pos}, isMaster={PhotonNetwork.IsMasterClient}");

            PhotonView localPV = local.GetComponent<PhotonView>();
            if (localPV != null) spawnedItem.RequestPickup(localPV);
            return true;
        }

        // ─────────────────────────────────────────────────
        // 工具
        // ─────────────────────────────────────────────────

        /// <summary>
        /// 检查玩家 3 个常规槽（itemSlots 0/1/2）是否至少有一个空。
        /// 用于 F8 刷号角前置校验：避免号角被硬塑进 tempFullSlot（见调用处注释）。
        /// </summary>
        private static bool HasFreeRegularSlot(Character character)
        {
            if (character == null || character.player == null) return false;
            Player player = character.player;
            if (player.itemSlots == null) return false;
            foreach (var slot in player.itemSlots)
            {
                if (slot == null) continue;
                if (slot.IsEmpty()) return true;
            }
            return false;
        }

        private static bool IsSlotItem(ItemSlot slot, string itemName)
        {
            return slot != null && !slot.IsEmpty()
                && slot.prefab != null && slot.prefab.name == itemName;
        }

        private static bool IsNonFaerieLantern(ItemSlot slot)
        {
            if (slot == null || slot.IsEmpty() || slot.prefab == null) return false;
            if (slot.prefab.name != "Lantern") return false;
            // Faerie 灯的 prefab 名可能就是 "Lantern_Faerie"，或 GameObject.name 含 "Faerie"
            // 这里双保险：prefab 名含 Faerie 也算仙子
            if (slot.prefab.name.Contains("Faerie")) return false;
            // 若 prefab 本身是个生成过的对象，检查 GameObject 名
            if (slot.prefab.gameObject != null && slot.prefab.gameObject.name.Contains("Faerie")) return false;
            return true;
        }
    }
}
