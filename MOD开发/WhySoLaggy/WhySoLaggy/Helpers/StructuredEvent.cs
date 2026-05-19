using System.Collections.Generic;

namespace WhySoLaggy
{
    /// <summary>
    /// 结构化事件类型（1.0.3 新增）。
    /// </summary>
    internal enum EventType
    {
        FpsReport,
        PluginTiming,
        PatchTiming,
        SpikeFrame,
        AbuseAlert,
        RpcCall,
        PeriodicReport,
        HarmonyPatchMap,
        MethodTrace,
        /// <summary>1.0.3 新增：PhotonNetwork.Instantiate 调用追踪，抓刷物品源头。</summary>
        InstantiateTrace,
        /// <summary>1.0.3 新增：从 Photon OnEvent 解包得到的远端客户端 RPC 事件（主机端抓客户端真实 sender）。</summary>
        RemoteRpcTrace,
        /// <summary>1.0.3 新增：PhotonView Ownership 转让/请求事件（EventCode 210/211/215）。</summary>
        OwnershipChange,
    }

    /// <summary>
    /// 结构化事件（1.0.3 新增）：由各采集模块构造，送入 StructuredLogger 后
    /// 按固定列顺序落 CSV、按字段字典落 JSONL。
    /// </summary>
    internal struct StructuredEvent
    {
        /// <summary>ISO-8601 时间戳（yyyy-MM-dd HH:mm:ss.fff）。</summary>
        public string Timestamp;
        /// <summary>Unity 帧号（Time.frameCount）。</summary>
        public long FrameNumber;
        /// <summary>事件类型。</summary>
        public EventType Type;
        /// <summary>
        /// 自定义字段：键名与 CSV 列名大小写一致（如 "AvgFps"、"RpcMethod"）。
        /// 未在 CSV 列表中的键会被忽略（CSV），但全部写入 JSONL。
        /// </summary>
        public Dictionary<string, object> Fields;
    }
}
