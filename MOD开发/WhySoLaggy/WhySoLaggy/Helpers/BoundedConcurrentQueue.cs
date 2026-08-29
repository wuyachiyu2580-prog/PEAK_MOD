using System;
using System.Collections.Generic;

namespace WhySoLaggy
{
    internal sealed class BoundedConcurrentQueue<T>
    {
        private readonly Queue<T> _items = new Queue<T>();
        private readonly object _lock = new object();
        private int _capacity;
        private int _peakCount;
        private long _droppedInWindow;

        public BoundedConcurrentQueue(int capacity)
        {
            Capacity = capacity;
        }

        public int Capacity
        {
            get { lock (_lock) return _capacity; }
            set
            {
                if (value < 1) throw new ArgumentOutOfRangeException(nameof(value));
                lock (_lock)
                {
                    _capacity = value;
                    while (_items.Count > _capacity)
                    {
                        _items.Dequeue();
                        _droppedInWindow++;
                    }
                    if (_items.Count > _peakCount) _peakCount = _items.Count;
                }
            }
        }

        public int Count
        {
            get { lock (_lock) return _items.Count; }
        }

        public QueueEnqueueResult Enqueue(T item)
        {
            lock (_lock)
            {
                _items.Enqueue(item);
                bool dropped = false;
                if (_items.Count > _capacity)
                {
                    _items.Dequeue();
                    _droppedInWindow++;
                    dropped = true;
                }
                if (_items.Count > _peakCount) _peakCount = _items.Count;
                return new QueueEnqueueResult(_items.Count, dropped);
            }
        }

        public bool TryDequeue(out T item)
        {
            lock (_lock)
            {
                if (_items.Count == 0)
                {
                    item = default(T);
                    return false;
                }
                item = _items.Dequeue();
                return true;
            }
        }

        public QueueWindowStats TakeWindowStats()
        {
            lock (_lock)
            {
                var result = new QueueWindowStats(_capacity, _items.Count, _peakCount, _droppedInWindow);
                _peakCount = _items.Count;
                _droppedInWindow = 0;
                return result;
            }
        }

        public void Clear()
        {
            lock (_lock)
            {
                _items.Clear();
                _peakCount = 0;
                _droppedInWindow = 0;
            }
        }
    }

    internal struct QueueEnqueueResult
    {
        public QueueEnqueueResult(int depth, bool dropped)
        {
            Depth = depth;
            Dropped = dropped;
        }

        public int Depth { get; }
        public bool Dropped { get; }
    }

    internal struct QueueWindowStats
    {
        public QueueWindowStats(int capacity, int depth, int peak, long dropped)
        {
            Capacity = capacity;
            Depth = depth;
            Peak = peak;
            Dropped = dropped;
        }

        public int Capacity { get; }
        public int Depth { get; }
        public int Peak { get; }
        public long Dropped { get; }
    }
}
