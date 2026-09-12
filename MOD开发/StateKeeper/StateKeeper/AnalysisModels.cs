using System;
using System.Collections.Generic;

namespace StateKeeper
{
    [Serializable]
    internal sealed class AnalysisResult
    {
        public const int CurrentVersion = 5;
        public List<LifecycleObservation> lifecycle = new List<LifecycleObservation>();
        public List<LifecycleInterval> lifecycleIntervals = new List<LifecycleInterval>();
        public List<ReportEpisode> episodes = new List<ReportEpisode>();
        public List<QualityObservation> diagnostics = new List<QualityObservation>();
        public List<ReportEvent> timeline = new List<ReportEvent>();
        public int processedInventoryCount;
        public int analysisVersion = CurrentVersion;
        public string[] collectionCapabilities = new string[0];
        public List<AnalysisAssistanceEvent> assistance = new List<AnalysisAssistanceEvent>();
        public List<AnalysisEffectChange> effects = new List<AnalysisEffectChange>();
        public string sourceFingerprint;
        public string[] statusTypeOrder = new string[0];
        public string[] afflictionTypeOrder = new string[0];
        public List<AnalysisDistancePoint> teamSpan = new List<AnalysisDistancePoint>();
        public int schemaVersion;
        public string runId;
        public string sourceLastSavedUtc;
        public int sourceChunkCount;
        public int sourceSampleCount;
        public int sourceInventorySnapshotCount;
        public int sourceEventCount;
        public bool hasAscentLevel;
        public int ascentLevel;
        public bool hasCustomRun;
        public bool isCustomRun;
        public string gameVersion;
        public string recordingUserId;
        public double analysisMilliseconds;
        public long observedManagedBytes;
        public AnalysisOverview overview = new AnalysisOverview();
        public AnalysisQuality quality = new AnalysisQuality();
        public List<AnalysisPlayer> players = new List<AnalysisPlayer>();
        public List<AnalysisDistancePair> distancePairs = new List<AnalysisDistancePair>();
        public List<AnalysisItemObservation> items = new List<AnalysisItemObservation>();
        public List<ItemDefinition> definitions = new List<ItemDefinition>();
        public List<MountainSegmentDefinition> mountainSegments = new List<MountainSegmentDefinition>();
        public List<AnalysisSegment> segments = new List<AnalysisSegment>();
    }

    [Serializable]
    internal sealed class AnalysisOverview
    {
        public string outcome;
        public float durationSeconds;
        public int playerCount;
        public int jumpCount;
        public int deathCount;
        public int passedOutCount;
        public int lifeSavedCount;
        public int rescuePullCount;
        public float friendHealingAmount;
        public int itemObservationCount;
        public int validDistanceCount;
        public int mountainSegmentCount;
    }

    [Serializable]
    internal sealed class AnalysisAssistanceEvent
    {
        public float time;
        public int epoch;
        public string kind;
        public int actorPlayerIndex = -1;
        public int targetPlayerIndex = -1;
        public string itemName;
        public string itemGuid;
        public float amount;
        public string resourceKey;
    }

    [Serializable]
    internal sealed class AnalysisQuality
    {
        public int missingChunkCount;
        public int corruptChunkCount;
        public int timeBackwardsCount;
        public int missingPlayerSampleCount;
        public int invalidPositionCount;
        public int nonFiniteValueCount;
        public int excludedDistanceCount;
        public int gapCount;
        public int countMismatchCount;
        public int outOfRangeStaminaCount;
        public List<string> warnings = new List<string>();
    }

    [Serializable]
    internal sealed class AnalysisPlayer
    {
        public int playerIndex;
        public string displayName;
        public string stableUserId;
        public int rawDeathCount;
        public int duplicateDeathCount;
        public int unconfirmedDeathCount;
        public int rawPassedOutCount;
        public int consumedCount;
        public int possibleHelpCount;
        public float lowStaminaSeconds;
        public float lowCapacitySeconds;
        public float isolatedSeconds;
        public Dictionary<string, float> movementSeconds = new Dictionary<string, float>();
        public Dictionary<string, float> statusIntegrals = new Dictionary<string, float>();
        public int jumpCount;
        public List<float> jumpTimes = new List<float>();
        public List<EvidenceReference> jumpEvidence = new List<EvidenceReference>();
        public int deathCount;
        public int passedOutCount;
        public float staminaMin = 1f;
        public float staminaMax;
        public float staminaAverage;
        public float observedSeconds;
        public float aliveSeconds;
        public List<float> deathTimes = new List<float>();
        public List<float> passedOutTimes = new List<float>();
        public List<AnalysisDistancePoint> nearestSeries = new List<AnalysisDistancePoint>();
        public List<AnalysisSeriesPoint> staminaSeries = new List<AnalysisSeriesPoint>();
        public List<AnalysisStaminaEpisode> staminaEpisodes = new List<AnalysisStaminaEpisode>();
        public List<AnalysisIsolationEpisode> isolationEpisodes = new List<AnalysisIsolationEpisode>();
        public int lifeSavedCount;
        public int rescuePullCount;
        public float friendHealingAmount;
    }

    [Serializable]
    internal sealed class AnalysisSeriesPoint
    {
        public EvidenceReference evidence;
        public float time;
        public float endTime;
        public int epoch;
        public bool breakBefore;
        public bool dead;
        public float maxStamina;
        public int? petrifyAmount;
        public float[] statuses;
        public List<int> afflictions;
        public float minimum;
        public float maximum;
        public float validSeconds;
        public double totalIntegral;
        public float regularStamina;
        public float extraStamina;
        public float regularMinimum, regularMaximum, extraMinimum, extraMaximum;
        public float totalStamina;
        public float normalizedStamina;
        public int sampleCount;
        public float observedSeconds;
        public float[] movementSeconds;
        public float[] statusIntegrals;
    }

    [Serializable]
    internal sealed class AnalysisDistancePair
    {
        public int playerA;
        public int playerB;
        public int sampleCount;
        public float median;
        public float p90;
        public float p99;
        public float max;
        public float nearestObserved;
        public float over25Ratio;
        public float over50Ratio;
        public float over100Ratio;
        public float validSeconds;
        public List<AnalysisDistancePoint> series = new List<AnalysisDistancePoint>();
        // Derived intervals, not raw frames: start, duration, meters. Allows exact range filtering.
        public List<float> intervals = new List<float>();
        public List<int> intervalEpochs = new List<int>();
    }

    [Serializable]
    internal sealed class AnalysisItemObservation
    {
        public EvidenceReference evidence;
        public int observationId;
        public int attributionGroupId;
        public int? cookedAmount;
        public float endTime;
        public List<string> attributionReasons = new List<string>();
        public List<AnalysisEffectRule> rules = new List<AnalysisEffectRule>();
        public bool possibleHelp;
        public bool possibleRescue;
        public float time;
        public int playerIndex = -1;
        public int actorPlayerIndex = -1;
        public bool actorInferred;
        public int targetPlayerIndex = -1;
        public ushort itemId;
        public int epoch;
        public int evidenceCount = 1;
        public string attribution = "Ambiguous";
        public string attributionReason;
        public List<AnalysisEffectChange> observedEffects = new List<AnalysisEffectChange>();
        public string itemGuid;
        public string itemName;
        public string prefabName;
        public string slot;
        public string kind;
        public string resourceKey;
        public float previousValue;
        public float value;
        public string confidence;
        public string detail;
        public List<string> effectHints = new List<string>();
    }

    [Serializable]
    internal sealed class AnalysisSegment
    {
        public int index;
        public string titleKey;
        public string displayName;
        public bool hasStartTime;
        public bool hasEndTime;
        public float startTime;
        public float endTime;
        public bool derivedFromBoundary;
    }

    [Serializable]
    internal sealed class AnalysisStaminaEpisode
    {
        public float startTime;
        public float endTime;
        public float startValue;
        public float endValue;
        public string direction;
        public bool burst;
    }

    [Serializable]
    internal sealed class AnalysisIsolationEpisode
    {
        public EvidenceReference evidence;
        public float startTime;
        public float endTime;
        public float peakNearestDistance;
        public string endReason;
    }

    [Serializable]
    internal sealed class AnalysisDistancePoint
    {
        public EvidenceReference evidence;
        public float time;
        public float endTime;
        public float value;
        public float maximum;
        public float minimum;
        public int count;
        public bool breakBefore;
        public int epoch;
    }

    [Serializable]
    internal sealed class AnalysisEffectChange
    {
        public int effectId;
        public int epoch;
        public string attribution = "Ambiguous";
        public List<string> reasons = new List<string>();
        public List<int> candidateGroupIds = new List<int>();
        public float cumulativeDelta;
        public string channel;
        public int playerIndex;
        public float previousValue;
        public float value;
        public float beforeTime;
        public float afterTime;
        public bool matchesTheory;
    }

    [Serializable]
    internal sealed class AnalysisEffectRule
    {
        public string channel;
        public float? amount;
        public int direction;
        public float delay;
        public float duration;
        public bool continuous;
        public string scope = "Recipient";
        public float? radius;
        public bool ignoreCaster;
        public string trigger;
        public string source;
        public bool uncertain;
        public string requiredAffliction;
        public string afterAfflictionEnds;
        public string budgetGroup;
        public float? budget;
        public int priority;
    }
}
