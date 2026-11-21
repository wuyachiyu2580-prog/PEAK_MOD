using System;
using System.Collections.Generic;
using System.Text;

namespace WhySoLaggy
{
    internal sealed class CaptureNetworkIdentity
    {
        internal static readonly string[] Columns = { "SessionId", "CaptureId", "Validity", "RpcMethod", "CpuGpuStatus" };
        internal readonly string Key;
        internal readonly Dictionary<string, object> Strings;
        internal readonly int Bytes;
        internal int References;
        internal static string KeyOf(Dictionary<string, object> fields)
        {
            var key = new StringBuilder();
            foreach (var name in Columns)
            {
                if (!fields.TryGetValue(name, out var value)) { key.Append('-'); continue; }
                var text = (string)value;
                key.Append(text.Length).Append(':').Append(text);
            }
            return key.ToString();
        }
        internal CaptureNetworkIdentity(Dictionary<string, object> fields, string key)
        {
            Key = key; Strings = new Dictionary<string, object>();
            foreach (var name in Columns) if (fields.TryGetValue(name, out var value)) Strings.Add(name, value);
            Bytes = EventCodec.Size(new StructuredEvent { Fields = Strings }) + 128 + key.Length * 2;
        }
    }

    internal sealed class CaptureNetwork
    {
        private static readonly string[] Columns = { "SchemaVersion", "RoomEpoch", "Sequence", "MonotonicSeconds",
            "Count", "ActualWindowSeconds", "Rate", "Ping", "RttVariance", "IncomingCommands", "OutgoingCommands",
            "ReliableRetransmissions", "RpcQueueDepth", "QueueDelayMs", "DetailDroppedTotal", "DetailSampledOut",
            "RpcDetailsTruncated",
            "CpuFrameMs", "GpuFrameMs", "PresentWaitMs",
            "RttSampleCount30s", "RttMin30s", "RttMedian30s", "RttP95_30s", "RttMax30s",
            "IncomingCommandsSampleCount30s", "IncomingCommandsMin30s", "IncomingCommandsMedian30s", "IncomingCommandsP95_30s", "IncomingCommandsMax30s",
            "OutgoingCommandsSampleCount30s", "OutgoingCommandsMin30s", "OutgoingCommandsMedian30s", "OutgoingCommandsP95_30s", "OutgoingCommandsMax30s" };
        internal const int PayloadBytes = 560;
        internal readonly CaptureNetworkIdentity Identity;
        private readonly long[] values = new long[Columns.Length];
        private readonly byte[] types = new byte[Columns.Length];
        private readonly ulong present;
        private readonly long frame;
        private readonly string timestamp;
        internal static bool Supports(StructuredEvent e)
        {
            if (e.Type != EventType.NetworkSample || e.Fields == null) return false;
            foreach (var field in e.Fields)
            {
                if (Array.IndexOf(CaptureNetworkIdentity.Columns, field.Key) >= 0)
                { if (!(field.Value is string)) return false; continue; }
                if (Array.IndexOf(Columns, field.Key) < 0) return false;
                if (field.Value == null) continue;
                var code = Type.GetTypeCode(field.Value.GetType());
                if (code < TypeCode.SByte || code > TypeCode.Double || field.Value.GetType().IsEnum) return false;
            }
            return true;
        }
        internal CaptureNetwork(StructuredEvent e, CaptureNetworkIdentity identity)
        {
            Identity = identity; frame = e.FrameNumber; timestamp = e.Timestamp;
            for (int i = 0; i < Columns.Length; i++)
            {
                if (!e.Fields.TryGetValue(Columns[i], out var value)) continue;
                present |= 1UL << i;
                if (value == null) continue;
                var code = Type.GetTypeCode(value.GetType()); types[i] = (byte)code;
                values[i] = code == TypeCode.Double || code == TypeCode.Single ? BitConverter.DoubleToInt64Bits(Convert.ToDouble(value)) :
                    code == TypeCode.UInt64 ? unchecked((long)(ulong)value) : Convert.ToInt64(value);
            }
        }
        private object Value(int i)
        {
            long v = values[i];
            switch ((TypeCode)types[i])
            {
                case TypeCode.Empty: return null;
                case TypeCode.SByte: return (sbyte)v;
                case TypeCode.Byte: return (byte)v;
                case TypeCode.Int16: return (short)v;
                case TypeCode.UInt16: return (ushort)v;
                case TypeCode.Int32: return (int)v;
                case TypeCode.UInt32: return (uint)v;
                case TypeCode.Int64: return v;
                case TypeCode.UInt64: return unchecked((ulong)v);
                case TypeCode.Single: return (float)BitConverter.Int64BitsToDouble(v);
                default: return BitConverter.Int64BitsToDouble(v);
            }
        }
        internal StructuredEvent Expand()
        {
            var fields = new Dictionary<string, object>(Identity.Strings);
            for (int i = 0; i < Columns.Length; i++) if ((present & (1UL << i)) != 0) fields.Add(Columns[i], Value(i));
            return new StructuredEvent { Type = EventType.NetworkSample, FrameNumber = frame, Timestamp = timestamp, Fields = fields };
        }
    }
}
