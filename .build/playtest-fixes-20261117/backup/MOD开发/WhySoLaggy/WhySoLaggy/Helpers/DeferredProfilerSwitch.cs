using System;
using System.Threading;

namespace WhySoLaggy
{
    // Config events can run on a file-watcher thread. Only Drain invokes the main-thread action.
    internal sealed class DeferredProfilerSwitch
    {
        private int _pending;
        private volatile bool _cancelled;
        private bool _hasApplied, _last;

        internal void Request(bool enabled)
        {
            if (!_cancelled) Interlocked.Exchange(ref _pending, enabled ? 2 : 1);
        }

        internal void Drain(bool ready, Action<bool> apply)
        {
            if (_cancelled || !ready) return;
            int request = Interlocked.Exchange(ref _pending, 0);
            if (request == 0) return;
            bool desired = request == 2;
            if (_hasApplied && desired == _last) return;
            apply(desired);
            _last = desired;
            _hasApplied = true;
        }

        internal void Cancel()
        {
            _cancelled = true;
            Interlocked.Exchange(ref _pending, 0);
        }
    }
}
