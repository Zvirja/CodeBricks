using System.Linq;
using CodeBricks.Tests.Utils;
using FluentAssertions;
using Xunit;

namespace CodeBricks.Tests
{
    public class AppendOnlyCollectionTests
    {
        [Fact]
        public void ShouldRespectForwardOrderWhenInsertedWithoutConcurrency()
        {
            var sut = new AppendOnlyCollection<object>();
            var objectsToInsert = Enumerable.Repeat(0, 150).Select(i => new IdentityObject(i)).ToArray();

            ConcurrencyUtil.AddConcurrently(1, objectsToInsert, o => sut.Add(o));

            sut.Enumerate().Should().Equal(objectsToInsert);
        }

        [Fact]
        public void ShouldRespectReverseOrderWhenInsertedWithoutConcurrency()
        {
            var sut = new AppendOnlyCollection<object>();
            var objectsToInsert = Enumerable.Repeat(0, 150).Select(i => new IdentityObject(i)).ToArray();

            ConcurrencyUtil.AddConcurrently(1, objectsToInsert, o => sut.Add(o));

            sut.EnumerateReverse().Should().Equal(objectsToInsert.Reverse().ToArray());
        }

        [Fact]
        public void ShouldReturnAllValuesWhenInsertedConcurrently()
        {
            var sut = new AppendOnlyCollection<object>();
            var objectToInsert = new object();
            const int concurrencyLevel = 250;
            const int iterationsPerThread = 4_000_00;

            ConcurrencyUtil.ExecuteConcurrently(concurrencyLevel, iterationsPerThread, () => sut.Add(objectToInsert));

            var expectedCount = concurrencyLevel * iterationsPerThread;
            sut.Enumerate().Should().HaveCount(expectedCount);
            sut.EnumerateReverse().Should().HaveCount(expectedCount);
        }
    }
}
