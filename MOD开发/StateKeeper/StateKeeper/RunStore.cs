using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;

namespace StateKeeper
{
    internal sealed class RunStore
    {
        internal const int CurrentSchemaVersion = 3;
        private const int MaxRecentRuns = 10;
        private const int SamplesPerChunk = 150;
        private static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
        {
            Culture = System.Globalization.CultureInfo.InvariantCulture,
            NullValueHandling = NullValueHandling.Ignore
        };

        private readonly object _sync = new object();
        private readonly object _errorSync = new object();
        private readonly string _root;
        private readonly string _runs;
        private readonly string _favorites;
        private readonly string _activePath;
        private readonly string _indexPath;
        private RunIndexFile _index;
        private Task _writeTail = Task.CompletedTask;
        private Exception _backgroundWriteError;
        private long _checkpointGeneration;
        private long _latestCheckpointGeneration;

        public RunStore()
            : this(Path.Combine(Application.persistentDataPath, "StateKeeper"))
        {
        }

        internal RunStore(string rootPath)
        {
            _root = rootPath;
            _runs = Path.Combine(_root, "Runs");
            _favorites = Path.Combine(_root, "Favorites");
            _activePath = Path.Combine(_root, "active-run.json");
            _indexPath = Path.Combine(_root, "index.json");
            Directory.CreateDirectory(_runs);
            Directory.CreateDirectory(_favorites);
            _index = LoadIndex();
        }

        public string RootPath { get { return _root; } }

        internal string FindManifestPath(string runId)
        {
            if (string.IsNullOrEmpty(runId)) return null;
            lock (_sync)
            {
                RunIndexEntry entry = GetEntriesLocked().FirstOrDefault(e => string.Equals(e.runId, runId, StringComparison.OrdinalIgnoreCase));
                if (entry == null || entry.schemaVersion != CurrentSchemaVersion) return null;
                string directory = entry.favorite ? _favorites : _runs;
                string path = SafeChildPath(directory, entry.fileName);
                return File.Exists(path) ? path : null;
            }
        }

        public RunRecord LoadActive()
        {
            lock (_sync)
            {
                if (!File.Exists(_activePath)) return null;
                try
                {
                    RunRecord result = JsonConvert.DeserializeObject<RunRecord>(File.ReadAllText(_activePath), JsonSettings);
                    if (result == null || result.header == null || string.IsNullOrEmpty(result.header.runId))
                        throw new InvalidDataException("Active run has no valid header or RunId.");
                    // Schema 2 is intentionally left in place for manual cleanup. It is not resumed or rewritten.
                    if (result.schemaVersion != CurrentSchemaVersion)
                        return null;

                    Normalize(result);
                    if (result.activeChunk != null)
                    {
                        string chunkPath = SafeChildPath(_runs, result.activeChunk.fileName);
                        RunChunk chunk = ReadCompressedChunk(chunkPath);
                        if (chunk.sequence != result.activeChunk.sequence)
                            throw new InvalidDataException("Active chunk sequence does not match its manifest.");
                        if (chunk.schemaVersion != CurrentSchemaVersion) return null;
                        result.samples = chunk.samples ?? new List<PlayerSample>();
                        result.inventorySnapshots = chunk.inventorySnapshots ?? new List<InventorySnapshot>();
                        result.events = chunk.events ?? new List<StatsEvent>();
                    }
                    return result;
                }
                catch (Exception ex)
                {
                    QuarantineCorrupt(_activePath, ex);
                    return null;
                }
            }
        }

        public void SaveActive(RunRecord record)
        {
            if (record == null) return;
            lock (_sync)
            {
                ThrowBackgroundWriteError();
                long checkpointGeneration = Interlocked.Increment(ref _checkpointGeneration);
                Volatile.Write(ref _latestCheckpointGeneration, checkpointGeneration);
                record.header.lastSavedUtc = DateTime.UtcNow.ToString("o");
                ChunkWrite chunkWrite = PrepareChunk(record, false);
                RunRecord manifest = CloneManifest(record);
                EnqueueWrite(delegate
                {
                    bool isLatest = checkpointGeneration == Volatile.Read(ref _latestCheckpointGeneration);
                    if (!isLatest && (chunkWrite == null || !chunkWrite.isSealed)) return;
                    if (chunkWrite != null) WriteCompressedAtomic(chunkWrite.path, chunkWrite.chunk);
                    if (isLatest) WriteAtomic(_activePath, manifest);
                });
            }
        }

        public void FlushPendingWrites()
        {
            lock (_sync)
            {
                _writeTail.GetAwaiter().GetResult();
                ThrowBackgroundWriteError();
            }
        }

        public void Complete(RunRecord record, RunStatus status, RunOutcome outcome)
        {
            if (record == null || record.header == null || string.IsNullOrEmpty(record.header.runId)) return;
            lock (_sync)
            {
                ThrowBackgroundWriteError();
                record.header.status = status.ToString();
                record.header.outcome = outcome.ToString();
                record.header.endedUtc = DateTime.UtcNow.ToString("o");
                record.header.lastSavedUtc = record.header.endedUtc;
                record.header.customName = CleanCustomName(record.header.customName);
                int sampleCount = CountSamples(record);
                int inventorySnapshotCount = CountInventorySnapshots(record);
                int eventCount = CountEvents(record);
                float durationSeconds = FindTotalEndTime(record);
                ChunkWrite chunkWrite = PrepareChunk(record, true);
                string fileName = record.header.runId + ".json";
                RunRecord manifest = CloneManifest(record);
                _index.entries.RemoveAll(e => e != null && string.Equals(e.runId, record.header.runId, StringComparison.OrdinalIgnoreCase));
                _index.entries.Add(new RunIndexEntry
                {
                    runId = record.header.runId,
                    status = record.header.status,
                    outcome = record.header.outcome,
                    startedUtc = record.header.startedUtc,
                    endedUtc = record.header.endedUtc,
                    favorite = false,
                    fileName = fileName,
                    customName = CleanCustomName(record.header.customName),
                    durationSeconds = durationSeconds,
                    playerCount = record.players == null ? 0 : record.players.Count,
                    sampleCount = sampleCount,
                    inventorySnapshotCount = inventorySnapshotCount,
                    eventCount = eventCount
                });
                CleanupRecent();
                RunIndexFile indexSnapshot = CloneIndex(_index);
                EnqueueWrite(delegate
                {
                    if (chunkWrite != null) WriteCompressedAtomic(chunkWrite.path, chunkWrite.chunk);
                    WriteAtomic(Path.Combine(_runs, fileName), manifest);
                    TryDelete(_activePath);
                    WriteAtomic(_indexPath, indexSnapshot);
                });
            }
        }

        internal IReadOnlyList<RunIndexEntry> GetRecentEntries()
        {
            lock (_sync) return GetEntriesLocked().Where(e => !e.favorite).OrderByDescending(e => ParseDate(e.endedUtc, e.startedUtc)).Select(CloneIndexEntry).ToList();
        }

        internal IReadOnlyList<RunIndexEntry> GetFavoriteEntries()
        {
            lock (_sync) return GetEntriesLocked().Where(e => e.favorite).OrderByDescending(e => ParseDate(e.endedUtc, e.startedUtc)).Select(CloneIndexEntry).ToList();
        }

        internal bool ToggleFavorite(string runId)
        {
            if (string.IsNullOrEmpty(runId)) return false;
            lock (_sync)
            {
                _writeTail.GetAwaiter().GetResult();
                ThrowBackgroundWriteError();
                RunIndexEntry entry = GetEntriesLocked().FirstOrDefault(e => string.Equals(e.runId, runId, StringComparison.OrdinalIgnoreCase));
                if (entry == null || entry.status == RunStatus.Active.ToString()) return false;
                string sourceDir = entry.favorite ? _favorites : _runs;
                string targetDir = entry.favorite ? _runs : _favorites;
                string manifestPath = SafeChildPath(sourceDir, entry.fileName);
                if (!File.Exists(manifestPath)) return false;
                MoveRunFiles(sourceDir, targetDir, entry.fileName);
                entry.favorite = !entry.favorite;
                CleanupRecent();
                SaveIndex();
                return true;
            }
        }

        internal bool RenameRun(string runId, string customName)
        {
            if (string.IsNullOrEmpty(runId)) return false;
            lock (_sync)
            {
                _writeTail.GetAwaiter().GetResult();
                ThrowBackgroundWriteError();
                RunIndexEntry entry = GetEntriesLocked().FirstOrDefault(e => string.Equals(e.runId, runId, StringComparison.OrdinalIgnoreCase));
                if (entry == null || entry.status == RunStatus.Active.ToString()) return false;
                string directory = entry.favorite ? _favorites : _runs;
                string manifestPath = SafeChildPath(directory, entry.fileName);
                if (!File.Exists(manifestPath)) return false;
                RunRecord manifest = JsonConvert.DeserializeObject<RunRecord>(File.ReadAllText(manifestPath), JsonSettings);
                if (manifest == null || manifest.schemaVersion != CurrentSchemaVersion || manifest.header == null) return false;
                string cleaned = CleanCustomName(customName);
                manifest.header.customName = cleaned;
                WriteAtomic(manifestPath, manifest);
                entry.customName = cleaned;
                SaveIndex();
                return true;
            }
        }

        internal bool DeleteRun(string runId)
        {
            if (string.IsNullOrEmpty(runId)) return false;
            lock (_sync)
            {
                _writeTail.GetAwaiter().GetResult();
                ThrowBackgroundWriteError();
                RunIndexEntry entry = GetEntriesLocked().FirstOrDefault(e => string.Equals(e.runId, runId, StringComparison.OrdinalIgnoreCase));
                if (entry == null || entry.status == RunStatus.Active.ToString()) return false;
                string directory = entry.favorite ? _favorites : _runs;
                DeleteRunFiles(directory, entry.fileName);
                TryDelete(Path.Combine(_root, "Analysis", entry.runId + ".analysis.json.gz"));
                _index.entries.Remove(entry);
                SaveIndex();
                return true;
            }
        }

        private ChunkWrite PrepareChunk(RunRecord record, bool forceSeal)
        {
            if (record.samples.Count == 0 && record.inventorySnapshots.Count == 0 && record.events.Count == 0) return null;

            int sequence = record.activeChunk == null ? record.chunks.Count : record.activeChunk.sequence;
            string fileName = record.activeChunk == null
                ? record.header.runId + ".chunk-" + sequence.ToString("D5") + ".json.gz"
                : record.activeChunk.fileName;
            var chunk = new RunChunk
            {
                schemaVersion = CurrentSchemaVersion,
                sequence = sequence,
                startTime = FindStartTime(record),
                endTime = FindEndTime(record),
                samples = new List<PlayerSample>(record.samples),
                inventorySnapshots = new List<InventorySnapshot>(record.inventorySnapshots),
                events = new List<StatsEvent>(record.events)
            };
            string path = SafeChildPath(_runs, fileName);

            var info = new RunChunkInfo
            {
                sequence = sequence,
                fileName = fileName,
                startTime = chunk.startTime,
                endTime = chunk.endTime,
                sampleCount = chunk.samples.Count,
                inventorySnapshotCount = chunk.inventorySnapshots.Count,
                eventCount = chunk.events.Count
            };

            if (forceSeal || record.samples.Count >= SamplesPerChunk)
            {
                record.chunks.RemoveAll(c => c != null && c.sequence == sequence);
                record.chunks.Add(info);
                record.chunks.Sort((left, right) => left.sequence.CompareTo(right.sequence));
                record.activeChunk = null;
                record.samples = new List<PlayerSample>();
                record.inventorySnapshots = new List<InventorySnapshot>();
                record.events = new List<StatsEvent>();
            }
            else
            {
                record.activeChunk = info;
            }
            return new ChunkWrite
            {
                path = path,
                chunk = chunk,
                isSealed = forceSeal || record.activeChunk == null
            };
        }

        private static float FindStartTime(RunRecord record)
        {
            float result = float.MaxValue;
            if (record.samples.Count > 0) result = Math.Min(result, record.samples[0].time);
            if (record.inventorySnapshots.Count > 0) result = Math.Min(result, record.inventorySnapshots[0].time);
            if (record.events.Count > 0) result = Math.Min(result, record.events[0].time);
            return result == float.MaxValue ? 0f : result;
        }

        private static float FindEndTime(RunRecord record)
        {
            float result = 0f;
            if (record.samples.Count > 0) result = Math.Max(result, record.samples[record.samples.Count - 1].time);
            if (record.inventorySnapshots.Count > 0) result = Math.Max(result, record.inventorySnapshots[record.inventorySnapshots.Count - 1].time);
            if (record.events.Count > 0) result = Math.Max(result, record.events[record.events.Count - 1].time);
            return result;
        }

        private void EnqueueWrite(Action operation)
        {
            _writeTail = _writeTail.ContinueWith(delegate
            {
                try { operation(); }
                catch (Exception ex)
                {
                    lock (_errorSync) _backgroundWriteError = ex;
                }
            }, CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default);
        }

        private void ThrowBackgroundWriteError()
        {
            Exception error;
            lock (_errorSync)
            {
                error = _backgroundWriteError;
                _backgroundWriteError = null;
            }
            if (error != null) throw new IOException("Background run-data write failed.", error);
        }

        private static RunRecord CloneManifest(RunRecord source)
        {
            var result = new RunRecord
            {
                schemaVersion = source.schemaVersion,
                storageFormat = source.storageFormat,
                collectionRevision = source.collectionRevision,
                collectionCapabilities = source.collectionCapabilities == null ? null : (string[])source.collectionCapabilities.Clone(),
                afflictionTypeOrder = source.afflictionTypeOrder == null ? null : (string[])source.afflictionTypeOrder.Clone(),
                distanceUnitsToMeters = source.distanceUnitsToMeters,
                effectContexts = source.effectContexts == null ? new List<RunEffectContext>() : new List<RunEffectContext>(source.effectContexts),
                header = new RunHeader
                {
                    runId = source.header.runId,
                    status = source.header.status,
                    outcome = source.header.outcome,
                    startedUtc = source.header.startedUtc,
                    endedUtc = source.header.endedUtc,
                    lastSavedUtc = source.header.lastSavedUtc,
                    gameVersion = source.header.gameVersion,
                    customName = source.header.customName,
                    hasAscentLevel = source.header.hasAscentLevel,
                    ascentLevel = source.header.ascentLevel,
                    hasCustomRun = source.header.hasCustomRun,
                    isCustomRun = source.header.isCustomRun
                },
                statusTypeOrder = source.statusTypeOrder == null ? new string[0] : (string[])source.statusTypeOrder.Clone(),
                activeChunk = CloneChunkInfo(source.activeChunk)
            };
            result.players.Clear();
            foreach (PlayerIdentity player in source.players)
            {
                result.players.Add(new PlayerIdentity
                {
                    playerIndex = player.playerIndex,
                    userId = player.userId,
                    displayName = player.displayName,
                    actorNumber = player.actorNumber,
                    isLocal = player.isLocal,
                    isBot = player.isBot
                });
            }
            result.chunks.Clear();
            foreach (RunChunkInfo chunk in source.chunks) result.chunks.Add(CloneChunkInfo(chunk));
            if (source.definitions != null)
            {
                foreach (ItemDefinition definition in source.definitions)
                {
                    if (definition == null) continue;
                    result.definitions.Add(new ItemDefinition
                    {
                        itemId = definition.itemId,
                        itemName = definition.itemName,
                        prefabName = definition.prefabName,
                        totalUses = definition.totalUses,
                        usingTimePrimary = definition.usingTimePrimary,
                        itemTags = definition.itemTags == null ? new List<string>() : new List<string>(definition.itemTags),
                        actions = definition.actions == null ? new List<ItemDefinitionAction>() : definition.actions,
                        components = definition.components == null ? new List<ItemDefinitionComponent>() : definition.components,
                        cookingRules = definition.cookingRules == null ? new List<ItemDefinitionCooking>() : definition.cookingRules,
                        effectHints = definition.effectHints == null ? new List<ItemEffectHint>() : definition.effectHints
                    });
                }
            }
            if (source.mountainSegments != null)
            {
                foreach (MountainSegmentDefinition segment in source.mountainSegments)
                {
                    if (segment == null) continue;
                    result.mountainSegments.Add(new MountainSegmentDefinition
                    {
                        index = segment.index,
                        titleKey = segment.titleKey,
                        capturedTitle = segment.capturedTitle,
                        biomeKey = segment.biomeKey,
                        hasBoundaryZ = segment.hasBoundaryZ,
                        boundaryZ = segment.boundaryZ
                    });
                }
            }
            return result;
        }

        private static RunChunkInfo CloneChunkInfo(RunChunkInfo source)
        {
            if (source == null) return null;
            return new RunChunkInfo
            {
                sequence = source.sequence,
                fileName = source.fileName,
                startTime = source.startTime,
                endTime = source.endTime,
                sampleCount = source.sampleCount,
                inventorySnapshotCount = source.inventorySnapshotCount,
                eventCount = source.eventCount
            };
        }

        private static RunIndexFile CloneIndex(RunIndexFile source)
        {
            var result = new RunIndexFile { schemaVersion = source.schemaVersion };
            if (source.entries == null) return result;
            foreach (RunIndexEntry entry in source.entries)
            {
                if (entry == null) continue;
                result.entries.Add(new RunIndexEntry
                {
                    schemaVersion = entry.schemaVersion,
                    runId = entry.runId,
                    status = entry.status,
                    outcome = entry.outcome,
                    startedUtc = entry.startedUtc,
                    endedUtc = entry.endedUtc,
                    favorite = entry.favorite,
                    fileName = entry.fileName,
                    customName = entry.customName,
                    durationSeconds = entry.durationSeconds,
                    playerCount = entry.playerCount,
                    sampleCount = entry.sampleCount,
                    inventorySnapshotCount = entry.inventorySnapshotCount,
                    eventCount = entry.eventCount
                });
            }
            return result;
        }

        private RunIndexFile LoadIndex()
        {
            try
            {
                if (!File.Exists(_indexPath)) return new RunIndexFile();
                RunIndexFile result = JsonConvert.DeserializeObject<RunIndexFile>(File.ReadAllText(_indexPath), JsonSettings);
                if (result == null || result.schemaVersion != CurrentSchemaVersion) return new RunIndexFile();
                if (result.entries == null) result.entries = new List<RunIndexEntry>();
                result.entries.RemoveAll(e => e == null || e.schemaVersion != CurrentSchemaVersion);
                return result;
            }
            catch
            {
                return new RunIndexFile();
            }
        }

        private List<RunIndexEntry> GetEntriesLocked()
        {
            return _index.entries.Where(e => e != null && e.schemaVersion == CurrentSchemaVersion && !string.IsNullOrEmpty(e.runId)).ToList();
        }

        private static RunIndexEntry CloneIndexEntry(RunIndexEntry entry)
        {
            return new RunIndexEntry
            {
                schemaVersion = entry.schemaVersion,
                runId = entry.runId,
                status = entry.status,
                outcome = entry.outcome,
                startedUtc = entry.startedUtc,
                endedUtc = entry.endedUtc,
                favorite = entry.favorite,
                fileName = entry.fileName,
                customName = entry.customName,
                durationSeconds = entry.durationSeconds,
                playerCount = entry.playerCount,
                sampleCount = entry.sampleCount,
                inventorySnapshotCount = entry.inventorySnapshotCount,
                eventCount = entry.eventCount
            };
        }

        private static string CleanCustomName(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            var builder = new StringBuilder(Math.Min(40, value.Length));
            foreach (char character in value)
            {
                if (char.IsControl(character)) continue;
                if (builder.Length >= 40) break;
                builder.Append(character);
            }
            return builder.ToString().Trim();
        }

        private static int CountSamples(RunRecord record)
        {
            return (record.chunks == null ? 0 : record.chunks.Sum(c => c == null ? 0 : c.sampleCount)) + (record.samples == null ? 0 : record.samples.Count);
        }

        private static int CountInventorySnapshots(RunRecord record)
        {
            return (record.chunks == null ? 0 : record.chunks.Sum(c => c == null ? 0 : c.inventorySnapshotCount)) + (record.inventorySnapshots == null ? 0 : record.inventorySnapshots.Count);
        }

        private static int CountEvents(RunRecord record)
        {
            return (record.chunks == null ? 0 : record.chunks.Sum(c => c == null ? 0 : c.eventCount)) + (record.events == null ? 0 : record.events.Count);
        }

        private static float FindTotalEndTime(RunRecord record)
        {
            float result = 0f;
            if (record.chunks != null)
                foreach (RunChunkInfo chunk in record.chunks)
                    if (chunk != null) result = Math.Max(result, chunk.endTime);
            return Math.Max(result, FindEndTime(record));
        }

        private void CleanupRecent()
        {
            List<RunIndexEntry> recent = _index.entries
                .Where(e => e != null && !e.favorite && e.status != RunStatus.Active.ToString())
                .OrderByDescending(e => ParseDate(e.endedUtc, e.startedUtc))
                .ToList();
            foreach (RunIndexEntry old in recent.Skip(MaxRecentRuns).ToList())
            {
                DeleteRunFiles(_runs, old.fileName);
                _index.entries.Remove(old);
            }
        }

        private void MoveRunFiles(string sourceDir, string targetDir, string manifestName)
        {
            List<string> files = GetRunFiles(sourceDir, manifestName);
            foreach (string fileName in files.Where(f => !string.Equals(f, manifestName, StringComparison.OrdinalIgnoreCase)))
                MoveReplacing(SafeChildPath(sourceDir, fileName), SafeChildPath(targetDir, fileName));
            MoveReplacing(SafeChildPath(sourceDir, manifestName), SafeChildPath(targetDir, manifestName));
        }

        private void DeleteRunFiles(string directory, string manifestName)
        {
            foreach (string fileName in GetRunFiles(directory, manifestName))
                TryDelete(SafeChildPath(directory, fileName));
        }

        private static List<string> GetRunFiles(string directory, string manifestName)
        {
            var files = new List<string>();
            string safeManifest = Path.GetFileName(manifestName);
            string manifestPath = SafeChildPath(directory, safeManifest);
            if (File.Exists(manifestPath))
            {
                try
                {
                    RunRecord record = JsonConvert.DeserializeObject<RunRecord>(File.ReadAllText(manifestPath), JsonSettings);
                    if (record != null && record.chunks != null)
                        foreach (RunChunkInfo chunk in record.chunks)
                            if (chunk != null && !string.IsNullOrEmpty(chunk.fileName)) files.Add(Path.GetFileName(chunk.fileName));
                }
                catch { }
            }
            files.Add(safeManifest);
            return files.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        private void SaveIndex()
        {
            WriteAtomic(_indexPath, _index);
        }

        private static DateTime ParseDate(string primary, string fallback)
        {
            DateTime parsed;
            if (DateTime.TryParse(primary, null, System.Globalization.DateTimeStyles.RoundtripKind, out parsed)) return parsed;
            if (DateTime.TryParse(fallback, null, System.Globalization.DateTimeStyles.RoundtripKind, out parsed)) return parsed;
            return DateTime.MinValue;
        }

        private static void Normalize(RunRecord record)
        {
            if (record.players == null) record.players = new List<PlayerIdentity>();
            for (int i = 0; i < record.players.Count; i++) record.players[i].playerIndex = i;
            if (record.statusTypeOrder == null) record.statusTypeOrder = new string[0];
            if (record.chunks == null) record.chunks = new List<RunChunkInfo>();
            if (record.samples == null) record.samples = new List<PlayerSample>();
            if (record.inventorySnapshots == null) record.inventorySnapshots = new List<InventorySnapshot>();
            if (record.events == null) record.events = new List<StatsEvent>();
        }

        private static RunChunk ReadCompressedChunk(string path)
        {
            using (var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var gzip = new GZipStream(file, CompressionMode.Decompress))
            using (var reader = new StreamReader(gzip, Encoding.UTF8))
            using (var jsonReader = new JsonTextReader(reader))
            {
                RunChunk result = JsonSerializer.Create(JsonSettings).Deserialize<RunChunk>(jsonReader);
                if (result == null) throw new InvalidDataException("Chunk is empty.");
                return result;
            }
        }

        private static void WriteCompressedAtomic<T>(string path, T value)
        {
            string temp = path + ".tmp-" + Guid.NewGuid().ToString("N");
            try
            {
                using (var file = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, FileOptions.SequentialScan))
                using (var gzip = new GZipStream(file, System.IO.Compression.CompressionLevel.Optimal))
                using (var writer = new StreamWriter(gzip, new UTF8Encoding(false), 65536))
                using (var jsonWriter = new JsonTextWriter(writer))
                    JsonSerializer.Create(JsonSettings).Serialize(jsonWriter, value);
                ReplaceAtomic(temp, path);
            }
            catch
            {
                TryDelete(temp);
                throw;
            }
        }

        private static void WriteAtomic<T>(string path, T value)
        {
            string temp = path + ".tmp-" + Guid.NewGuid().ToString("N");
            try
            {
                string json = JsonConvert.SerializeObject(value, Formatting.None, JsonSettings);
                File.WriteAllText(temp, json, new UTF8Encoding(false));
                ReplaceAtomic(temp, path);
            }
            catch
            {
                TryDelete(temp);
                throw;
            }
        }

        private static void ReplaceAtomic(string temp, string path)
        {
            if (File.Exists(path)) File.Replace(temp, path, null);
            else File.Move(temp, path);
        }

        private static void MoveReplacing(string source, string target)
        {
            if (!File.Exists(source)) return;
            TryDelete(target);
            File.Move(source, target);
        }

        private static string SafeChildPath(string directory, string fileName)
        {
            return Path.Combine(directory, Path.GetFileName(fileName));
        }

        private static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { }
        }

        private static void QuarantineCorrupt(string path, Exception ex)
        {
            try
            {
                string quarantine = path + ".corrupt-" + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");
                if (File.Exists(path)) File.Move(path, quarantine);
                Debug.LogWarning("[StateKeeper] Ignored corrupt JSON: " + path + " (" + ex.Message + ")");
            }
            catch (Exception moveEx)
            {
                Debug.LogWarning("[StateKeeper] Could not quarantine corrupt JSON: " + moveEx.Message);
            }
        }

        private sealed class ChunkWrite
        {
            public string path;
            public RunChunk chunk;
            public bool isSealed;
        }
    }
}
