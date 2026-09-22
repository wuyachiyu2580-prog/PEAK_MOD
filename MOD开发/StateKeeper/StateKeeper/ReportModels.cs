using System;
using System.Collections.Generic;

namespace StateKeeper
{
    [Serializable]
    internal sealed class EvidenceReference
    {
        public string runId;
        public int epoch;
        public int chunk;
        public string stream;
        public int index;
    }

    [Serializable]
    internal sealed class LifecycleInterval
    {
        public EvidenceReference evidence;
        public int playerIndex;
        public float start, end;
        public string state;
        public bool entered;
        public int sampleCount;
    }

    [Serializable]
    internal sealed class LifecycleObservation
    {
        public EvidenceReference evidence;
        public int playerIndex;
        public float time;
        public string kind;
        public string verdict = "Unconfirmed";
        public int duplicateCount;
        public string reason;
    }

    [Serializable]
    internal sealed class QualityObservation
    {
        public EvidenceReference evidence;
        public int playerIndex = -1;
        public float time;
        public float endTime;
        public string reason;
        public float x, y, z;
        public float? distance;
        public int count = 1;
    }

    [Serializable]
    internal sealed class ReportEpisode
    {
        public EvidenceReference evidence;
        public List<EvidenceReference> supportingEvidence;
        public List<int> supportingPlayers;
        public int playerIndex = -1;
        public int otherPlayerIndex = -1;
        public string kind;
        public string channel;
        public float start, end, activeSeconds;
        public float peak;
    }

    [Serializable]
    internal sealed class ReportEvent
    {
        public EvidenceReference evidence;
        public List<EvidenceReference> supportingEvidence;
        public List<int> supportingPlayers;
        public int playerIndex = -1;
        public int otherPlayerIndex = -1;
        public float time, endTime;
        public string kind;
        public string confidence;
        public int itemObservationId;
        public string detail;
    }

    internal static class ReportThresholds
    {
        internal const int CertainUseScore = 80;
        internal const int LikelyUseScore = 55;
        internal const int PossibleUseScore = 30;
        internal const float DeathWindow = 2f;
        internal const float SentinelRadius = 2f;
        internal const float LowRegularRatio = .1f;
        internal const float LowExtra = .05f;
        internal const float LowStaminaDuration = 3f;
        internal const float LowCapacity = .25f;
        internal const float LowCapacityDuration = 10f;
        internal const float JoinGap = 2f;
        internal const float StatusWindow = 5f;
        internal const float StatusRise = .2f;
    }
}
