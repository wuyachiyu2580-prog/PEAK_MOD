namespace WhySoLaggy
{
    internal enum OwnershipEventKind
    {
        Request,
        Transfer,
        Update,
    }

    internal struct OwnershipEventInfo
    {
        public OwnershipEventKind Kind;
        public int SenderActor;
        public int[] ViewOwnerData;

        public int ViewId => ViewOwnerData != null && ViewOwnerData.Length > 0 ? ViewOwnerData[0] : 0;
        public int RelatedOwner => ViewOwnerData != null && ViewOwnerData.Length > 1 ? ViewOwnerData[1] : 0;
        public int PairCount => ViewOwnerData == null ? 0 : ViewOwnerData.Length / 2;
    }

    internal static class OwnershipEventParser
    {
        public const byte RequestCode = 209;
        public const byte TransferCode = 210;
        public const byte UpdateCode = 212;

        public static bool IsSupported(byte code)
        {
            return code == RequestCode || code == TransferCode || code == UpdateCode;
        }

        public static bool TryParse(byte code, object payload, int senderActor, out OwnershipEventInfo info)
        {
            info = default(OwnershipEventInfo);
            if (!IsSupported(code) || !(payload is int[] values)) return false;

            bool valid = code == UpdateCode
                ? values.Length >= 2 && values.Length % 2 == 0
                : values.Length == 2;
            if (!valid) return false;

            for (int i = 0; i < values.Length; i += 2)
            {
                if (values[i] <= 0 || values[i + 1] < 0) return false;
            }

            info = new OwnershipEventInfo
            {
                Kind = code == RequestCode ? OwnershipEventKind.Request
                    : code == TransferCode ? OwnershipEventKind.Transfer
                    : OwnershipEventKind.Update,
                SenderActor = senderActor,
                ViewOwnerData = values,
            };
            return true;
        }

        public static string BuildPairSummary(int[] values)
        {
            if (values == null || values.Length == 0) return "";
            var parts = new string[values.Length / 2];
            for (int i = 0; i < values.Length; i += 2)
                parts[i / 2] = values[i] + "->" + values[i + 1];
            return string.Join(";", parts);
        }
    }
}
