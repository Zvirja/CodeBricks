using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;

namespace CodeBricks
{
    public class AppendOnlyCollection<T> where T : class
    {
        private readonly Segment _firstSegment;
        private Segment _lastSegment;

        public AppendOnlyCollection(int segmentSize = 32)
        {
            _firstSegment = _lastSegment = new Segment(previousSegment: null, segmentSize);
        }

        public void Add(T value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));

            for (;;)
            {
                if (_lastSegment.TryAdd(value))
                {
                    return;
                }

                // If we are unable to add anymore - it means that segment is full and we need to create a new segment.
                // We use CAS to ensure we don't rewrite pointer which might be modified by a background thread.
                // If CAS is successful, we update previous segment to point to new segment
                Segment last = _lastSegment;
                var newSegment = new Segment(last, last.Size);
                if (Interlocked.CompareExchange(ref _lastSegment, newSegment, last) == last)
                {
                    last.NextSegment = newSegment;
                }
            }
        }

        public DirectedEnumerable Enumerate() => new DirectedEnumerable(this, forwardDirection: true);
        public DirectedEnumerable EnumerateReverse() => new DirectedEnumerable(this, forwardDirection: false);

        private class Segment
        {
            // Notice, due to concurrency the index can overflow the segment size a lot.
            // Just be aware of that and don't forget to coerce the value.
            private int _writeIndex;
            public readonly T[] Values;
            public readonly Segment PreviousSegment;
            public Segment NextSegment;
            public int Size => Values.Length;
            public int Count => Math.Min(_writeIndex + 1, Size);

            public Segment(Segment previousSegment, int segmentSize)
            {
                Values = new T[segmentSize];
                PreviousSegment = previousSegment;
                _writeIndex = -1;
            }

            public bool TryAdd(T value)
            {
                if (_writeIndex >= Size)
                {
                    return false;
                }

                int newIndex = Interlocked.Increment(ref _writeIndex);
                if (newIndex >= Size)
                {
                    return false;
                }

                Values[newIndex] = value;

                return true;
            }
        }

        public struct Enumerator : IEnumerator<T>
        {
            private readonly bool _forward;
            private Segment _currentSegment;
            private int _currentSegmentIndex;

            public Enumerator(AppendOnlyCollection<T> owner, bool forward)
            {
                _forward = forward;
                _currentSegment = forward ? owner._firstSegment : owner._lastSegment;
                _currentSegmentIndex = forward ? -1 : _currentSegment.Count;
            }

            public bool MoveNext()
            {
                return _forward ? MoveForward() : MoveBackward();
            }

            private bool MoveForward()
            {
                // Inspect each segment to the end and move to the next segment.
                // Notice, it might happen that due to concurrency element is not assigned, while
                // the segment index has been already bumped up. To handle that we check whether value by index is not null.
                // If it is - just skip the slot and navigate to the next one.
                // This small trick allows us to solve a problem of reliable track of whether value is already set.

                while (_currentSegment != null)
                {
                    _currentSegmentIndex++;

                    while (_currentSegmentIndex < _currentSegment.Count)
                    {
                        if (Current != null)
                        {
                            return true;
                        }

                        _currentSegmentIndex++;
                    }

                    _currentSegment = _currentSegment.NextSegment;
                    _currentSegmentIndex = -1;
                }

                return false;
            }

            private bool MoveBackward()
            {
                // We inspect each segment while it has elements and return them.
                // After that we switch to the previous segment if it's available.
                // Notice, it might happen that due to concurrency element is not assigned, while
                // the segment index has been already bumped up. To handle that we check whether value by index is not null.
                // If it is - just skip the slot and navigate to the next one.
                // This small trick allows us to solve a problem of reliable track of whether value is already set.

                while (_currentSegment != null)
                {
                    _currentSegmentIndex--;

                    while (_currentSegmentIndex >= 0)
                    {
                        if (Current != null)
                        {
                            return true;
                        }

                        _currentSegmentIndex--;
                    }

                    _currentSegment = _currentSegment.PreviousSegment;
                    _currentSegmentIndex = _currentSegment != null ? _currentSegment.Count : -1;
                }

                return false;
            }

            public void Reset() => throw new NotSupportedException();

            public void Dispose()
            {
            }

            public T Current => _currentSegment.Values[_currentSegmentIndex];

            object IEnumerator.Current => Current;
        }

        public struct DirectedEnumerable : IEnumerable<T>
        {
            private readonly AppendOnlyCollection<T> _owner;
            private readonly bool _forwardDirection;

            public DirectedEnumerable(AppendOnlyCollection<T> owner, bool forwardDirection)
            {
                _owner = owner;
                _forwardDirection = forwardDirection;
            }

            public Enumerator GetEnumerator() => new Enumerator(_owner, _forwardDirection);

            IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }
    }
}
