using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace PeakWorkshopCNScanner
{
    [DataContract]
    internal sealed class ScanRecord
    {
        [DataMember] public int Id;
        [DataMember] public string Kind;
        [DataMember] public string Text;
        [DataMember] public string Scope;
        [DataMember] public string Evidence;
        [DataMember] public string DrawStack = "Not captured (budget limited); scope is still known.";
        [DataMember] public string FirstSeenUtc;
        [DataMember] public string LastSeenUtc;
        [DataMember] public int SampledFrames;
        [DataMember] public string Scene;
        [DataMember] public string ChinesePatchState;
        [DataMember] public float X, Y, Width, Height;
        [DataMember] public string Coordinates = "IMGUI local draw rect (before GUI.matrix and clip). Last sampled position.";
        internal int LastFrame = -1;
        internal bool HasStack;
        internal ScanRecord Copy() => (ScanRecord)MemberwiseClone();
    }

    internal struct TextKey : IEquatable<TextKey>
    {
        internal string Scope, Text;
        internal TextKey(string scope, string text) { Scope = scope; Text = text; }
        public bool Equals(TextKey other) => Scope == other.Scope && Text == other.Text;
        public override bool Equals(object obj) => obj is TextKey other && Equals(other);
        public override int GetHashCode() => unchecked(Scope.GetHashCode() * 397 ^ Text.GetHashCode());
    }

    internal sealed class ScanStore
    {
        internal const int MaxTextLength = 16384;
        private readonly Dictionary<TextKey, ScanRecord> _cache = new Dictionary<TextKey, ScanRecord>();
        private readonly List<ScanRecord> _records = new List<ScanRecord>();
        private readonly TextClassifier _classifier;
        private readonly int _maxRecords, _maxCache;
        private int _cacheCharacters;
        internal int Count => _records.Count;
        internal int CacheCount => _cache.Count;
        internal long Revision { get; private set; }
        internal long Classifications { get; private set; }
        internal bool LimitReached { get; private set; }
        internal bool OversizeTextSeen { get; private set; }
        internal int AnalysisBudget;

        internal ScanStore(TextClassifier classifier, int maxRecords, int maxCache)
        {
            _classifier = classifier;
            _maxRecords = maxRecords;
            _maxCache = maxCache;
        }

        internal ScanRecord Observe(string scope, string text, bool analyze)
        {
            if (string.IsNullOrEmpty(text)) return null;
            if (text.Length > MaxTextLength)
            {
                if (!OversizeTextSeen) { OversizeTextSeen = true; Revision++; }
                return null;
            }
            var key = new TextKey(scope, text);
            if (_cache.TryGetValue(key, out var cached)) return cached;
            if (!analyze || AnalysisBudget <= 0) return null;
            if (_records.Count >= _maxRecords || _cache.Count >= _maxCache || _cacheCharacters + text.Length > 2000000)
            {
                if (!LimitReached) { LimitReached = true; Revision++; }
                return null;
            }
            AnalysisBudget--;
            Classifications++;
            var finding = _classifier.Classify(text);
            ScanRecord record = null;
            if (finding != null)
            {
                record = new ScanRecord { Id = _records.Count + 1, Kind = finding.Kind, Evidence = finding.Evidence, Text = text, Scope = scope };
                _records.Add(record);
            }
            _cache.Add(key, record);
            _cacheCharacters += text.Length;
            Revision++;
            return record;
        }

        internal void Touch(ScanRecord record, int frame, string utc, string scene, string chinese,
            float x, float y, float width, float height)
        {
            if (record.LastFrame == frame) return;
            record.LastFrame = frame;
            record.FirstSeenUtc = record.FirstSeenUtc ?? utc;
            record.LastSeenUtc = utc;
            record.SampledFrames++;
            record.Scene = scene;
            record.ChinesePatchState = chinese;
            record.X = x; record.Y = y; record.Width = width; record.Height = height;
            Revision++;
        }

        internal ScanRecord[] Snapshot()
        {
            var result = new ScanRecord[_records.Count];
            for (int i = 0; i < result.Length; i++) result[i] = _records[i].Copy();
            return result;
        }
    }
}
