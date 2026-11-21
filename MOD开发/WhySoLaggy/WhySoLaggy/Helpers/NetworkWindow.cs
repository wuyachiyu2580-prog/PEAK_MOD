using System;

namespace WhySoLaggy
{
    internal readonly struct NetworkWindowStatistics
    {
        internal readonly int Count;
        internal readonly double? Minimum, Median, P95, Maximum;

        internal NetworkWindowStatistics(int count, int minimum, double median, int p95, int maximum)
        {
            Count = count; Minimum = minimum; Median = median; P95 = p95; Maximum = maximum;
        }
    }

    internal readonly struct NetworkWindowSnapshot
    {
        internal readonly NetworkWindowStatistics Rtt, IncomingCommands, OutgoingCommands;
        internal NetworkWindowSnapshot(NetworkWindowStatistics rtt, NetworkWindowStatistics incoming, NetworkWindowStatistics outgoing)
        {
            Rtt = rtt; IncomingCommands = incoming; OutgoingCommands = outgoing;
        }
    }

    internal sealed class NetworkWindow
    {
        private const int Capacity = 31;
        private const double DurationSeconds = 30;
        private readonly Sample[] samples = new Sample[Capacity];
        private readonly int[] rttValues = new int[Capacity];
        private readonly int[] incomingValues = new int[Capacity];
        private readonly int[] outgoingValues = new int[Capacity];
        private int next;
        private int count;

        private struct Sample
        {
            internal double Time;
            internal int? Rtt;
            internal int? Incoming;
            internal int? Outgoing;
        }

        internal void Add(double time, int? rtt, int? incoming, int? outgoing)
        {
            samples[next] = new Sample { Time = time, Rtt = rtt, Incoming = incoming, Outgoing = outgoing };
            next = (next + 1) % Capacity;
            if (count < Capacity) count++;
        }

        internal NetworkWindowSnapshot Snapshot(double now)
        {
            int rttCount = 0, incomingCount = 0, outgoingCount = 0;
            double oldest = now - DurationSeconds;

            for (int i = 0; i < count; i++)
            {
                Sample sample = samples[(next - count + i + Capacity) % Capacity];
                if (sample.Time < oldest || sample.Time > now) continue;
                if (sample.Rtt.HasValue) rttValues[rttCount++] = sample.Rtt.Value;
                if (sample.Incoming.HasValue) incomingValues[incomingCount++] = sample.Incoming.Value;
                if (sample.Outgoing.HasValue) outgoingValues[outgoingCount++] = sample.Outgoing.Value;
            }

            return new NetworkWindowSnapshot(Calculate(rttValues, rttCount), Calculate(incomingValues, incomingCount),
                Calculate(outgoingValues, outgoingCount));
        }

        internal void Clear() { next = count = 0; }

        private static NetworkWindowStatistics Calculate(int[] values, int count)
        {
            if (count == 0) return default;
            Array.Sort(values, 0, count);
            double median = ((long)values[(count - 1) / 2] + values[count / 2]) / 2d;
            return new NetworkWindowStatistics(count, values[0], median, values[(int)Math.Ceiling(count * .95) - 1], values[count - 1]);
        }
    }
}
