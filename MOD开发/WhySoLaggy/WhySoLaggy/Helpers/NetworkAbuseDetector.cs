using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using ExitGames.Client.Photon;
using HarmonyLib;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using Hashtable = ExitGames.Client.Photon.Hashtable;

namespace WhySoLaggy
{
    /// <summary>
    /// 炸房/网络滥用监测器（事后调查用，不做防御）。
    /// 监测行为：
    /// 1. 快速大量 Instantiate/Destroy（刷物体炸房）
    /// 2. RPC 洪水（大量 RPC 导致网络阻塞）
    /// 3. 场景对象数量突增（僵尸/PhotonView 短时间暴涨）
    /// 4. 通过 Photon OnEvent 追踪远端玩家的网络事件来源
    /// </summary>
    internal static class NetworkAbuseDetector
    {
        // ── 配置阈值（由 Plugin 传入） ──
        public static int InstantiateRateThreshold = 15;   // 每秒 Instantiate 次数
        public static int DestroyRateThreshold = 20;       // 每秒 Destroy 次数
        public static int RpcRateThreshold = 50;           // 每秒 RPC 次数
        public static int ObjectSpikeThreshold = 30;       // 对象数量单次增量阈值
        public static float CheckIntervalSeconds = 1f;     // 检测周期
        public static float ReportIntervalSeconds = 30f;   // 汇总报告周期

        // ── Photon 事件码 ──
        private const byte EventInstantiate = 202;
        private const byte EventRpc = 200;
        private const byte EventDestroy = 204;
        private const byte EventDestroyPlayer = 207;
        // 1.0.3：PhotonView Ownership 事件码（PUN2 定义在 PunEvent）
        private const byte EventOwnershipRequest = 210;
        private const byte EventOwnershipTransfer = 211;
        private const byte EventOwnershipUpdate = 215;

        // ── 线程安全锁 ──
        private static readonly object _lock = new object();

        // ── 速率计数器（本地 API 调用） ──
        private static int _localInstantiateCount;
        private static int _localDestroyCount;
        private static int _localRpcCount;

        // ── 远端事件计数（通过 OnEvent 捕获） ──
        private static int _remoteInstantiateCount;
        private static int _remoteDestroyCount;
        private static int _remoteRpcCount;

        // ── 按玩家统计（远端事件，键=ActorNumber） ──
        private static readonly Dictionary<int, int> _instantiateByActor = new Dictionary<int, int>();
        private static readonly Dictionary<int, int> _rpcByActor = new Dictionary<int, int>();
        private static readonly Dictionary<int, int> _destroyByActor = new Dictionary<int, int>();

        // ── 1.0.3 功能 E：按真实 Actor × Method 的双键统计 ──
        /// <summary>快窗（CheckInterval=1s 重置）：供阈值告警。</summary>
        private static readonly Dictionary<int, Dictionary<string, int>> _rpcByActorMethod
            = new Dictionary<int, Dictionary<string, int>>();
        /// <summary>慢窗（ReportInterval=30s 重置）：供周期报告打印 Top。</summary>
        private static readonly Dictionary<int, Dictionary<string, int>> _rpcByActorMethodTotal
            = new Dictionary<int, Dictionary<string, int>>();
        /// <summary>单 Actor + 单 Method 每检查窗口内的计数阈值（超过即告警）。</summary>
        public static int ActorMethodRateThreshold = 20;

        // ── 1.0.3 功能 D：Ownership 抢夺频率统计（键=ActorNumber） ──
        /// <summary>快窗：某 Actor 在检查窗口内获取了多少个 PhotonView 的所有权。</summary>
        private static readonly Dictionary<int, int> _ownershipGrabbedByActor = new Dictionary<int, int>();
        /// <summary>单个窗口内 Ownership 转让阈值（超过即告警）。</summary>
        public static int OwnershipGrabRateThreshold = 10;

        // ── 按 Prefab 统计（本地钩子） ──
        private static readonly Dictionary<string, int> _prefabCount = new Dictionary<string, int>();

        // ── 1.0.3：InstantiateTrace 采样控制 ──
        // 每个 prefab 已采集调用栈次数（前 N 次全采，之后按窗口限流）
        private static readonly Dictionary<string, int> _prefabTraceTaken = new Dictionary<string, int>();
        // 上一次采集调用栈的时间（秒），用于窗口限流
        private static float _lastTraceSampleTime;
        // 每个 prefab 前 N 次必采调用栈
        private const int TraceFirstNPerPrefab = 3;
        // 之后的时间窗口（秒）内全局最多采 1 次调用栈
        private const float TraceSampleWindowSeconds = 5f;
        // 下次遇到 Instantiate 洪水时强制采集下一帧所有调用栈（抓爆发源头）
        private static bool _forceTraceNext;

        // ── 1.0.3：Master 端近期客户端 RPC 滑窗（用于将 Instantiate 关联到客户端请求者）
        /// <summary>近期客户端 RPC 条目（realtime，单位秒）。</summary>
        private struct ClientRpcRecord { public float Time; public int Actor; public string Method; }
        private static readonly LinkedList<ClientRpcRecord> _recentClientRpcs = new LinkedList<ClientRpcRecord>();
        /// <summary>Instantiate 关联客户端 RPC 的回看窗口（秒）。</summary>
        private const float SuspectWindowSeconds = 2.5f;
        /// <summary>滑窗最多保持的条数（满才刪）。</summary>
        private const int SuspectWindowMaxEntries = 256;

        // ── 对象计数 ──
        private static int _lastPhotonViewCount;
        private static int _lastZombieCount;
        private static Type _zombieType;

        // ── 计时器 ──
        private static float _checkTimer;
        private static float _reportTimer;
        private static bool _initialized;

        // ── 累计统计（用于周期报告） ──
        private static int _totalInstantiates;
        private static int _totalDestroys;
        private static int _totalRpcs;
        private static int _alertCount;

        /// <summary>
        /// 初始化：注册 Harmony 钩子 + 查找游戏类型。
        /// </summary>
        public static void Initialize(Harmony harmony)
        {
            if (_initialized) return;

            try
            {
                PatchPhotonMethods(harmony);
                FindGameTypes();

                _lastPhotonViewCount = CountPhotonViews();
                _lastZombieCount = CountZombies();

                _initialized = true;
                AbuseLogger.Info("[ABUSE] NetworkAbuseDetector initialized (observation-only mode)");
                AbuseLogger.Info($"[ABUSE] Thresholds: Instantiate={InstantiateRateThreshold}/s, Destroy={DestroyRateThreshold}/s, RPC={RpcRateThreshold}/s, ObjectSpike={ObjectSpikeThreshold}");
                AbuseLogger.Info($"[ABUSE] Initial counts: PhotonViews={_lastPhotonViewCount}, Zombies={_lastZombieCount}");
            }
            catch (Exception ex)
            {
                WhySoLaggyPlugin.Log?.LogError($"[WHY_LAG] NetworkAbuseDetector init failed: {ex}");
            }
        }

        // ═══════════════════════════════════════════════
        //  Photon OnEvent — 远端事件监听（核心改进）
        // ═══════════════════════════════════════════════

        /// <summary>
        /// 由 Plugin 的 IOnEventCallback.OnEvent 调用。
        /// 追踪远端玩家发送的网络事件，精确识别炸房来源。
        /// </summary>
        public static void OnNetworkEvent(byte eventCode, int senderActorNumber)
        {
            if (!_initialized) return;

            // 忽略本地玩家事件（本地已通过 Prefix 钩子追踪）
            if (PhotonNetwork.LocalPlayer != null && senderActorNumber == PhotonNetwork.LocalPlayer.ActorNumber)
                return;

            lock (_lock)
            {
                switch (eventCode)
                {
                    case EventInstantiate:
                        _remoteInstantiateCount++;
                        _totalInstantiates++;
                        IncrementActor(_instantiateByActor, senderActorNumber);
                        break;

                    case EventRpc:
                        _remoteRpcCount++;
                        _totalRpcs++;
                        IncrementActor(_rpcByActor, senderActorNumber);
                        break;

                    case EventDestroy:
                    case EventDestroyPlayer:
                        _remoteDestroyCount++;
                        _totalDestroys++;
                        IncrementActor(_destroyByActor, senderActorNumber);
                        break;
                }
            }
        }

        // ══════════════════════════════════
        //  1.0.3：OnEvent 解包 — 主机端抓客户端 RPC 真实 sender
        // ══════════════════════════════════

        /// <summary>
        /// 从 Photon EventCode=200 的 CustomData 中解出方法名与 viewID，
        /// 结合 photonEvent.Sender 得到的真实客户端 Actor，对白名单高危方法写结构化事件，
        /// 并维护近期客户端 RPC 滑窗供 InstantiateTrace 关联。
        /// </summary>
        public static void OnRemoteRpcEvent(EventData photonEvent)
        {
            if (!_initialized) return;
            if (photonEvent.Code != EventRpc) return;

            int sender = photonEvent.Sender;
            // 跳过本机事件（本机自发 RPC 已由 RpcMonitor 追踪）
            try
            {
                if (PhotonNetwork.LocalPlayer != null && sender == PhotonNetwork.LocalPlayer.ActorNumber)
                    return;
            }
            catch { }

            string methodName = null;
            int viewID = 0;
            int paramCount = 0;
            try
            {
                var data = photonEvent.CustomData as Hashtable;
                if (data != null)
                {
                    // PUN2 rpcData 约定：(byte)0=viewID, (byte)3=methodName(string), 
                    // (byte)5=methodIndex(byte 指向 RpcList), (byte)4=parameters(object[])
                    if (data.ContainsKey((byte)0))
                    {
                        object v = data[(byte)0];
                        if (v is int iv) viewID = iv;
                    }
                    if (data.ContainsKey((byte)3))
                    {
                        methodName = data[(byte)3] as string;
                    }
                    if (string.IsNullOrEmpty(methodName) && data.ContainsKey((byte)5))
                    {
                        // methodIndex → RpcList 查表
                        object idx = data[(byte)5];
                        try
                        {
                            var settings = PhotonNetwork.PhotonServerSettings;
                            var list = settings?.RpcList;
                            if (list != null && idx is byte bi && bi < list.Count)
                                methodName = list[bi];
                        }
                        catch { }
                    }
                    if (data.ContainsKey((byte)4))
                    {
                        var parr = data[(byte)4] as object[];
                        if (parr != null) paramCount = parr.Length;
                    }
                }
            }
            catch (Exception ex)
            {
                WhySoLaggyPlugin.Log?.LogWarning($"[WHY_LAG] OnRemoteRpcEvent unpack failed: {ex.Message}");
            }

            // 写入滑窗（给 InstantiateTrace 关联用）：只在主机端有意义，其他端不写也不错
            if (!string.IsNullOrEmpty(methodName))
            {
                lock (_lock)
                {
                    _recentClientRpcs.AddLast(new ClientRpcRecord
                    {
                        Time = Time.realtimeSinceStartup,
                        Actor = sender,
                        Method = methodName,
                    });
                    if (_recentClientRpcs.Count > SuspectWindowMaxEntries)
                        _recentClientRpcs.RemoveFirst();

                    // 1.0.3 功能 E：双键统计 (actor, method)
                    if (!_rpcByActorMethod.TryGetValue(sender, out var fastInner))
                    {
                        fastInner = new Dictionary<string, int>(StringComparer.Ordinal);
                        _rpcByActorMethod[sender] = fastInner;
                    }
                    fastInner.TryGetValue(methodName, out int fastV);
                    fastInner[methodName] = fastV + 1;

                    if (!_rpcByActorMethodTotal.TryGetValue(sender, out var slowInner))
                    {
                        slowInner = new Dictionary<string, int>(StringComparer.Ordinal);
                        _rpcByActorMethodTotal[sender] = slowInner;
                    }
                    slowInner.TryGetValue(methodName, out int slowV);
                    slowInner[methodName] = slowV + 1;
                }
            }

            // 白名单过滤：只有牵涉到“刻诗进背包/物品操作/危险 RPC”的方法才写结构化事件
            if (string.IsNullOrEmpty(methodName) || !RpcMonitor.WatchedMethods.Contains(methodName))
                return;

            try
            {
                string senderName = null;
                try
                {
                    if (PhotonNetwork.CurrentRoom != null && PhotonNetwork.CurrentRoom.Players != null
                        && PhotonNetwork.CurrentRoom.Players.TryGetValue(sender, out var p))
                        senderName = p?.NickName;
                }
                catch { }

                var fields = new Dictionary<string, object>
                {
                    { "RpcMethod", methodName },
                    { "SenderActor", sender },
                    { "SenderName", senderName ?? "<unknown>" },
                    { "TargetViewID", viewID },
                    { "IsMasterClient", PhotonNetwork.IsMasterClient ? 1 : 0 },
                    { "ArgsSummary", $"params={paramCount}" },
                };
                StructuredLogger.WriteEvent(new StructuredEvent
                {
                    Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"),
                    FrameNumber = Time.frameCount,
                    Type = EventType.RemoteRpcTrace,
                    Fields = fields,
                });
            }
            catch (Exception ex)
            {
                WhySoLaggyPlugin.Log?.LogWarning($"[WHY_LAG] RemoteRpcTrace emit failed: {ex.Message}");
            }
        }

        /// <summary>
        /// 在滑窗中找最贴近 now 且在 SuspectWindowSeconds 内的客户端 RPC。
        /// 返回的 age 以毫秒为单位，找不到返回 false。
        /// </summary>
        private static bool TryFindRecentClientRpc(float now, out int actor, out string method, out int ageMs)
        {
            actor = -1;
            method = null;
            ageMs = -1;

            lock (_lock)
            {
                // 清理过期条目
                while (_recentClientRpcs.Count > 0 &&
                       now - _recentClientRpcs.First.Value.Time > SuspectWindowSeconds)
                {
                    _recentClientRpcs.RemoveFirst();
                }
                if (_recentClientRpcs.Count == 0) return false;
                var last = _recentClientRpcs.Last.Value;
                actor = last.Actor;
                method = last.Method;
                ageMs = (int)((now - last.Time) * 1000f);
                return true;
            }
        }

        // ══════════════════════════════════
        //  1.0.3 功能 D：PhotonView Ownership 审计
        // ══════════════════════════════════

        /// <summary>
        /// 监听 EventCode 210/211/215 的事件，解出 viewID 与对方 Actor，写结构化事件。
        /// 同时累加 _ownershipGrabbedByActor 用于抢夺频率告警。
        /// </summary>
        public static void OnOwnershipEvent(EventData photonEvent)
        {
            if (!_initialized) return;
            byte code = photonEvent.Code;
            if (code != EventOwnershipRequest && code != EventOwnershipTransfer && code != EventOwnershipUpdate)
                return;

            int viewID = 0;
            int otherActor = 0;
            try
            {
                var arr = photonEvent.CustomData as int[];
                if (arr != null && arr.Length >= 2)
                {
                    viewID = arr[0];
                    otherActor = arr[1];
                }
            }
            catch { }

            int sender = photonEvent.Sender;
            string eventName = code == EventOwnershipRequest ? "Request"
                             : code == EventOwnershipTransfer ? "Transfer"
                             : "Update";

            // Transfer/Update 事件中的“获取者”才计入抢夺计数；arr[1]=新 owner
            int grabber = (code == EventOwnershipTransfer || code == EventOwnershipUpdate) ? otherActor : sender;
            if (grabber > 0)
            {
                lock (_lock)
                {
                    _ownershipGrabbedByActor.TryGetValue(grabber, out int g);
                    _ownershipGrabbedByActor[grabber] = g + 1;
                }
            }

            try
            {
                var fields = new Dictionary<string, object>
                {
                    { "RpcMethod", "Ownership" + eventName },
                    { "SenderActor", sender },
                    { "TargetViewID", viewID },
                    { "ArgsSummary", $"otherActor={otherActor}" },
                };
                try
                {
                    if (PhotonNetwork.CurrentRoom != null && PhotonNetwork.CurrentRoom.Players != null
                        && PhotonNetwork.CurrentRoom.Players.TryGetValue(sender, out var p))
                        fields["SenderName"] = p?.NickName;
                }
                catch { }
                StructuredLogger.WriteEvent(new StructuredEvent
                {
                    Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"),
                    FrameNumber = Time.frameCount,
                    Type = EventType.OwnershipChange,
                    Fields = fields,
                });
            }
            catch (Exception ex)
            {
                WhySoLaggyPlugin.Log?.LogWarning($"[WHY_LAG] OwnershipChange emit failed: {ex.Message}");
            }
        }

        /// <summary>每帧调用，执行周期性检测和报告。</summary>
        public static void Tick()
        {
            if (!_initialized) return;

            float dt = Time.unscaledDeltaTime;
            _checkTimer += dt;
            _reportTimer += dt;

            if (_checkTimer >= CheckIntervalSeconds)
            {
                CheckRates();
                CheckObjectSpike();
                // 1.0.3：Actor×Method 和 Ownership 抢夺的阈值告警
                CheckActorMethodHotspots();
                CheckOwnershipGrab();
                ResetCounters();
                RpcMonitor.OnWindowEnd();
                _checkTimer = 0f;
            }

            if (_reportTimer >= ReportIntervalSeconds)
            {
                WritePeriodicReport();
                ResetReportStats();
                _reportTimer = 0f;
            }
        }

        // ═══════════════════════════════════════════════
        //  Harmony Patches（本地 API 调用追踪）
        // ═══════════════════════════════════════════════

        private static void PatchPhotonMethods(Harmony harmony)
        {
            // --- PhotonNetwork.Instantiate ---
            var instantiateMethods = typeof(PhotonNetwork).GetMethods(BindingFlags.Public | BindingFlags.Static);
            foreach (var method in instantiateMethods)
            {
                if (method.Name == "Instantiate" || method.Name == "InstantiateRoomObject")
                {
                    try
                    {
                        harmony.Patch(method,
                            prefix: new HarmonyMethod(typeof(NetworkAbuseDetector), nameof(OnInstantiatePrefix)));
                    }
                    catch (Exception ex)
                    {
                        AbuseLogger.Info($"[ABUSE] Failed to patch {method.Name}: {ex.Message}");
                    }
                }
            }

            // --- PhotonNetwork.Destroy ---
            var destroyMethods = typeof(PhotonNetwork).GetMethods(BindingFlags.Public | BindingFlags.Static);
            foreach (var method in destroyMethods)
            {
                if (method.Name == "Destroy")
                {
                    try
                    {
                        harmony.Patch(method,
                            prefix: new HarmonyMethod(typeof(NetworkAbuseDetector), nameof(OnDestroyPrefix)));
                    }
                    catch (Exception ex)
                    {
                        AbuseLogger.Info($"[ABUSE] Failed to patch Destroy: {ex.Message}");
                    }
                }
            }

            // --- PhotonView.RPC ---
            var rpcMethods = typeof(PhotonView).GetMethods(BindingFlags.Public | BindingFlags.Instance);
            foreach (var method in rpcMethods)
            {
                if (method.Name == "RPC")
                {
                    try
                    {
                        harmony.Patch(method,
                            prefix: new HarmonyMethod(typeof(NetworkAbuseDetector), nameof(OnRpcPrefix)));
                    }
                    catch (Exception ex)
                    {
                        AbuseLogger.Info($"[ABUSE] Failed to patch RPC: {ex.Message}");
                    }
                }
            }

            AbuseLogger.Info("[ABUSE] Photon method hooks registered (local API tracking)");
        }

        private static void FindGameTypes()
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (_zombieType != null) break;
                try
                {
                    foreach (var t in asm.GetTypes())
                    {
                        if (t.Name == "MushroomZombie")
                        {
                            _zombieType = t;
                            AbuseLogger.Info($"[ABUSE] Found zombie type: {t.FullName}");
                            break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    // 1.0.3：不再吞错，记录程序集名
                    WhySoLaggyPlugin.Log?.LogWarning($"[WHY_LAG] FindGameTypes scan failed in asm={asm.GetName().Name}: {ex.Message}");
                }
            }

            if (_zombieType == null)
                AbuseLogger.Info("[ABUSE] MushroomZombie type not found (zombie counting disabled)");
        }

        // ═══════════════════════════════════════════════
        //  Harmony Prefix Callbacks（本地调用）
        // ═══════════════════════════════════════════════

        static void OnInstantiatePrefix(string prefabName, Vector3 position)
        {
            int localActor;
            bool isMaster;
            int totalSoFar;
            bool shouldTakeTrace = false;

            lock (_lock)
            {
                _localInstantiateCount++;
                _totalInstantiates++;
                totalSoFar = _totalInstantiates;

                if (!string.IsNullOrEmpty(prefabName))
                {
                    if (!_prefabCount.ContainsKey(prefabName))
                        _prefabCount[prefabName] = 0;
                    _prefabCount[prefabName]++;

                    // 调用栈采样：① 每 prefab 前 N 次 ② 每窗口全局 1 次 ③ 洪水强制
                    int taken;
                    _prefabTraceTaken.TryGetValue(prefabName, out taken);
                    if (taken < TraceFirstNPerPrefab)
                    {
                        shouldTakeTrace = true;
                        _prefabTraceTaken[prefabName] = taken + 1;
                    }
                    else if (_forceTraceNext)
                    {
                        shouldTakeTrace = true;
                        _forceTraceNext = false;
                    }
                    else if (Time.realtimeSinceStartup - _lastTraceSampleTime >= TraceSampleWindowSeconds)
                    {
                        shouldTakeTrace = true;
                        _lastTraceSampleTime = Time.realtimeSinceStartup;
                    }
                }
            }

            // 下面的写日志/取栈放在锁外（I/O + 栈分析较慢，不必阻塞其他计数器）
            try
            {
                localActor = (PhotonNetwork.LocalPlayer != null) ? PhotonNetwork.LocalPlayer.ActorNumber : -1;
                isMaster = PhotonNetwork.IsMasterClient;
            }
            catch
            {
                localActor = -1;
                isMaster = false;
            }

            string trace = null;
            string caller = null;
            if (shouldTakeTrace)
            {
                ExtractCallerStack(out trace, out caller);
            }

            try
            {
                var fields = new Dictionary<string, object>
                {
                    { "PrefabName", prefabName ?? "<null>" },
                    { "IsMasterClient", isMaster ? 1 : 0 },
                    { "Position", $"{position.x:F1},{position.y:F1},{position.z:F1}" },
                    { "LocalActor", localActor },
                };
                if (!string.IsNullOrEmpty(trace)) fields["TraceStack"] = trace;
                if (!string.IsNullOrEmpty(caller)) fields["TraceCaller"] = caller;

                // 1.0.3：主机端将本次 Instantiate 关联到近期客户端 RPC（请求者追踪）
                if (isMaster)
                {
                    int reqActor, ageMs;
                    string reqMethod;
                    if (TryFindRecentClientRpc(Time.realtimeSinceStartup, out reqActor, out reqMethod, out ageMs))
                    {
                        fields["SuspectedRequesterActor"] = reqActor;
                        fields["SuspectedRequesterRpc"] = reqMethod ?? "<?>";
                        fields["SuspectedAgeMs"] = ageMs;
                        try
                        {
                            if (PhotonNetwork.CurrentRoom != null && PhotonNetwork.CurrentRoom.Players != null
                                && PhotonNetwork.CurrentRoom.Players.TryGetValue(reqActor, out var p))
                                fields["SuspectedRequesterName"] = p?.NickName ?? "<unknown>";
                        }
                        catch { }
                    }
                }

                StructuredLogger.WriteEvent(new StructuredEvent
                {
                    Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"),
                    FrameNumber = Time.frameCount,
                    Type = EventType.InstantiateTrace,
                    Fields = fields,
                });
            }
            catch (Exception ex)
            {
                WhySoLaggyPlugin.Log?.LogWarning($"[WHY_LAG] InstantiateTrace emit failed: {ex.Message}");
            }
        }

        /// <summary>
        /// 抽取第一个「非 Photon/Harmony/UnityEngine/WhySoLaggy」的业务帧作为 caller，
        /// 帮助快速定位触发 Instantiate 的源头（比如某 MOD 的类名）。
        /// </summary>
        private static void ExtractCallerStack(out string traceOut, out string callerOut)
        {
            traceOut = null;
            callerOut = null;
            try
            {
                var st = new System.Diagnostics.StackTrace(2, false);
                int frames = st.FrameCount;
                var sb = new StringBuilder();
                int emitted = 0;
                for (int i = 0; i < frames && emitted < 12; i++)
                {
                    var m = st.GetFrame(i).GetMethod();
                    if (m == null) continue;
                    var dt = m.DeclaringType;
                    string dtName = (dt != null) ? dt.FullName : "<?>";
                    if (sb.Length > 0) sb.Append(" | ");
                    sb.Append(dtName).Append('.').Append(m.Name);
                    emitted++;

                    if (callerOut == null && dt != null)
                    {
                        string ns = dt.Namespace ?? string.Empty;
                        if (!ns.StartsWith("Photon") && !ns.StartsWith("HarmonyLib")
                            && !ns.StartsWith("UnityEngine") && !ns.StartsWith("System")
                            && ns != "WhySoLaggy")
                        {
                            callerOut = dtName + "." + m.Name;
                        }
                    }
                }
                traceOut = sb.ToString();
            }
            catch { /* 取栈失败不影响主流程 */ }
        }

        /// <summary>供 Instantiate 洪水告警触发时调用，强制下一次 Instantiate 采栈。</summary>
        public static void ForceTraceNextInstantiate() { _forceTraceNext = true; }

        static void OnDestroyPrefix()
        {
            lock (_lock)
            {
                _localDestroyCount++;
                _totalDestroys++;
            }
        }

        static void OnRpcPrefix(PhotonView __instance, string methodName)
        {
            lock (_lock)
            {
                _localRpcCount++;
                _totalRpcs++;
            }
        }

        // ═══════════════════════════════════════════════
        //  Detection Logic
        // ═══════════════════════════════════════════════

        private static bool IsChinese =>
            Application.systemLanguage == SystemLanguage.Chinese
            || Application.systemLanguage == SystemLanguage.ChineseSimplified
            || Application.systemLanguage == SystemLanguage.ChineseTraditional;

        private static void CheckRates()
        {
            float interval = CheckIntervalSeconds;

            int totalInst, totalDest, totalRpc;
            Dictionary<int, int> instByActor, rpcByActor, destByActor;

            lock (_lock)
            {
                totalInst = _localInstantiateCount + _remoteInstantiateCount;
                totalDest = _localDestroyCount + _remoteDestroyCount;
                totalRpc = _localRpcCount + _remoteRpcCount;

                instByActor = new Dictionary<int, int>(_instantiateByActor);
                rpcByActor = new Dictionary<int, int>(_rpcByActor);
                destByActor = new Dictionary<int, int>(_destroyByActor);
            }

            float instRate = totalInst / interval;
            float destroyRate = totalDest / interval;
            float rpcRate = totalRpc / interval;

            if (instRate >= InstantiateRateThreshold)
            {
                _alertCount++;
                string logMsg = $"Instantiate flood! Rate: {instRate:F1}/s (threshold: {InstantiateRateThreshold}/s)";
                AbuseLogger.Alert(logMsg);
                string uiMsg = IsChinese
                    ? $"⚠ 刷物体洪水！速率: {instRate:F1}/秒（阈值: {InstantiateRateThreshold}/秒）"
                    : $"⚠ Instantiate flood! Rate: {instRate:F1}/s (threshold: {InstantiateRateThreshold}/s)";
                AbuseNotificationUI.Show(uiMsg);
                LogTopActors(instByActor, "Instantiate");
                LogTopPrefabs();
                EmitAbuseAlertEvent("InstantiateFlood", instRate, InstantiateRateThreshold, instByActor);
                // 1.0.3：强制下一次 Instantiate 采栈，抓洪水源头
                ForceTraceNextInstantiate();
            }
        
            if (destroyRate >= DestroyRateThreshold)
            {
                _alertCount++;
                string logMsg = $"Destroy flood! Rate: {destroyRate:F1}/s (threshold: {DestroyRateThreshold}/s)";
                AbuseLogger.Alert(logMsg);
                string uiMsg = IsChinese
                    ? $"⚠ 大量销毁！速率: {destroyRate:F1}/秒（阈值: {DestroyRateThreshold}/秒）"
                    : $"⚠ Destroy flood! Rate: {destroyRate:F1}/s (threshold: {DestroyRateThreshold}/s)";
                AbuseNotificationUI.Show(uiMsg);
                LogTopActors(destByActor, "Destroy");
                EmitAbuseAlertEvent("DestroyFlood", destroyRate, DestroyRateThreshold, destByActor);
            }
        
            if (rpcRate >= RpcRateThreshold)
            {
                _alertCount++;
                string logMsg = $"RPC flood! Rate: {rpcRate:F1}/s (threshold: {RpcRateThreshold}/s)";
                AbuseLogger.Alert(logMsg);
                string uiMsg = IsChinese
                    ? $"⚠ RPC洪水！速率: {rpcRate:F1}/秒（阈值: {RpcRateThreshold}/秒）"
                    : $"⚠ RPC flood! Rate: {rpcRate:F1}/s (threshold: {RpcRateThreshold}/s)";
                AbuseNotificationUI.Show(uiMsg);
                LogTopActors(rpcByActor, "RPC");
                // 联动输出当前窗口 top RPC 方法名，让核查更直接
                RpcMonitor.LogCurrentWindowTopMethods(5);
                EmitAbuseAlertEvent("RpcFlood", rpcRate, RpcRateThreshold, rpcByActor);
            }
        }
        
        // 1.0.3：AbuseAlert 结构化事件发射
        private static void EmitAbuseAlertEvent(string alertType, float rate, int threshold, Dictionary<int, int> actorMap)
        {
            try
            {
                int topActor = -1;
                int topCount = 0;
                if (actorMap != null)
                {
                    foreach (var kv in actorMap)
                        if (kv.Value > topCount) { topActor = kv.Key; topCount = kv.Value; }
                }
                var fields = new Dictionary<string, object>
                {
                    { "AlertType", alertType },
                    { "Rate", Math.Round((double)rate, 2) },
                    { "Threshold", threshold },
                };
                if (topActor >= 0)
                {
                    fields["TopActor"] = topActor;
                    fields["TopActorName"] = ResolvePlayerName(topActor);
                }
                try { fields["Ping"] = PhotonNetwork.GetPing(); } catch { }
        
                StructuredLogger.WriteEvent(new StructuredEvent
                {
                    Timestamp = StructuredLogger.NowStamp(),
                    FrameNumber = Time.frameCount,
                    Type = EventType.AbuseAlert,
                    Fields = fields,
                });
            }
            catch (Exception ex)
            {
                WhySoLaggyPlugin.Log?.LogWarning($"[WHY_LAG] NetworkAbuseDetector EmitAbuseAlert failed: {ex.Message}");
            }
        }

        // ══════════════════════════════════
        //  1.0.3 功能 D + E 的阈值告警
        // ══════════════════════════════════

        /// <summary>功能 E：任何 (actor, method) 在窗口内超过 ActorMethodRateThreshold 即告警。</summary>
        private static void CheckActorMethodHotspots()
        {
            List<(int actor, string method, int count)> hits = null;
            lock (_lock)
            {
                foreach (var kv in _rpcByActorMethod)
                {
                    foreach (var inner in kv.Value)
                    {
                        if (inner.Value >= ActorMethodRateThreshold)
                        {
                            if (hits == null) hits = new List<(int, string, int)>(4);
                            hits.Add((kv.Key, inner.Key, inner.Value));
                        }
                    }
                }
            }
            if (hits == null) return;

            foreach (var (actor, method, count) in hits)
            {
                _alertCount++;
                string name = TryGetNickName(actor);
                string logMsg = $"Actor×Method hotspot! Actor #{actor} ({name}) sent '{method}' x{count} in {CheckIntervalSeconds:F1}s (threshold: {ActorMethodRateThreshold})";
                AbuseLogger.Alert(logMsg);
                string uiMsg = IsChinese
                    ? $"⚠ 客户端 RPC 热点！#{actor} {name} {method} ×{count}"
                    : $"⚠ Actor×Method hotspot! #{actor} {name} {method} ×{count}";
                AbuseNotificationUI.Show(uiMsg);
                try
                {
                    StructuredLogger.WriteEvent(new StructuredEvent
                    {
                        Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"),
                        FrameNumber = Time.frameCount,
                        Type = EventType.AbuseAlert,
                        Fields = new Dictionary<string, object>
                        {
                            { "AlertType", "ActorMethodHotspot" },
                            { "TopActor", actor },
                            { "TopActorName", name },
                            { "RpcMethod", method },
                            { "CurrentCount", count },
                            { "Threshold", ActorMethodRateThreshold },
                        },
                    });
                }
                catch { }
            }
        }

        /// <summary>功能 D：任何玩家在窗口内 Ownership 转让次数 >= OwnershipGrabRateThreshold 即告警。</summary>
        private static void CheckOwnershipGrab()
        {
            List<(int actor, int count)> hits = null;
            lock (_lock)
            {
                foreach (var kv in _ownershipGrabbedByActor)
                {
                    if (kv.Value >= OwnershipGrabRateThreshold)
                    {
                        if (hits == null) hits = new List<(int, int)>(2);
                        hits.Add((kv.Key, kv.Value));
                    }
                }
            }
            if (hits == null) return;

            foreach (var (actor, count) in hits)
            {
                _alertCount++;
                string name = TryGetNickName(actor);
                string logMsg = $"Ownership grab! Actor #{actor} ({name}) took {count} PhotonView ownerships in {CheckIntervalSeconds:F1}s (threshold: {OwnershipGrabRateThreshold})";
                AbuseLogger.Alert(logMsg);
                string uiMsg = IsChinese
                    ? $"⚠ 所有权抢夺！#{actor} {name} ■{count}"
                    : $"⚠ Ownership grab! #{actor} {name} ×{count}";
                AbuseNotificationUI.Show(uiMsg);
                try
                {
                    StructuredLogger.WriteEvent(new StructuredEvent
                    {
                        Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"),
                        FrameNumber = Time.frameCount,
                        Type = EventType.AbuseAlert,
                        Fields = new Dictionary<string, object>
                        {
                            { "AlertType", "OwnershipGrab" },
                            { "TopActor", actor },
                            { "TopActorName", name },
                            { "CurrentCount", count },
                            { "Threshold", OwnershipGrabRateThreshold },
                        },
                    });
                }
                catch { }
            }
        }

        /// <summary>由 ActorNumber 安全查 NickName，失败返回 "?"。</summary>
        private static string TryGetNickName(int actor)
        {
            try
            {
                if (PhotonNetwork.CurrentRoom != null && PhotonNetwork.CurrentRoom.Players != null
                    && PhotonNetwork.CurrentRoom.Players.TryGetValue(actor, out var p))
                    return p?.NickName ?? "?";
            }
            catch { }
            return "?";
        }

        private static void CheckObjectSpike()
        {
            int currentPV = CountPhotonViews();
            int currentZombie = CountZombies();

            int pvDelta = currentPV - _lastPhotonViewCount;
            int zombieDelta = currentZombie - _lastZombieCount;

            if (pvDelta >= ObjectSpikeThreshold)
            {
                _alertCount++;
                string logMsg = $"PhotonView spike! +{pvDelta} in {CheckIntervalSeconds}s (now: {currentPV})";
                AbuseLogger.Alert(logMsg);
                string uiMsg = IsChinese
                    ? $"⚠ 对象突增！+{pvDelta} 个/{CheckIntervalSeconds}秒（当前: {currentPV}）"
                    : $"⚠ PhotonView spike! +{pvDelta} in {CheckIntervalSeconds}s (now: {currentPV})";
                AbuseNotificationUI.Show(uiMsg);
                string topOwners = InvestigatePhotonViewOwners();
                EmitSpikeEvent("PhotonViewSpike", pvDelta, currentPV, topOwners);
            }
            
            if (zombieDelta >= ObjectSpikeThreshold)
            {
                _alertCount++;
                string logMsg = $"Zombie spike! +{zombieDelta} in {CheckIntervalSeconds}s (now: {currentZombie})";
                AbuseLogger.Alert(logMsg);
                string uiMsg = IsChinese
                    ? $"⚠ 僵尸突增！+{zombieDelta} 个/{CheckIntervalSeconds}秒（当前: {currentZombie}）"
                    : $"⚠ Zombie spike! +{zombieDelta} in {CheckIntervalSeconds}s (now: {currentZombie})";
                AbuseNotificationUI.Show(uiMsg);
                EmitSpikeEvent("ZombieSpike", zombieDelta, currentZombie);
            }

            _lastPhotonViewCount = currentPV;
            _lastZombieCount = currentZombie;
        }

        // ═══════════════════════════════════════════════
        //  Object Counting
        // ═══════════════════════════════════════════════

        private static int CountPhotonViews()
        {
            try
            {
                int count = 0;
                foreach (var pv in PhotonNetwork.PhotonViewCollection)
                    count++;
                return count;
            }
            catch { return 0; }
        }

        private static int CountZombies()
        {
            if (_zombieType == null) return 0;
            try
            {
                var objs = UnityEngine.Object.FindObjectsByType(_zombieType, FindObjectsSortMode.None);
                return objs?.Length ?? 0;
            }
            catch { return 0; }
        }

        // 1.0.4：改为返回 Top5 嫌疑人字符串（格式 "Name#id:count;Name#id:count;..."），供 EmitSpikeEvent 落盘
        private static string InvestigatePhotonViewOwners()
        {
            try
            {
                var ownerCount = new Dictionary<string, int>();
                foreach (var pv in PhotonNetwork.PhotonViewCollection)
                {
                    string owner = "Scene";
                    if (pv != null && pv.Owner != null)
                        owner = $"{pv.Owner.NickName ?? "?"}#{pv.Owner.ActorNumber}";

                    if (!ownerCount.ContainsKey(owner))
                        ownerCount[owner] = 0;
                    ownerCount[owner]++;
                }

                AbuseLogger.Write("[ABUSE]   PhotonView owner distribution:");
                var sorted = new List<KeyValuePair<string, int>>(ownerCount);
                sorted.Sort((a, b) => b.Value.CompareTo(a.Value));
                foreach (var kv in sorted)
                {
                    AbuseLogger.WriteRaw($"          {kv.Key}: {kv.Value} objects");
                }

                // 通知占比最大的嫌疑人
                if (sorted.Count > 0 && sorted[0].Value > ObjectSpikeThreshold)
                {
                    string uiMsg = IsChinese
                        ? $"  → 最大持有: {sorted[0].Key}（{sorted[0].Value} 个对象）"
                        : $"  → Top owner: {sorted[0].Key} ({sorted[0].Value} objs)";
                    AbuseNotificationUI.Show(uiMsg);
                }

                if (sorted.Count == 0) return null;
                var topSb = new StringBuilder();
                int topN = sorted.Count < 5 ? sorted.Count : 5;
                for (int i = 0; i < topN; i++)
                {
                    if (i > 0) topSb.Append(';');
                    topSb.Append(sorted[i].Key).Append(':').Append(sorted[i].Value);
                }
                return topSb.ToString();
            }
            catch (Exception ex)
            {
                AbuseLogger.Write($"[ABUSE]   Failed to investigate owners: {ex.Message}");
                return null;
            }
        }

        // ═══════════════════════════════════════════════
        //  Reporting
        // ═══════════════════════════════════════════════

        private static void WritePeriodicReport()
        {
            AbuseLogger.Write(new string('─', 50));
            AbuseLogger.Write("[ABUSE] ═══ Periodic Abuse Report ═══");
            if (PhotonNetwork.InRoom)
            {
                var room = PhotonNetwork.CurrentRoom;
                AbuseLogger.Write($"[ABUSE]   Room: {room?.Name ?? "?"}, Players: {room?.PlayerCount ?? 0}/{room?.MaxPlayers ?? 0}");
            }
            else
            {
                AbuseLogger.Write("[ABUSE]   Not in room");
            }

            AbuseLogger.Write($"[ABUSE]   Current objects: PhotonViews={_lastPhotonViewCount}, Zombies={_lastZombieCount}");
            
            // 1.0.3：采集 Ping
            int ping = -1;
            try { ping = PhotonNetwork.GetPing(); } catch { }
            if (ping >= 0)
                AbuseLogger.Write($"[ABUSE]   Photon Ping: {ping} ms");
            
            float elapsed = ReportIntervalSeconds;
            AbuseLogger.Write($"[ABUSE]   Period totals ({elapsed:F0}s): Instantiates={_totalInstantiates}, Destroys={_totalDestroys}, RPCs={_totalRpcs}");
            AbuseLogger.Write($"[ABUSE]   Alerts triggered: {_alertCount}");
            
            // 1.0.3：PeriodicReport 结构化事件
            // 1.0.4：补齐文本日志里已存在但此前丢失的字段（房间、玩家数、Instantiate/Destroy/RPC 总量、僵尸数等）
            try
            {
                string roomName = null; int playerCount = 0, maxPlayers = 0;
                if (PhotonNetwork.InRoom)
                {
                    var rm = PhotonNetwork.CurrentRoom;
                    roomName = rm?.Name;
                    playerCount = rm?.PlayerCount ?? 0;
                    maxPlayers = rm?.MaxPlayers ?? 0;
                }
                StructuredLogger.WriteEvent(new StructuredEvent
                {
                    Timestamp = StructuredLogger.NowStamp(),
                    FrameNumber = Time.frameCount,
                    Type = EventType.PeriodicReport,
                    Fields = new Dictionary<string, object>
                    {
                        { "AlertType", "PeriodicReport" },
                        { "CurrentCount", _lastPhotonViewCount },
                        { "Delta", _totalInstantiates - _totalDestroys },
                        { "TotalInstantiates", _totalInstantiates },
                        { "TotalDestroys", _totalDestroys },
                        { "TotalRpcs", _totalRpcs },
                        { "AlertCount", _alertCount },
                        { "ZombieCount", _lastZombieCount },
                        { "RoomName", roomName ?? "" },
                        { "PlayerCount", playerCount },
                        { "MaxPlayers", maxPlayers },
                        { "Ping", ping },
                    },
                });
            }
            catch (Exception ex)
            {
                WhySoLaggyPlugin.Log?.LogWarning($"[WHY_LAG] NetworkAbuseDetector PeriodicReport event failed: {ex.Message}");
            }

            // Top prefabs
            lock (_lock)
            {
                if (_prefabCount.Count > 0)
                {
                    AbuseLogger.Write("[ABUSE]   Top spawned prefabs:");
                    var sorted = new List<KeyValuePair<string, int>>(_prefabCount);
                    sorted.Sort((a, b) => b.Value.CompareTo(a.Value));
                    int shown = 0;
                    foreach (var kv in sorted)
                    {
                        AbuseLogger.WriteRaw($"          {kv.Key}: {kv.Value}x");
                        if (++shown >= 5) break;
                    }
                }
            }

            // 玩家列表
            if (PhotonNetwork.InRoom)
            {
                AbuseLogger.Write("[ABUSE]   Players in room:");
                foreach (var player in PhotonNetwork.PlayerList)
                {
                    string tag = player.IsMasterClient ? " [Master]" : "";
                    AbuseLogger.WriteRaw($"          #{player.ActorNumber} {player.NickName}{tag}");
                }
            }

            // 1.0.3 功能 E：Top Actor×Method（只限客户端发来的 RPC）
            lock (_lock)
            {
                if (_rpcByActorMethodTotal.Count > 0)
                {
                    AbuseLogger.Write("[ABUSE]   Top client RPCs (Actor×Method):");
                    var flat = new List<(int actor, string method, int count)>();
                    foreach (var kv in _rpcByActorMethodTotal)
                        foreach (var inner in kv.Value)
                            flat.Add((kv.Key, inner.Key, inner.Value));
                    flat.Sort((a, b) => b.count.CompareTo(a.count));
                    int shown = 0;
                    foreach (var (actor, method, count) in flat)
                    {
                        string nm = TryGetNickName(actor);
                        AbuseLogger.WriteRaw($"          #{actor} {nm}: {method} ×{count}");
                        if (++shown >= 10) break;
                    }
                }
            }

            AbuseLogger.Write(new string('─', 50));

            // 联动输出 RpcMonitor 的周期报告（top 方法 + watched 明细）
            RpcMonitor.WritePeriodicReport();
        }

        // ═══════════════════════════════════════════════
        //  Helpers
        // ═══════════════════════════════════════════════

        // 1.0.3：ObjectSpike 事件发射；1.0.4：新增 topOwners 参数透传 PhotonView 嫌疑人分布
        private static void EmitSpikeEvent(string alertType, int delta, int currentCount, string topOwners = null)
        {
            try
            {
                var fields = new Dictionary<string, object>
                {
                    { "AlertType", alertType },
                    { "Delta", delta },
                    { "CurrentCount", currentCount },
                    { "Threshold", ObjectSpikeThreshold },
                };
                if (!string.IsNullOrEmpty(topOwners)) fields["TopOwners"] = topOwners;
                try { fields["Ping"] = PhotonNetwork.GetPing(); } catch { }
                StructuredLogger.WriteEvent(new StructuredEvent
                {
                    Timestamp = StructuredLogger.NowStamp(),
                    FrameNumber = Time.frameCount,
                    Type = EventType.AbuseAlert,
                    Fields = fields,
                });
            }
            catch (Exception ex)
            {
                WhySoLaggyPlugin.Log?.LogWarning($"[WHY_LAG] NetworkAbuseDetector EmitSpike failed: {ex.Message}");
            }
        }

        private static void ResetCounters()
        {
            lock (_lock)
            {
                _localInstantiateCount = 0;
                _localDestroyCount = 0;
                _localRpcCount = 0;
                _remoteInstantiateCount = 0;
                _remoteDestroyCount = 0;
                _remoteRpcCount = 0;
                _instantiateByActor.Clear();
                _rpcByActor.Clear();
                _destroyByActor.Clear();
                // 1.0.3：快窗重置
                _rpcByActorMethod.Clear();
                _ownershipGrabbedByActor.Clear();
            }
        }

        private static void ResetReportStats()
        {
            lock (_lock)
            {
                _totalInstantiates = 0;
                _totalDestroys = 0;
                _totalRpcs = 0;
                _alertCount = 0;
                _prefabCount.Clear();
                // 1.0.3：慢窗重置
                _rpcByActorMethodTotal.Clear();
            }
        }

        private static void IncrementActor(Dictionary<int, int> dict, int actorNumber)
        {
            if (!dict.ContainsKey(actorNumber))
                dict[actorNumber] = 0;
            dict[actorNumber]++;
        }

        private static void LogTopActors(Dictionary<int, int> dict, string action)
        {
            if (dict.Count == 0) return;
            var sorted = new List<KeyValuePair<int, int>>(dict);
            sorted.Sort((a, b) => b.Value.CompareTo(a.Value));

            AbuseLogger.AlertDetail($"[ABUSE]   Top {action} sources (by ActorNumber):");
            int shown = 0;
            foreach (var kv in sorted)
            {
                string playerName = ResolvePlayerName(kv.Key);
                AbuseLogger.AlertDetailRaw($"          {playerName}#{kv.Key}: {kv.Value}x");
                if (++shown >= 5) break;
            }

            // 通知 UI 显示最大嫌疑人
            if (sorted.Count > 0)
            {
                string topName = ResolvePlayerName(sorted[0].Key);
                string uiMsg = IsChinese
                    ? $"  → 嫌疑人: {topName}#{sorted[0].Key}（{sorted[0].Value}次 {action}）"
                    : $"  → Suspect: {topName}#{sorted[0].Key} ({sorted[0].Value}x {action})";
                AbuseNotificationUI.Show(uiMsg);
            }
        }

        private static void LogTopPrefabs()
        {
            lock (_lock)
            {
                if (_prefabCount.Count == 0) return;
                var sorted = new List<KeyValuePair<string, int>>(_prefabCount);
                sorted.Sort((a, b) => b.Value.CompareTo(a.Value));

                AbuseLogger.AlertDetail("[ABUSE]   Top prefabs this interval:");
                int shown = 0;
                foreach (var kv in sorted)
                {
                    AbuseLogger.AlertDetailRaw($"          {kv.Key}: {kv.Value}x");
                    if (++shown >= 5) break;
                }
            }
        }

        private static string ResolvePlayerName(int actorNumber)
        {
            try
            {
                if (!PhotonNetwork.InRoom) return "?";
                foreach (var p in PhotonNetwork.PlayerList)
                {
                    if (p.ActorNumber == actorNumber)
                        return p.NickName ?? "?";
                }
            }
            catch { }
            return "?";
        }
    }
}
