using System;
using System.Collections.Generic;

namespace WhySoLaggy
{
    internal struct AlertCooldownDecision
    {
        public bool Emit;
        public int SuppressedCount;
        public float PeakValue;
    }

    internal sealed class AlertCooldownTracker
    {
        private struct State
        {
            public float LastEmission;
            public int SuppressedCount;
            public float PeakValue;
            public bool HasEmission;
        }

        private readonly Dictionary<string, State> _states =
            new Dictionary<string, State>(StringComparer.Ordinal);

        public AlertCooldownDecision Observe(string key, float now, float value, float cooldownSeconds)
        {
            if (string.IsNullOrEmpty(key)) throw new ArgumentException("Alert key is required.", nameof(key));
            if (cooldownSeconds < 0f) cooldownSeconds = 0f;

            State state;
            if (!_states.TryGetValue(key, out state))
                state = new State { PeakValue = value };

            if (state.HasEmission && now - state.LastEmission < cooldownSeconds)
            {
                state.SuppressedCount++;
                if (value > state.PeakValue) state.PeakValue = value;
                _states[key] = state;
                return new AlertCooldownDecision
                {
                    Emit = false,
                    SuppressedCount = state.SuppressedCount,
                    PeakValue = state.PeakValue,
                };
            }

            float peak = state.HasEmission ? Math.Max(state.PeakValue, value) : value;
            int suppressed = state.HasEmission ? state.SuppressedCount : 0;
            _states[key] = new State
            {
                LastEmission = now,
                SuppressedCount = 0,
                PeakValue = 0f,
                HasEmission = true,
            };
            return new AlertCooldownDecision
            {
                Emit = true,
                SuppressedCount = suppressed,
                PeakValue = peak,
            };
        }

        public void Clear()
        {
            _states.Clear();
        }
    }
}
