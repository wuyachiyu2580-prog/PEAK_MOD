using System;
using System.Collections.Generic;

namespace WhySoLaggy
{
    internal sealed class BoundedConcurrentQueue<T>
    {
        private readonly Queue<T> _items = new Queue<T>();
        private readonly object _lock = new object();
        private readonly Queue<int> _sizes = new Queue<int>();
        private readonly Func<T,int> _size;
        private readonly long _byteCapacity;
        private long _bytes;
        public long Bytes { get { lock(_lock)return _bytes; } }
        private int _capacity;
        private int _peakCount;
        private long _droppedInWindow;

        public BoundedConcurrentQueue(int capacity, Func<T,int> size = null, long byteCapacity = long.MaxValue)
        {
            _size=size; _byteCapacity=byteCapacity; Capacity = capacity;
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
                        _items.Dequeue(); _bytes-=_sizes.Dequeue();
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
                int size=Math.Max(0,_size?.Invoke(item)??0);
                if(size>_byteCapacity){_droppedInWindow++;return new QueueEnqueueResult(_items.Count,true,1);}
                _items.Enqueue(item);_sizes.Enqueue(size);_bytes+=size;
                bool dropped = false;int droppedCount=0;
                while (_items.Count > _capacity || _bytes>_byteCapacity)
                {
                    _items.Dequeue(); _bytes-=_sizes.Dequeue();
                    _droppedInWindow++;
                    dropped = true; droppedCount++;
                }
                if (_items.Count > _peakCount) _peakCount = _items.Count;
                return new QueueEnqueueResult(_items.Count, dropped,droppedCount);
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
                item = _items.Dequeue();_bytes-=_sizes.Dequeue();
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
                _items.Clear();_sizes.Clear();_bytes=0;
                _peakCount = 0;
                _droppedInWindow = 0;
            }
        }
    }

    internal struct QueueEnqueueResult
    {
        public QueueEnqueueResult(int depth, bool dropped,int droppedCount=0)
        {
            Depth = depth;
            Dropped = dropped; DroppedCount=droppedCount;
        }

        public int DroppedCount { get; }
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
