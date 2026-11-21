using System;
using System.Collections.Generic;

namespace WhySoLaggy
{
    internal enum CaptureCategory { Frame, Anchor, Protected, Network, Detail }
    internal enum CaptureLossReason { Capacity, Oversize, SharedBudget }

    internal struct CaptureLossRange
    {
        internal long Lower, Upper;
        internal bool Unknown;
        internal string Quality => Unknown ? "Unknown" : Lower == Upper ? "Exact" : "Bounded";
    }

    // Snapshots own their counters. Subsequent rolling evictions cannot change a capture.
    internal sealed class CaptureLossSnapshot
    {
        internal readonly double Start, End;
        internal readonly CaptureLossRange[] Ranges;
        internal CaptureLossSnapshot(double start, double end, CaptureLossRange[] ranges)
        { Start = start; End = end; Ranges = ranges; }
        internal CaptureLossRange Total
        {
            get
            {
                var result = new CaptureLossRange();
                foreach (var range in Ranges) { result.Lower += range.Lower; result.Upper += range.Upper; result.Unknown |= range.Unknown; }
                return result;
            }
        }
        internal object[] Rows(string phase)
        {
            var rows = new object[CaptureLossLedger.Kinds];
            for (int i = 0; i < rows.Length; i++)
            {
                var r = Ranges[i];
                rows[i] = new Dictionary<string, object> { {"Phase", phase}, {"Category", ((CaptureCategory)(i / 3)).ToString()},
                    {"Reason", ((CaptureLossReason)(i % 3)).ToString()}, {"LowerBound", r.Lower},
                    {"UpperBound", r.Unknown ? (object)null : r.Upper}, {"Quality", r.Quality} };
            }
            return rows;
        }
        internal static CaptureLossSnapshot Unknown(double start, double end)
        {
            var ranges = new CaptureLossRange[CaptureLossLedger.Kinds];
            for (int i = 0; i < ranges.Length; i++) ranges[i].Unknown = true;
            return new CaptureLossSnapshot(start, end, ranges);
        }
    }

    internal sealed class CaptureLossLedger
    {
        internal const int Kinds = 15, Seconds = 128;
        internal const int PayloadBytes = Kinds * Seconds * 32 + 256;
        private struct Bucket { internal long Second, Count; internal double First, Last; }
        private readonly Bucket[] buckets = new Bucket[Kinds * Seconds];
        private readonly long[] totals = new long[Kinds];
        private double forgottenThrough = double.NegativeInfinity;
        private bool invalidTime;

        internal void Add(double time, CaptureCategory category, CaptureLossReason reason)
        {
            int kind = (int)category * 3 + (int)reason;
            totals[kind]++;
            if (double.IsNaN(time) || double.IsInfinity(time) || time < 0 || time > long.MaxValue / 2d) { invalidTime = true; return; }
            long second = (long)Math.Floor(time);
            int index = kind * Seconds + (int)(second % Seconds);
            var b = buckets[index];
            if (b.Count != 0 && b.Second != second)
            {
                if (b.Second > second) { forgottenThrough = Math.Max(forgottenThrough, time); return; }
                forgottenThrough = Math.Max(forgottenThrough, b.Last);
                b = default;
            }
            if (b.Count == 0) { b.Second = second; b.First = b.Last = time; }
            else { b.First = Math.Min(b.First, time); b.Last = Math.Max(b.Last, time); }
            b.Count++; buckets[index] = b;
        }

        internal CaptureLossSnapshot Snapshot(double start, double end)
        {
            var ranges = new CaptureLossRange[Kinds];
            bool unknown = invalidTime || start <= forgottenThrough || end < start;
            for (int k = 0; k < Kinds; k++)
            {
                ranges[k].Unknown = unknown;
                for (int i = 0; i < Seconds; i++)
                {
                    var b = buckets[k * Seconds + i];
                    if (b.Count == 0 || b.Last < start || b.First > end) continue;
                    ranges[k].Upper += b.Count;
                    if (b.First >= start && b.Last <= end) ranges[k].Lower += b.Count;
                    else
                    {
                        // The observed endpoints are real events, even for a partial bucket.
                        if (b.First >= start && b.First <= end) ranges[k].Lower++;
                        else ranges[k].Upper--;
                        if (b.Last != b.First && b.Last >= start && b.Last <= end) ranges[k].Lower++;
                        else if (b.Last != b.First) ranges[k].Upper--;
                    }
                }
            }
            return new CaptureLossSnapshot(start, end, ranges);
        }

        internal CaptureLossSnapshot Lifetime(double start, double end)
        {
            var ranges = new CaptureLossRange[Kinds];
            for (int i = 0; i < Kinds; i++) ranges[i].Lower = ranges[i].Upper = totals[i];
            return new CaptureLossSnapshot(start, end, ranges);
        }
    }
}
