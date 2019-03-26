using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace CodeBricks.Tests.Utils
{
    public static class ConcurrencyUtil
    {
        public static void ExecuteConcurrently(int concurrencyLevel, Action action)
        {
            var threads = new Thread[concurrencyLevel];

            for (var i = 0; i < concurrencyLevel; i++)
                threads[i] = new Thread(() => action()) {IsBackground = true};

            foreach (var thread in threads)
            {
                thread.Start();
            }

            foreach (var thread in threads)
            {
                thread.Join();
            }
        }

        public static void ExecuteConcurrently(int concurrencyLevel, int iterationsPerThread, Action action)
        {
            ExecuteConcurrently(concurrencyLevel, () =>
            {
                for (int i = 0; i < iterationsPerThread; i++)
                {
                    action();
                }
            });
        }

        public static void AddConcurrently<T>(int concurrencyLevel, IEnumerable<T> elements, Action<T> adder)
        {
            var queue = new ConcurrentQueue<T>(elements);
            ExecuteConcurrently(concurrencyLevel, () =>
            {
                while (queue.TryDequeue(out var val))
                {
                    adder(val);
                }
            });
        }
    }
}
