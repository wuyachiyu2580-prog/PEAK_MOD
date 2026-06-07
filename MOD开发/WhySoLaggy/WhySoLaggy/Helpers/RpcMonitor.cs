using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using HarmonyLib;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using Hashtable = ExitGames.Client.Photon.Hashtable;

namespace WhySoLaggy
{
    /// <summary>
    /// 高性能 RPC 监控器（1.0.3 重构）：
    /// - 热路径：仅快速提取少量字段后 Enqueue，主线程 Tick 中统一消费（ConcurrentQueue 方案）。
    /// - 每方法独立环形缓冲（方案 A，1.0.2 保留）：SyncAfflictionsRPC 等高频 RPC 不再淹没低频 RPC。
    /// - 所有 Photon 回调均在主线程，改用 ConcurrentQueue 只是为了与后续可能的多线程变更隔离。
    /// - Payload 大小估算 + 结构化事件（1.0.3 新增）。
    /// </summary>
    internal static class RpcMonitor
    {
        // ── 配置 ──
        public static bool Enabled = true;
        public static int TopMethodCount = 10;
        /// <summary>每个 watched 方法独立缓冲的容量（方案 A：按方法分组）。</summary>
        public static int WatchedRecordPerMethodCapacity = 32;
        /// <summary>定期报告里每个方法最多展示的最近记录条数。</summary>
        public static int WatchedShowPerMethod = 6;
        /// <summary>1.0.3：主线程每帧 PumpQueue 最多消费条数（防止尖峰）。</summary>
        public static int PumpBatchSize = 32;

        /// <summary>
        /// 关注的高危 RPC 白名单（会记录完整调用明细：sender + target + args）。
        /// 选型原则：低频 + 破坏性高。所有条目均已在 Assembly-CSharp 1.61.b 中核实 [PunRPC]。
        /// 绝不加：每帧/每秒同步类（Sync*Time/Lava/Fog/Tornado/Vine、Jump/Crouch/Climb 等）——会将 args 序列化压力放大 100×。
        /// </summary>
        public static readonly HashSet<string> WatchedMethods = new HashSet<string>(StringComparer.Ordinal)
        {
            // ═══ 喂食完整链路 ─ 核心监控 ─ 能识别"谁喂谁" ═══
            "SendFeedDataRPC",
            "RemoveFeedDataRPC",
            "GetFedItemRPC",
            "Consume",
            "RPCA_ConsumeItem",
            // ═══ 治疗/解毒统计 ═══
            "IncrementFriendHealingRpc",
            "IncrementPoisonHealedStat",
            // ═══ 状态/Affliction 同步 ═══
            "SyncStatusesRPC",
            "SyncAfflictionsRPC",
            "RPC_ApplyStatusesFromFloatArray",
            "RPCA_AddStatusBingBing",
            "RPCA_Stick",
            "RPCA_Unstick",                 // 1.0.4：与 Stick 成对
            "RPC_StickToCharacterRemote",   // 1.0.4：StickyItemComponent 粘人
            "TryAddAfflictionToLocalCharacter", // 1.0.4：Action_ApplyMassAffliction 群体施加
            // ═══ 死亡 / 复活 / 传送 / 摔倒 / 昏迷 ═══
            "RPCA_Die",
            "RPCA_Zombify",
            "RPCA_SetDead",
            "RPCA_PassOut",
            "RPCA_UnPassOut",               // 1.0.4：与 PassOut 成对
            "RPCA_Fall",
            "RPCA_FallWithScreenShake",
            "RPCA_UnFall",                  // 1.0.4：与 Fall 成对
            "RPCA_Revive",                  // 1.0.4：复活
            "RPCA_ReviveAtPosition",        // 1.0.4：在指定位置复活
            "WarpPlayerRPC",
            // ═══ 物理冲击 ═══
            "RPCA_AddForceAtPosition",
            "RPCA_AddForceToBodyPart",
            // ═══ 抓取 / 踢 / 背人（1.0.4） ═══
            "RPCA_GrabAttach",
            "RPCA_GrabUnattach",
            "RPCA_Kick",
            "RPCA_StartCarry",
            "RPCA_Drop",                    // CharacterCarrying.RPCA_Drop（同名视窗 OK，同为“丢人”）
            // ═══ 物品交互 ═══
            "LightLanternRPC",
            "PutInBackpackRPC",
            "SetHeldItemID",
            "DropItemRpc",
            "DropItemFromSlotRPC",          // 1.0.4：CharacterItems 从指定槽丢
            "DestroyHeldItemRpc",           // 1.0.4：破坏性
            "EquipSlotRpc",                 // 1.0.4：装备槽切换
            "RequestPickup",
            "RPC_SetThrownData",
            "RPCAddItemToBackpack",         // 1.0.4：Backpack.RPCAddItemToBackpack
            "RPCAddItemToCharacterBackpack",// 1.0.4：CharacterBackpackHandler
            "OpenLuggageRPC",               // 1.0.4：行李箱
            // ═══ 库存 / 装备操作（1.0.4） ═══
            "SyncInventoryRPC",
            "RPC_SetInventory",
            "RPCRemoveItemFromSlot",
            // ═══ 生成/spawn（1.0.4）——“刷物品”必经路径 ═══
            "CreatePrefabRPC",
            "InstantiateAndGrabRPC",
            "RPC_SpawnResourceAtPosition",
            // ═══ 场景/终局（1.0.4）——一次就是大新闻 ═══
            "RPCEndGame",
            "RPCEndGame_ForceWin",
            "BeginIslandLoadRPC",
            "LoadIslandMaster",
            "BeginAirportLoadRPC",
            "LoadAirportMaster",
            // ═══ 玩家被踢（1.0.4） ═══
            "RPC_GetKicked",
            // ═══ 龙卷风控人（1.0.4）——捕捉/抛出 ═══
            "RPCA_CaptureCharacter",
            "RPCA_ThrowPlayer",
            "RPCA_InitTornado",
            // ═══ 食人/骷髅状态（1.0.4） ═══
            "RPCA_SyncCanBeCannibalized",
            "RPC_SyncSkeleton",
            // ═══ 篥火（1.0.4） ═══
            "Light_Rpc",                    // Campfire.Light_Rpc（也会命中同名其它，低频 OK）
            "Extinguish_Rpc",               // Campfire.Extinguish_Rpc
        };

        // ── Photon keyByteX 硬编码 ──
        private const byte K_VIEWID = 0;
        private const byte K_PREFIX = 1;
        private const byte K_METHOD_NAME = 3;
        private const byte K_ARGS = 4;
        private const byte K_METHOD_IDX = 5;

        // ── 统计（主线程消费，无需 lock） ──
        private static readonly Dictionary<string, int> _totalByMethod = new Dictionary<string, int>(128);
        private static readonly Dictionary<string, Dictionary<int, int>> _totalByMethodActor =
            new Dictionary<string, Dictionary<int, int>>(64);
        private static readonly Dictionary<string, int> _windowByMethod = new Dictionary<string, int>(64);
        private static readonly Dictionary<string, long> _totalBytesByMethod = new Dictionary<string, long>(128);
        private static readonly Dictionary<string, int> _maxBytesByMethod = new Dictionary<string, int>(128);

        // ── 高危方法明细环形缓冲（方案 A：按方法分组独立） ──
        private struct WatchedRecord
        {
            public float time;
            public string method;
            public int senderActor;
            public string senderName;
            public int targetViewID;
            public string targetName;
            public string targetPath;
            public string argsSummary;
            public string specificDesc;
            public int payloadBytes;
        }

        private sealed class MethodBuffer
        {
            public readonly WatchedRecord[] Records;
            public int Idx;
            public int Count;
            public int Total;
            public MethodBuffer(int cap) { Records = new WatchedRecord[cap]; }
            public void Reset() { Idx = 0; Count = 0; Total = 0; }
        }

        private static readonly Dictionary<string, MethodBuffer> _watchedByMethod =
            new Dictionary<string, MethodBuffer>(32, StringComparer.Ordinal);

        // ── 1.0.3：ConcurrentQueue 入队项 ──
        private struct RpcQueueItem
        {
            public string methodName;
            public int senderActor;
            public string senderName;
            public int targetVID;
            public object[] args;    // 引用；Photon 回调完毕即同帧消费，生命周期安全
            public int payloadBytes;
            public bool watched;
        }

        private static readonly ConcurrentQueue<RpcQueueItem> _queue = new ConcurrentQueue<RpcQueueItem>();

        // 复用缓冲（主线程，无 lock）
        private static readonly StringBuilder _argsSb = new StringBuilder(48);
        private static readonly StringBuilder _pathSb = new StringBuilder(64);
        private static readonly List<string> _pathStack = new List<string>(8);
        private static readonly List<KeyValuePair<string, int>> _totalSorted
            = new List<KeyValuePair<string, int>>(64);
        private static readonly List<KeyValuePair<string, MethodBuffer>> _watchedSorted
            = new List<KeyValuePair<string, MethodBuffer>>(32);
        private static readonly List<KeyValuePair<int, int>> _topActorsSorted
            = new List<KeyValuePair<int, int>>(8);

        // ── 运行时 ──
        private static bool _inited;

        // ═══════════════════════════════════════════════
        //  Initialization
        // ═══════════════════════════════════════════════

        /// <summary>合并 ExtraWatchMethods（逗号分隔）到 WatchedMethods。Plugin 在 Initialize 前调用。</summary>
        public static void AddExtraWatchMethods(string csv)
        {
            if (string.IsNullOrWhiteSpace(csv)) return;
            foreach (var part in csv.Split(','))
            {
                var name = part.Trim();
                if (!string.IsNullOrEmpty(name)) WatchedMethods.Add(name);
            }
        }

        public static void Initialize(Harmony harmony)
        {
            if (_inited) return;
            try
            {
                PatchExecuteRpc(harmony);
                _watchedByMethod.Clear();
                foreach (var mn in WatchedMethods)
                    _watchedByMethod[mn] = new MethodBuffer(WatchedRecordPerMethodCapacity);
                _inited = true;
                AbuseLogger.Info($"[RPC_MON] RpcMonitor initialized (watched: {WatchedMethods.Count}, per-method buffer: {WatchedRecordPerMethodCapacity}, pump batch: {PumpBatchSize})");
            }
            catch (Exception ex)
            {
                AbuseLogger.Info($"[RPC_MON] Init failed: {ex.Message}");
                WhySoLaggyPlugin.Log?.LogError($"[WHY_LAG] RpcMonitor init failed: {ex}");
            }
        }

        private static void PatchExecuteRpc(Harmony harmony)
        {
            MethodInfo m = typeof(PhotonNetwork).GetMethod(
                "ExecuteRpc",
                BindingFlags.NonPublic | BindingFlags.Static,
                null,
                new Type[] { typeof(Hashtable), typeof(Player) },
                null);

            if (m == null)
            {
                AbuseLogger.Info("[RPC_MON] WARNING: PhotonNetwork.ExecuteRpc not found. Remote RPC method names unavailable.");
                return;
            }

            try
            {
                harmony.Patch(m, prefix: new HarmonyMethod(typeof(RpcMonitor), nameof(OnExecuteRpcPrefix)));
                AbuseLogger.Info("[RPC_MON] Hooked PhotonNetwork.ExecuteRpc successfully");
            }
            catch (Exception ex)
            {
                WhySoLaggyPlugin.Log?.LogError($"[WHY_LAG] RpcMonitor patch ExecuteRpc failed: {ex}");
            }
        }

        // ═══════════════════════════════════════════════
        //  Hot Path — 热路径仅入队
        // ═══════════════════════════════════════════════

        static void OnExecuteRpcPrefix(Hashtable rpcData, Player sender)
        {
            if (!Enabled || rpcData == null) return;
            try
            {
                string methodName = null;
                object idxObj = rpcData[K_METHOD_IDX];
                if (idxObj != null)
                {
                    try
                    {
                        int idx = (byte)idxObj;
                        var list = PhotonNetwork.PhotonServerSettings?.RpcList;
                        if (list != null && idx < list.Count)
                            methodName = list[idx];
                    }
                    catch { }
                }
                if (methodName == null)
                    methodName = rpcData[K_METHOD_NAME] as string;
                if (string.IsNullOrEmpty(methodName)) return;

                bool watched = _watchedByMethod.ContainsKey(methodName);
                int targetVID = -1;
                try { if (rpcData[K_VIEWID] is int iv) targetVID = iv; } catch { }

                int bytes = EstimatePayloadSize(rpcData);
                object[] args = watched ? rpcData[K_ARGS] as object[] : null;

                _queue.Enqueue(new RpcQueueItem
                {
                    methodName = methodName,
                    senderActor = sender?.ActorNumber ?? -1,
                    senderName = sender?.NickName,
                    targetVID = targetVID,
                    args = args,
                    payloadBytes = bytes,
                    watched = watched,
                });
            }
            catch (Exception ex)
            {
                WhySoLaggyPlugin.Log?.LogWarning($"[WHY_LAG] RpcMonitor prefix failed: {ex.Message}");
            }
        }

        /// <summary>1.0.3：主线程 Update 每帧调用，消费队列。</summary>
        public static void PumpQueue()
        {
            if (!_inited) return;
            int budget = PumpBatchSize;
            while (budget-- > 0 && _queue.TryDequeue(out var item))
            {
                try { ProcessDequeued(ref item); }
                catch (Exception ex)
                {
                    WhySoLaggyPlugin.Log?.LogWarning($"[WHY_LAG] RpcMonitor pump failed: {ex.Message}");
                }
            }
        }

        private static void ProcessDequeued(ref RpcQueueItem item)
        {
            string methodName = item.methodName;

            // 累加统计
            _windowByMethod.TryGetValue(methodName, out int wc);
            _windowByMethod[methodName] = wc + 1;

            _totalByMethod.TryGetValue(methodName, out int tc);
            _totalByMethod[methodName] = tc + 1;

            if (!_totalByMethodActor.TryGetValue(methodName, out var inner))
            {
                inner = new Dictionary<int, int>(4);
                _totalByMethodActor[methodName] = inner;
            }
            inner.TryGetValue(item.senderActor, out int ac);
            inner[item.senderActor] = ac + 1;

            if (item.payloadBytes > 0)
            {
                _totalBytesByMethod.TryGetValue(methodName, out long tb);
                _totalBytesByMethod[methodName] = tb + item.payloadBytes;
                _maxBytesByMethod.TryGetValue(methodName, out int mb);
                if (item.payloadBytes > mb) _maxBytesByMethod[methodName] = item.payloadBytes;
            }

            if (item.watched && _watchedByMethod.TryGetValue(methodName, out var buf))
            {
                RecordWatched(ref item, buf);

                // 1.0.3：结构化事件（仅 watched 命中才写，减少量）
                // 1.0.4：回读 RecordWatched 刚写入的最新记录，复用已解析的 argsSummary/specificDesc/targetName，避免重复计算
                int lastIdx = (buf.Idx - 1 + buf.Records.Length) % buf.Records.Length;
                var recForLog = buf.Records[lastIdx];
                try
                {
                    StructuredLogger.WriteEvent(new StructuredEvent
                    {
                        Timestamp = StructuredLogger.NowStamp(),
                        FrameNumber = Time.frameCount,
                        Type = EventType.RpcCall,
                        Fields = new Dictionary<string, object>
                        {
                            { "RpcMethod", methodName },
                            { "SenderActor", item.senderActor },
                            { "SenderName", item.senderName ?? "" },
                            { "TargetViewID", item.targetVID },
                            { "TargetName", recForLog.targetName ?? "" },
                            { "PayloadBytes", item.payloadBytes },
                            { "ArgsSummary", recForLog.argsSummary ?? "" },
                            { "SpecificDesc", recForLog.specificDesc ?? "" },
                        },
                    });
                }
                catch { }
            }
        }

        // ═══════════════════════════════════════════════
        //  Payload & 详情解析（主线程）
        // ═══════════════════════════════════════════════

        private static int EstimatePayloadSize(Hashtable rpcData)
        {
            int total = 16;
            object argsObj = rpcData[K_ARGS];
            if (!(argsObj is object[] args)) return total;

            for (int i = 0; i < args.Length; i++)
            {
                object a = args[i];
                if (a == null) { total += 1; continue; }
                switch (a)
                {
                    case bool _:
                    case byte _:
                    case sbyte _:
                        total += 1; break;
                    case short _:
                    case ushort _:
                        total += 2; break;
                    case int _:
                    case uint _:
                    case float _:
                        total += 4; break;
                    case long _:
                    case ulong _:
                    case double _:
                        total += 8; break;
                    case Vector2 _:
                        total += 8; break;
                    case Vector3 _:
                        total += 12; break;
                    case Quaternion _:
                        total += 16; break;
                    case string s:
                        total += 2 + s.Length * 2; break;
                    case byte[] ba:
                        total += 4 + ba.Length; break;
                    case int[] ia:
                        total += 4 + ia.Length * 4; break;
                    case float[] fa:
                        total += 4 + fa.Length * 4; break;
                    case Array arr:
                        total += 4 + arr.Length * 4; break;
                    default:
                        total += 16; break;
                }
            }
            return total;
        }

        private static void RecordWatched(ref RpcQueueItem item, MethodBuffer buf)
        {
            string targetName = null;
            string targetPath = null;
            try
            {
                if (item.targetVID > 0)
                {
                    var pv = PhotonNetwork.GetPhotonView(item.targetVID);
                    if (pv != null)
                    {
                        targetName = pv.Owner?.NickName;
                        if (string.IsNullOrEmpty(targetName))
                            targetName = pv.gameObject != null ? pv.gameObject.name : null;
                        if (pv.gameObject != null)
                            targetPath = BuildTransformPath(pv.gameObject.transform);
                    }
                }
            }
            catch { }

            string argSum = BuildArgsSummary(item.args);

            string specific = null;
            try { specific = ParseSpecific(item.methodName, item.args, item.targetVID); }
            catch { }

            var rec = new WatchedRecord
            {
                time = Time.realtimeSinceStartup,
                method = item.methodName,
                senderActor = item.senderActor,
                senderName = item.senderName,
                targetViewID = item.targetVID,
                targetName = targetName,
                targetPath = targetPath,
                argsSummary = argSum,
                specificDesc = specific,
                payloadBytes = item.payloadBytes,
            };

            buf.Records[buf.Idx] = rec;
            buf.Idx = (buf.Idx + 1) % buf.Records.Length;
            if (buf.Count < buf.Records.Length) buf.Count++;
            buf.Total++;
        }

        private static string BuildArgsSummary(object[] args)
        {
            if (args == null || args.Length == 0) return null;
            _argsSb.Clear();
            int n = args.Length > 3 ? 3 : args.Length;
            for (int i = 0; i < n; i++)
            {
                if (i > 0) _argsSb.Append(',');
                object a = args[i];
                if (a == null) { _argsSb.Append("null"); continue; }
                _argsSb.Append(a.GetType().Name);
                if (a is int || a is bool || a is float || a is string || a is byte || a is short)
                    _argsSb.Append('=').Append(a);
            }
            if (args.Length > 3) _argsSb.Append(",...");
            return _argsSb.ToString();
        }

        private static string ParseSpecific(string m, object[] args, int targetVID)
        {
            if (args == null) return null;
            switch (m)
            {
                case "SendFeedDataRPC":
                    if (args.Length >= 4 && args[0] is int gid && args[1] is int rid && args[2] is int iid)
                    {
                        string giver = ResolvePhotonViewOwner(gid);
                        string receiver = ResolvePhotonViewOwner(rid);
                        string itemName = ResolvePhotonViewGameObject(iid);
                        float castTime = args[3] is float ct ? ct : 0f;
                        return $"喂食者={giver}#{gid} → 被喂者={receiver}#{rid}, 物品={itemName}#{iid}, 时长={castTime:F1}s";
                    }
                    break;

                case "RemoveFeedDataRPC":
                    if (args.Length >= 1 && args[0] is int rmGid)
                    {
                        string rmGiver = ResolvePhotonViewOwner(rmGid);
                        return $"喂食结束: 喂食者={rmGiver}#{rmGid}";
                    }
                    break;

                case "GetFedItemRPC":
                    if (args.Length >= 1 && args[0] is int fedItemId)
                    {
                        string itemName = ResolvePhotonViewGameObject(fedItemId);
                        string targetOwner = ResolvePhotonViewOwner(targetVID);
                        return $"接收物品={itemName}#{fedItemId}, 目标角色={targetOwner}#{targetVID}";
                    }
                    break;

                case "Consume":
                case "RPCA_ConsumeItem":
                    if (args.Length >= 1 && args[0] is int consumerId)
                    {
                        string eater = ResolvePhotonViewOwner(consumerId);
                        string itemObj = ResolvePhotonViewGameObject(targetVID);
                        return $"消耗者={eater}#{consumerId}, 物品={itemObj}#{targetVID}";
                    }
                    break;

                case "IncrementFriendHealingRpc":
                    if (args.Length >= 1 && args[0] is int healAmt)
                        return $"治疗量={healAmt}";
                    break;

                case "IncrementPoisonHealedStat":
                    if (args.Length >= 1 && args[0] is int poisonAmt)
                        return $"解毒量={poisonAmt}";
                    break;

                case "DropItemRpc":
                    return $"丢弃物品, target={ResolvePhotonViewGameObject(targetVID)}#{targetVID}";

                case "RequestPickup":
                    if (args.Length >= 1)
                    {
                        string charName = "?";
                        if (args[0] is int rpVid) charName = ResolvePhotonViewOwner(rpVid);
                        return $"拾取请求: 角色={charName}, 物品={ResolvePhotonViewGameObject(targetVID)}#{targetVID}";
                    }
                    break;

                case "RPC_SetThrownData":
                    if (args.Length >= 2 && args[0] is int throwerId && args[1] is float throwAmt)
                    {
                        string thrower = ResolvePhotonViewOwner(throwerId);
                        return $"投掷者={thrower}#{throwerId}, 力度={throwAmt:F2}";
                    }
                    break;

                case "RPCA_Die":
                case "RPCA_Zombify":
                    if (args.Length >= 1 && args[0] is Vector3 dp)
                        return $"spawnPos=({dp.x:F1},{dp.y:F1},{dp.z:F1})";
                    break;

                case "WarpPlayerRPC":
                    if (args.Length >= 1 && args[0] is Vector3 wp)
                    {
                        string poof = args.Length >= 2 && args[1] is bool pb ? pb.ToString() : "?";
                        return $"to=({wp.x:F1},{wp.y:F1},{wp.z:F1}), poof={poof}";
                    }
                    break;

                case "RPCA_Fall":
                    if (args.Length >= 1 && args[0] is float fs)
                        return $"sec={fs:F2}";
                    break;

                case "RPCA_FallWithScreenShake":
                    if (args.Length >= 2 && args[0] is float fss && args[1] is float shake)
                        return $"sec={fss:F2}, shake={shake:F2}";
                    break;

                case "LightLanternRPC":
                    if (args.Length >= 1 && args[0] is bool lit)
                        return $"lit={lit}";
                    break;

                case "PutInBackpackRPC":
                    if (args.Length >= 1)
                    {
                        byte slot = args[0] is byte b ? b : (byte)0;
                        return $"slot={slot}";
                    }
                    break;

                case "RPCA_AddStatusBingBing":
                    if (args.Length >= 3 && args[0] is int t && args[1] is int sid && args[2] is int mult)
                    {
                        string tn = ResolvePhotonViewOwner(t);
                        return $"target={tn}#{t}, status={StatusName(sid)}, mult={mult}";
                    }
                    break;

                case "RPCA_Stick":
                    if (args.Length >= 5)
                    {
                        string bp = args[0]?.ToString() ?? "?";
                        int sIdx = args[3] is int si ? si : (args[3] is byte sb ? sb : -1);
                        float amt = args[4] is float fv ? fv : 0f;
                        return $"body={bp}, status={StatusName(sIdx)}, amount={amt:F2}";
                    }
                    break;

                case "SyncStatusesRPC":
                case "SyncAfflictionsRPC":
                    if (args.Length >= 1 && args[0] is byte[] ba)
                        return $"bytes={ba.Length}";
                    break;

                case "RPC_ApplyStatusesFromFloatArray":
                    if (args.Length >= 1 && args[0] is float[] fa)
                    {
                        var sb = new StringBuilder(64);
                        sb.Append($"floats[{fa.Length}]");
                        int n = Math.Min(fa.Length, 12);
                        sb.Append(" 值=");
                        for (int i = 0; i < n; i++)
                        {
                            if (i > 0) sb.Append(',');
                            if (fa[i] != 0f) sb.Append(StatusName(i)).Append(':').Append(fa[i].ToString("F2"));
                            else sb.Append('_');
                        }
                        return sb.ToString();
                    }
                    break;

                case "RPCA_AddForceAtPosition":
                    if (args.Length >= 3 && args[0] is Vector3 force)
                    {
                        float mag = force.magnitude;
                        return $"force=|{mag:F1}|, radius={(args[2] is float r ? r : 0f):F1}";
                    }
                    break;

                case "RPCA_AddForceToBodyPart":
                    if (args.Length >= 2 && args[1] is Vector3 f2)
                        return $"body={args[0]}, forceMag={f2.magnitude:F1}";
                    break;
            }
            return null;
        }

        private static string StatusName(int i)
        {
            switch (i)
            {
                case 0: return "Injury";
                case 1: return "Hunger";
                case 2: return "Cold";
                case 3: return "Poison";
                case 4: return "Crab";
                case 5: return "Curse";
                case 6: return "Drowsy";
                case 7: return "Weight";
                case 8: return "Hot";
                case 9: return "Thorns";
                case 10: return "Spores";
                case 11: return "Web";
                default: return $"STATUS_{i}";
            }
        }

        private static string ResolvePhotonViewOwner(int vid)
        {
            if (vid <= 0) return "?";
            try
            {
                var pv = PhotonNetwork.GetPhotonView(vid);
                if (pv == null) return "?";
                if (!string.IsNullOrEmpty(pv.Owner?.NickName)) return pv.Owner.NickName;
                return pv.gameObject != null ? pv.gameObject.name : "?";
            }
            catch { return "?"; }
        }

        private static string ResolvePhotonViewGameObject(int vid)
        {
            if (vid <= 0) return "?";
            try
            {
                var pv = PhotonNetwork.GetPhotonView(vid);
                if (pv == null) return "?";
                return pv.gameObject != null ? pv.gameObject.name : "?";
            }
            catch { return "?"; }
        }

        private static string BuildTransformPath(Transform t)
        {
            if (t == null) return null;
            try
            {
                _pathStack.Clear();
                int depth = 0;
                while (t != null && depth < 6)
                {
                    _pathStack.Add(t.name);
                    t = t.parent;
                    depth++;
                }
                _pathSb.Clear();
                for (int i = _pathStack.Count - 1; i >= 0; i--)
                {
                    _pathSb.Append(_pathStack[i]);
                    if (i > 0) _pathSb.Append('/');
                }
                return _pathSb.ToString();
            }
            catch { return null; }
        }

        // ═══════════════════════════════════════════════
        //  Periodic Output
        // ═══════════════════════════════════════════════

        public static void OnWindowEnd()
        {
            _windowByMethod.Clear();
        }

        public static void WritePeriodicReport()
        {
            // 确保队列里的残留事件先消费干净
            PumpQueue();

            bool hasWatched = false;
            foreach (var b in _watchedByMethod.Values)
                if (b.Total > 0) { hasWatched = true; break; }
            if (_totalByMethod.Count == 0 && !hasWatched) return;

            AbuseLogger.Write($"[RPC_MON]   Top {TopMethodCount} RPC methods this period:");

            _totalSorted.Clear();
            foreach (var kv in _totalByMethod) _totalSorted.Add(kv);
            _totalSorted.Sort((a, b) => b.Value.CompareTo(a.Value));

            int shown = 0;
            foreach (var kv in _totalSorted)
            {
                string actorsStr = BuildTopActorsString(kv.Key);
                _totalBytesByMethod.TryGetValue(kv.Key, out long tb);
                _maxBytesByMethod.TryGetValue(kv.Key, out int mb);
                int avg = kv.Value > 0 ? (int)(tb / kv.Value) : 0;
                AbuseLogger.WriteRaw(
                    $"          {kv.Key}: {kv.Value}x  avg={avg}B max={mb}B total={tb}B  [{actorsStr}]");
                if (++shown >= TopMethodCount) break;
            }

            if (hasWatched)
            {
                _watchedSorted.Clear();
                foreach (var kv in _watchedByMethod) _watchedSorted.Add(kv);
                _watchedSorted.Sort((a, b) => b.Value.Total.CompareTo(a.Value.Total));

                int totalAll = 0;
                foreach (var kv in _watchedSorted) totalAll += kv.Value.Total;
                AbuseLogger.Write($"[RPC_MON]   Watched RPC records: {totalAll} total across {WatchedMethods.Count} methods (per-method last {WatchedShowPerMethod})");

                foreach (var kv in _watchedSorted)
                {
                    var mb = kv.Value;
                    if (mb.Total == 0) continue;

                    int show = Math.Min(WatchedShowPerMethod, mb.Count);
                    string dropped = mb.Total > mb.Count ? $", dropped={mb.Total - mb.Count}" : "";
                    AbuseLogger.WriteRaw($"          ── {kv.Key}  total={mb.Total}, kept={mb.Count}{dropped}, showing last {show}");

                    int start = (mb.Idx - show + mb.Records.Length) % mb.Records.Length;
                    for (int i = 0; i < show; i++)
                    {
                        var r = mb.Records[(start + i) % mb.Records.Length];
                        AbuseLogger.WriteRaw(
                            $"             [{r.time:F1}s] {r.senderName ?? "?"}#{r.senderActor} → target=({r.targetName ?? "?"}#{r.targetViewID})  payload={r.payloadBytes}B");
                        AbuseLogger.WriteRaw(
                            $"               path={r.targetPath ?? "-"}");
                        AbuseLogger.WriteRaw(
                            $"               {(r.specificDesc != null ? "detail=" + r.specificDesc : "args=[" + (r.argsSummary ?? "-") + "]")}");
                    }
                }
            }

            _totalByMethod.Clear();
            _totalByMethodActor.Clear();
            _totalBytesByMethod.Clear();
            _maxBytesByMethod.Clear();
            foreach (var b in _watchedByMethod.Values) b.Reset();
        }

        private static string BuildTopActorsString(string methodName)
        {
            if (!_totalByMethodActor.TryGetValue(methodName, out var inner) || inner.Count == 0)
                return "";

            _topActorsSorted.Clear();
            foreach (var kv in inner) _topActorsSorted.Add(kv);
            _topActorsSorted.Sort((a, b) => b.Value.CompareTo(a.Value));

            _argsSb.Clear();
            int n = _topActorsSorted.Count > 3 ? 3 : _topActorsSorted.Count;
            for (int i = 0; i < n; i++)
            {
                if (i > 0) _argsSb.Append(", ");
                int actor = _topActorsSorted[i].Key;
                string pn = ResolvePlayerName(actor);
                _argsSb.Append(pn).Append('#').Append(actor).Append(':').Append(_topActorsSorted[i].Value);
            }
            return _argsSb.ToString();
        }

        private static string ResolvePlayerName(int actor)
        {
            if (actor < 0) return "Scene";
            try
            {
                if (!PhotonNetwork.InRoom) return "?";
                foreach (var p in PhotonNetwork.PlayerList)
                    if (p.ActorNumber == actor) return p.NickName ?? "?";
            }
            catch { }
            return "?";
        }

        // ═══════════════════════════════════════════════
        //  Public API for AbuseDetector / Dashboard
        // ═══════════════════════════════════════════════

        public static void LogCurrentWindowTopMethods(int topN)
        {
            if (_windowByMethod.Count == 0) return;
            _totalSorted.Clear();
            foreach (var kv in _windowByMethod) _totalSorted.Add(kv);
            _totalSorted.Sort((a, b) => b.Value.CompareTo(a.Value));

            AbuseLogger.AlertDetail("[RPC_MON]   Current-window top RPC methods:");
            int shown = 0;
            foreach (var kv in _totalSorted)
            {
                AbuseLogger.AlertDetailRaw($"          {kv.Key}: {kv.Value}x");
                if (++shown >= topN) break;
            }
        }

        /// <summary>1.0.3：Dashboard 专用 — 取当前窗口 top N watched 明细（字符串摘要）。</summary>
        public static string GetRecentWatchedSummary(int maxLines)
        {
            _argsSb.Clear();
            int lines = 0;
            foreach (var kv in _watchedByMethod)
            {
                if (lines >= maxLines) break;
                var mb = kv.Value;
                if (mb.Total == 0) continue;
                _argsSb.Append(kv.Key).Append('=').Append(mb.Total).Append(' ');
                lines++;
            }
            return _argsSb.Length == 0 ? "(none)" : _argsSb.ToString();
        }
    }
}
