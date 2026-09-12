using System.Diagnostics;
using System.Globalization;

namespace StateKeeper
{
    internal sealed class PerformanceWindow
    {
        private long _count;
        private double _total, _maximum;
        internal void Record(long start)
        {
            double ms = (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency;
            _count++; _total += ms; if (ms > _maximum) _maximum = ms;
        }
        internal string Take(string name)
        {
            string value = string.Format(CultureInfo.InvariantCulture, "{0}: calls={1}, mean={2:0.000}ms, max={3:0.000}ms", name, _count, _count > 0 ? _total / _count : 0, _maximum);
            _count = 0; _total = _maximum = 0; return value;
        }
    }
}
