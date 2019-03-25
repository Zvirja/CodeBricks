using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoFixture.Xunit2;
using FluentAssertions;
using Xunit;

namespace CodeBricks.Tests
{
  public class ConcurrentCollectorTests
  {
    private static void PutDataConcurrently<T>(ConcurrentCollector<T> collector, int threads, int numElementsPerThread, Func<int, T> factory)
    {
      var tasks = new Task[threads];

      for (var i = 0; i < threads; i++)
        tasks[i] = Task.Run(() =>
        {
          for (var j = 0; j < numElementsPerThread; j++)
            collector.Add(factory(j));
        });

      Task.WaitAll(tasks);
    }

    [Theory]
    [AutoData]
    public void EnumeratorReturnsSameNumberAsCountIfLessThanCapacity(int elementsPerThread)
    {
      //arrange
      var sut = new ConcurrentCollector<int>(10000);
      PutDataConcurrently(sut, 4, elementsPerThread%2000 + 1, x => x);
      var allElementsCount = sut.Count;

      //act
      var enumerableCount = sut.Count();

      //assert
      enumerableCount.Should().Be(allElementsCount);
    }

    [Theory]
    [AutoData]
    public void EnumeratorReturnsSameNumberAsCountIfMoreThanCapacity(int randomCapacity, int elementsPerThread)
    {
      //arrange
      var capacity = randomCapacity%10 + 1;
      var sut = new ConcurrentCollector<int>(capacity);
      PutDataConcurrently(sut, Environment.ProcessorCount, elementsPerThread%1000 + capacity, x => x);
      var allElementsCount = sut.Count;

      //act
      var enumerableCount = sut.Count();

      //assert
      enumerableCount.Should().Be(allElementsCount);
    }

    [Fact]
    public void CollectorRespectsOrderInSigleThreadedPopulation()
    {
      //arrange
      var entries = Enumerable.Range(150, 1000).ToArray();
      var sut = new ConcurrentCollector<int>(10);

      //act
      PutDataConcurrently(sut, 1, 1000, x => entries[x]);

      //assert
      sut.SequenceEqual(entries).Should().BeTrue();
    }

    [Fact]
    public void CountIsCorrectIfCapacityIsGreaterThanElementsNumber()
    {
      //arrange
      var sut = new ConcurrentCollector<int>(10000);
      PutDataConcurrently(sut, 4, 1000, x => x);

      //act
      var actualCount = sut.Count;

      //assert
      actualCount.Should().Be(4000);
    }

    [Fact]
    public void CountIsCorrectIfCapacityIsLessThanElementsNumber()
    {
      //arrange
      var sut = new ConcurrentCollector<int>(10);
      PutDataConcurrently(sut, 4, 10000, x => x);

      //act
      var actualCount = sut.Count;

      //assert
      actualCount.Should().Be(40000);
    }

    [Fact]
    public void EnumeratorReturnsAllEntries()
    {
      //arrange
      var entries = Enumerable.Range(0, 1000);
      var queue = new ConcurrentQueue<int>(entries);

      var sut = new ConcurrentCollector<int>(100);

      //act
      PutDataConcurrently(sut, 4, 250, x =>
      {
        int res;
        var success = queue.TryDequeue(out res);

        success.Should().BeTrue();
        return res;
      });

      //assert
      sut.Should().BeEquivalentTo(entries);
    }

    [Fact]
    public void WritesDontFailForHugeConcurrency()
    {
      //arrange
      const int ConcurrencyLevel = 1000;
      const int ItemsPerThread = 100000;
      var sut = new ConcurrentCollector<int>(9);

      //act
      Action concurrentPopulation = () =>
      {
        var threads = new Thread[ConcurrencyLevel].ToList();

        for (var i = 0; i < ConcurrencyLevel; i++)
          threads[i] = new Thread(() =>
          {
            for (var j = 0; j < ItemsPerThread; ++j)
              sut.Add(j);
          });

        threads.ForEach(x => x.Start());
        threads.ForEach(x => x.Join());
      };

      //assert
      concurrentPopulation.Should().NotThrow();
      sut.Count.Should().Be(ConcurrencyLevel*ItemsPerThread);
    }
  }
}