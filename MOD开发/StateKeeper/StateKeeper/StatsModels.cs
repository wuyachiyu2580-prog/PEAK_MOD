using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace StateKeeper
{
    public enum RunStatus
    {
        Active,
        Completed,
        Aborted
    }

    public enum RunOutcome
    {
        Unknown,
        Victory,
        Defeat,
        Aborted
    }

    public enum EventSource
    {
        LocalAuthoritative,
        RemoteObserved
    }

    [Serializable]
    public sealed class RunRecord
    {
        public int schemaVersion = 3;
        public string storageFormat = "chunked-json-gzip";
        public RunHeader header = new RunHeader();
        public List<PlayerIdentity> players = new List<PlayerIdentity>();
        public string[] statusTypeOrder = new string[0];
        public int collectionRevision;
        public string[] collectionCapabilities;
        public string[] afflictionTypeOrder;
        public float? distanceUnitsToMeters;
        public List<RunEffectContext> effectContexts = new List<RunEffectContext>();
        public List<RunChunkInfo> chunks = new List<RunChunkInfo>();
        public RunChunkInfo activeChunk;

        [JsonIgnore]
        public List<PlayerSample> samples = new List<PlayerSample>();

        [JsonIgnore]
        public List<InventorySnapshot> inventorySnapshots = new List<InventorySnapshot>();

        [JsonIgnore]
        public List<StatsEvent> events = new List<StatsEvent>();
        public List<ItemDefinition> definitions = new List<ItemDefinition>();
        public List<MountainSegmentDefinition> mountainSegments = new List<MountainSegmentDefinition>();
    }

    [Serializable]
    public sealed class RunChunk
    {
        public int schemaVersion = 3;
        public int sequence;
        public float startTime;
        public float endTime;
        public List<PlayerSample> samples = new List<PlayerSample>();
        public List<InventorySnapshot> inventorySnapshots = new List<InventorySnapshot>();
        public List<StatsEvent> events = new List<StatsEvent>();
    }

    [Serializable]
    public sealed class RunChunkInfo
    {
        public int sequence;
        public string fileName;
        public float startTime;
        public float endTime;
        public int sampleCount;
        public int inventorySnapshotCount;
        public int eventCount;
    }

    [Serializable]
    public sealed class RunHeader
    {
        public string runId;
        public string status = RunStatus.Active.ToString();
        public string outcome = RunOutcome.Unknown.ToString();
        public string startedUtc;
        public string endedUtc;
        public string lastSavedUtc;
        public string gameVersion = "PEAK 2.4.b";
        public string customName;
        public bool hasAscentLevel;
        public int ascentLevel;
        public bool hasCustomRun;
        public bool isCustomRun;
    }

    [Serializable]
    public sealed class PlayerIdentity
    {
        public int playerIndex;
        public string userId;
        public string displayName;
        public int actorNumber;
        public bool isLocal;
        public bool isBot;
    }

    [Serializable]
    public sealed class PlayerSample
    {
        public float time;
        public List<PlayerTelemetry> players = new List<PlayerTelemetry>();
        public int[] distancePlayerPairs = new int[0];
        public float[] distanceMeters = new float[0];
    }

    [Serializable]
    public sealed class PlayerTelemetry
    {
        public int playerIndex;
        public float regularStamina;
        public float extraStamina;
        public float maxStamina;
        public int? petrifyAmount;
        public float passOutValue;
        public bool dead;
        public bool passedOut;
        public bool fullyPassedOut;
        public bool fullyConscious;
        public bool sprinting;
        public bool grounded;
        public bool climbing;
        public bool ropeClimbing;
        public bool vineClimbing;
        public bool gliding;
        public bool parachuteAvailable;
        public bool rocketActive;
        public bool rocketLit;
        public bool struggling;
        public float positionX;
        public float positionY;
        public float positionZ;
        public float[] statuses = new float[0];
        public List<int> activeAfflictionTypes = new List<int>();
    }

    [Serializable]
    public sealed class InventorySnapshot
    {
        public float time;
        public int playerIndex;
        public List<ItemSnapshot> slots = new List<ItemSnapshot>();
    }

    [Serializable]
    public sealed class ItemSnapshot
    {
        public string slot;
        public ushort itemId;
        public string itemName;
        public string prefabName;
        public string guid;
        public bool hasUsesEntry;
        public bool hasUsesValue;
        public int uses;
        public bool hasPetterItemUses;
        public int petterItemUses;
        public bool hasUsed;
        public bool used;
        public bool hasFlareActive;
        public bool flareActive;
        public bool hasPowerEnabled;
        public bool powerEnabled;
        public float useRemaining;
        public bool hasUseRemaining;
        public float fuel;
        public bool hasFuel;
        public int cookedAmount;
        public bool hasCookedAmount;
        public string nestedSignature;
    }

    [Serializable]
    public sealed class StatsEvent
    {
        public float time;
        public string type;
        public EventSource source;
        public int subjectPlayerIndex = -1;
        public int targetPlayerIndex = -1;
        public int? actorPlayerIndex;
        public ushort itemId;
        public string itemName;
        public string itemGuid;
        public string previousItemGuid;
        public string resourceKey;
        public string definitionKey;
        public string slot;
        public string detail;
        public float value;
        public float previousValue;
        public int segmentIndex = -1;
        public string segmentKey;
    }

    [Serializable]
    public sealed class RunIndexFile
    {
        public int schemaVersion = 3;
        public List<RunIndexEntry> entries = new List<RunIndexEntry>();
    }

    [Serializable]
    public sealed class RunIndexEntry
    {
        public int schemaVersion = 3;
        public string runId;
        public string status;
        public string outcome;
        public string startedUtc;
        public string endedUtc;
        public bool favorite;
        public string fileName;
        public string customName;
        public float durationSeconds;
        public int playerCount;
        public int sampleCount;
        public int inventorySnapshotCount;
        public int eventCount;
    }

    [Serializable]
    public sealed class ItemDefinition
    {
        public ushort itemId;
        public string itemName;
        public string prefabName;
        public int totalUses;
        public float usingTimePrimary;
        public List<string> itemTags = new List<string>();
        public List<ItemDefinitionAction> actions = new List<ItemDefinitionAction>();
        public List<ItemDefinitionComponent> components = new List<ItemDefinitionComponent>();
        public List<ItemDefinitionCooking> cookingRules = new List<ItemDefinitionCooking>();
        public List<ItemEffectHint> effectHints = new List<ItemEffectHint>();
    }

    [Serializable]
    public sealed class ItemDefinitionAction
    {
        public string typeName;
        public string trigger;
        public string parameterSummary;
        public List<ItemDefinitionParameter> parameters = new List<ItemDefinitionParameter>();
    }

    [Serializable]
    public sealed class ItemDefinitionComponent
    {
        public string typeName;
        public string parameterSummary;
        public List<ItemDefinitionParameter> parameters = new List<ItemDefinitionParameter>();
    }

    [Serializable]
    public sealed class ItemDefinitionCooking
    {
        public string typeName;
        public string parameterSummary;
        public List<ItemDefinitionParameter> parameters = new List<ItemDefinitionParameter>();
    }

    [Serializable]
    public sealed class ItemDefinitionParameter
    {
        public string name;
        public string type;
        public string value;
        public List<ItemDefinitionParameter> children;
        public bool truncated;
    }

    [Serializable]
    public sealed class RunEffectContext
    {
        public float observedTime;
        public int[] mushroomEffects;
        public int[] mushroomStaminaAmounts;
    }

    [Serializable]
    public sealed class ItemEffectHint
    {
        public string type;
        public string target;
        public bool hasAmount;
        public float amount;
        public string trigger;
    }

    [Serializable]
    public sealed class MountainSegmentDefinition
    {
        public int index;
        public string titleKey;
        public string capturedTitle;
        public string biomeKey;
        public bool hasBoundaryZ;
        public float boundaryZ;
    }
}
