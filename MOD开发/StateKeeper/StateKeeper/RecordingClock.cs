using System;
using System.Diagnostics;

namespace StateKeeper
{
    internal sealed class RecordingClock
    {
        private readonly Func<double> _seconds;
        private double _origin, _offset;
        internal RecordingClock(Func<double> seconds = null)
        { _seconds = seconds ?? (() => (double)Stopwatch.GetTimestamp() / Stopwatch.Frequency); Reset(0); }
        internal void Reset(double offset) { _origin = _seconds(); _offset = Math.Max(0, offset); }
        internal float Time => (float)(_offset + Math.Max(0, _seconds() - _origin));
    }
}
