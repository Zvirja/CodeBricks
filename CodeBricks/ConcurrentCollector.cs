using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace CodeBricks
{
    /// <summary>
    ///   Collection that implements Multiple Producers - No Consumers scenario.
    ///   <para>
    ///     Common usage is to collect data in parallel and later pass it to consumer.
    ///     Current collection supports multiple writers, however reads should be performed only after all the writers finished
    ///     their work.
    ///     Reads could be parallel as well.
    ///   </para>
    /// </summary>
    public class ConcurrentCollector<T> : IEnumerable<T>
    {
        private readonly int _chunkCapacity;

        private readonly object _syncRoot = new object();

        public ConcurrentCollector(int chunkCapacity)
        {
            if (chunkCapacity <= 0)
                throw new ArgumentOutOfRangeException(nameof(chunkCapacity));

            _chunkCapacity = chunkCapacity;
            Chunks = new List<Chunk<T>>();

            AddNewChunkUnsafe();
        }

        private List<Chunk<T>> Chunks { get; }

        private Chunk<T> CurrentChunk { get; set; }

        /// <summary>
        ///   Returns number of elements in collector. Note, is not valid during collection phase, should only be used afterwards.
        /// </summary>
        public int Count
        {
            get
            {
                //All chunks except the last one are full. Chunks count is always greater than zero. The last chunk is CurrentChunk
                var fullChunksCount = _chunkCapacity * (Chunks.Count - 1);
                var lastChunkCount = CurrentChunk.ActualLength;

                return fullChunksCount + lastChunkCount;
            }
        }

        public IEnumerator<T> GetEnumerator()
        {
            // We just merge all the values returned by chunks
            return Chunks.SelectMany(x => x).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();


        /// <summary>
        ///   Adds element to collector. Is thread safe.
        /// </summary>
        public void Add(T value)
        {
            while (!CurrentChunk.TryAdd(value))
                EnsureCurrentChunkNotFullSync();
        }

        private void EnsureCurrentChunkNotFullSync()
        {
            lock (_syncRoot)
            {
                if (CurrentChunk.IsFull)
                    AddNewChunkUnsafe();
            }
        }

        private void AddNewChunkUnsafe()
        {
            var newChunk = new Chunk<T>(_chunkCapacity);
            Chunks.Add(newChunk);
            CurrentChunk = newChunk;
        }

        private class Chunk<TElement> : IEnumerable<TElement>
        {
            private readonly int _capacity;
            private readonly TElement[] _data;

            private volatile int _lastElementIndex;

            public Chunk(int capacity)
            {
                _data = new TElement[capacity];
                _capacity = capacity;
                _lastElementIndex = -1;
            }

            /// <summary>
            ///   Checks whether
            /// </summary>
            public bool IsFull => _lastElementIndex >= _capacity - 1;

            /// <summary>
            ///   Returns actual capaticy. Note, it ins't expected to return valid value during the population phase.
            /// </summary>
            public int ActualLength => _lastElementIndex >= _capacity ? _capacity : _lastElementIndex + 1;

            public IEnumerator<TElement> GetEnumerator()
            {
                if (IsFull)
                    return _data.AsEnumerable().GetEnumerator();

                // If chunk not empty - return inserted values only
                return _data.Take(_lastElementIndex + 1).GetEnumerator();
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }

            public bool TryAdd(TElement element)
            {
                // Ensure that chunk is not already full
                if (IsFull)
                    return false;

                var newIndex = Interlocked.Increment(ref _lastElementIndex);

                // Check whether new index is still in range. If not - current chunk is full
                if (newIndex >= _capacity)
                {
                    // Useless a bit, however idea is to ensure that this._index is not overflowed very much in case of huge concurrency
                    _lastElementIndex = _capacity;
                    return false;
                }

                _data[newIndex] = element;
                return true;
            }
        }
    }
}