using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace StateKeeper
{
    internal sealed class RunAnalysisService
    {
        private readonly RunStore _store;
        private readonly object _sync = new object();
        private CancellationTokenSource _cancellation;
        private Task _task;
        private AnalysisResult _result;
        private Exception _error;
        private string _runId, _status = "Idle";
        private int _currentChunk, _totalChunks, _generation;
        internal RunAnalysisService(RunStore store) { _store = store; }
        internal bool IsRunning { get { lock (_sync) return _task != null && !_task.IsCompleted; } }
        internal string RunId { get { lock (_sync) return _runId; } }
        internal string Status { get { lock (_sync) return _status; } }
        internal int CurrentChunk { get { lock (_sync) return _currentChunk; } }
        internal int TotalChunks { get { lock (_sync) return _totalChunks; } }
        internal AnalysisResult Result { get { lock (_sync) return _result; } }
        internal Exception Error { get { lock (_sync) return _error; } }

        internal void Start(string runId, bool force = false)
        {
            if (string.IsNullOrEmpty(runId)) return;
            lock (_sync)
            {
                _cancellation?.Cancel();
                var cancellation = new CancellationTokenSource();
                _cancellation = cancellation;
                int generation = ++_generation;
                _runId = runId; _result = null; _error = null;
                _currentChunk = 0; _totalChunks = 0; _status = "LoadingManifest";
                Task previous = _task;
                _task = Task.Factory.StartNew(() =>
                {
                    try
                    {
                        // A replacement request waits for cancellation cleanup, avoiding parallel full-run analyses.
                        if (previous != null) try { previous.GetAwaiter().GetResult(); } catch { }
                        AnalysisResult value = BuildOrLoad(runId, force, cancellation.Token, generation);
                        lock (_sync) if (generation == _generation) { _result = value; _status = "Completed"; }
                    }
                    catch (OperationCanceledException) { lock (_sync) if (generation == _generation) _status = "Cancelled"; }
                    catch (Exception ex) { lock (_sync) if (generation == _generation) { _error = ex; _status = "Failed"; } }
                    finally { lock (_sync) if (ReferenceEquals(_cancellation, cancellation)) _cancellation = null; cancellation.Dispose(); }
                }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            }
        }

        internal void Cancel() { lock (_sync) _cancellation?.Cancel(); }
        internal void ReleaseResult() { lock (_sync) _result = null; }

        private AnalysisResult BuildOrLoad(string id, bool force, CancellationToken token, int generation)
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            long observedMemory = GC.GetTotalMemory(false);
            token.ThrowIfCancellationRequested();
            string manifestPath = _store.FindManifestPath(id);
            if (manifestPath == null) throw new FileNotFoundException("Run manifest not found", id);
            RunRecord manifest = JsonConvert.DeserializeObject<RunRecord>(File.ReadAllText(manifestPath));
            if (manifest?.header == null || manifest.schemaVersion != RunStore.CurrentSchemaVersion)
                throw new InvalidDataException("Unsupported recording schema");
            manifest.chunks = manifest.chunks ?? new List<RunChunkInfo>();
            string folder = Path.GetDirectoryName(manifestPath);
            string signature = Fingerprint(manifest, folder, token);
            string cachePath = Path.Combine(_store.RootPath, "Analysis", id + ".analysis.json.gz");
            AnalysisResult cached = force ? null : ReadGzip<AnalysisResult>(cachePath, true);
            token.ThrowIfCancellationRequested();
            if (!force && cached != null && cached.analysisVersion == AnalysisResult.CurrentVersion && cached.sourceFingerprint == signature && cached.runId == id) return cached;
            cached = null;
            var engine = new RunAnalysisEngine(manifest);
            int number = 0, previousSequence = -1;
            foreach (RunChunkInfo info in manifest.chunks)
            {
                token.ThrowIfCancellationRequested();
                lock (_sync) if (generation == _generation) { _currentChunk = ++number; _totalChunks = manifest.chunks.Count; _status = "ReadingChunk"; }
                string path = ChunkPath(folder, info);
                if (path == null || !File.Exists(path)) { engine.Result.quality.missingChunkCount++; engine.Break("MissingChunk"); continue; }
                try
                {
                    RunChunk chunk = ReadGzip<RunChunk>(path, false);
                    if (chunk == null || chunk.schemaVersion != manifest.schemaVersion || chunk.sequence != info.sequence) throw new InvalidDataException("Chunk metadata mismatch");
                    if (info.sequence != previousSequence + 1) { engine.Result.quality.countMismatchCount++; engine.Break("SequenceGap"); }
                    previousSequence = info.sequence;
                    if ((chunk.samples?.Count ?? 0) != info.sampleCount || (chunk.inventorySnapshots?.Count ?? 0) != info.inventorySnapshotCount || (chunk.events?.Count ?? 0) != info.eventCount)
                    { engine.Result.quality.countMismatchCount++; engine.Break("CountMismatch"); }
                    engine.AddChunk(chunk, token);
                    observedMemory = Math.Max(observedMemory, GC.GetTotalMemory(false));
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                {
                    engine.Result.quality.corruptChunkCount++;
                    engine.Result.quality.warnings.Add("Chunk " + info.sequence + ": " + ex.GetType().Name);
                    engine.Break("CorruptChunk");
                }
            }
            AnalysisResult result = engine.Finish(token);
            result.sourceFingerprint = signature;
            result.analysisMilliseconds = watch.Elapsed.TotalMilliseconds;
            result.observedManagedBytes = Math.Max(observedMemory, GC.GetTotalMemory(false));
            token.ThrowIfCancellationRequested();
            WriteCache(cachePath, result, token);
            return result;
        }

        private static string ChunkPath(string folder, RunChunkInfo info)
        {
            if (string.IsNullOrEmpty(info?.fileName) || Path.GetFileName(info.fileName) != info.fileName) return null;
            return Path.Combine(folder, info.fileName);
        }

        private static string Fingerprint(RunRecord manifest, string folder, CancellationToken token)
        {
            using (SHA256 hash = SHA256.Create())
            {
                byte[] metadata = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new
                {
                    manifest.schemaVersion, manifest.collectionRevision, manifest.collectionCapabilities,
                    manifest.statusTypeOrder, manifest.afflictionTypeOrder, manifest.distanceUnitsToMeters,
                    manifest.definitions, manifest.mountainSegments, manifest.effectContexts, manifest.players,
                    manifest.header.outcome, manifest.header.gameVersion, manifest.header.hasAscentLevel,
                    manifest.header.ascentLevel, manifest.header.hasCustomRun, manifest.header.isCustomRun,
                    manifest.chunks
                }));
                hash.TransformBlock(metadata, 0, metadata.Length, metadata, 0);
                byte[] buffer = new byte[65536];
                foreach (RunChunkInfo info in manifest.chunks)
                {
                    token.ThrowIfCancellationRequested();
                    string path = ChunkPath(folder, info);
                    if (path == null || !File.Exists(path))
                    {
                        byte[] missing = Encoding.UTF8.GetBytes("missing:" + info?.fileName);
                        hash.TransformBlock(missing, 0, missing.Length, missing, 0);
                        continue;
                    }
                    using (FileStream stream = File.OpenRead(path))
                    {
                        int read;
                        while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
                        { token.ThrowIfCancellationRequested(); hash.TransformBlock(buffer, 0, read, buffer, 0); }
                    }
                }
                hash.TransformFinalBlock(new byte[0], 0, 0);
                return Convert.ToBase64String(hash.Hash);
            }
        }

        private static T ReadGzip<T>(string path, bool optional) where T : class
        {
            try
            {
                using (FileStream file = File.OpenRead(path))
                using (GZipStream gzip = new GZipStream(file, CompressionMode.Decompress))
                using (StreamReader reader = new StreamReader(gzip, Encoding.UTF8))
                using (JsonTextReader json = new JsonTextReader(reader))
                    return new JsonSerializer().Deserialize<T>(json);
            }
            catch { if (optional) return null; throw; }
        }

        private static void WriteCache(string path, AnalysisResult result, CancellationToken token)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temp = path + ".tmp-" + Guid.NewGuid().ToString("N");
            try
            {
                using (FileStream file = new FileStream(temp, FileMode.CreateNew, FileAccess.Write))
                using (GZipStream gzip = new GZipStream(file, CompressionLevel.Optimal))
                using (StreamWriter writer = new StreamWriter(gzip, new UTF8Encoding(false)))
                using (JsonTextWriter json = new JsonTextWriter(writer)) new JsonSerializer().Serialize(json, result);
                token.ThrowIfCancellationRequested();
                if (File.Exists(path)) File.Replace(temp, path, null); else File.Move(temp, path);
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }
    }
}
