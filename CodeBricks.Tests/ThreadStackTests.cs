using System;
using System.Threading;
using AutoFixture.Xunit2;
using FluentAssertions;
using Xunit;

namespace CodeBricks.Tests
{
  public class ThreadStackTests
  {
    [Theory]
    [AutoData]
    public void PushedElementCanBeRead(object element)
    {
      try
      {
        //act
        ThreadStack<object>.PushValue(element);

        //assert
        ThreadStack<object>.Current.Should().Be(element);
      }
      finally
      {
        ThreadStack<object>.PopValue();
      }
    }

    [Theory]
    [AutoData]
    public void LastPushedElementIsReturned(object element1, object element2)
    {
      try
      {
        //arrange
        ThreadStack<object>.PushValue(element1);

        //act
        ThreadStack<object>.PushValue(element2);

        //assert
        ThreadStack<object>.Current.Should().Be(element2);
      }
      finally
      {
        ThreadStack<object>.PopValue();
        ThreadStack<object>.PopValue();
      }
    }

    [Theory]
    [AutoData]
    public void StackDistinguishTypes(object objElement, string strElement)
    {
      try
      {
        //act
        ThreadStack<object>.PushValue(objElement);
        ThreadStack<string>.PushValue(strElement);

        //assert
        ThreadStack<string>.Current.Should().Be(strElement);
      }
      finally
      {
        ThreadStack<object>.PopValue();
        ThreadStack<string>.PopValue();
      }
    }

    [Theory]
    [AutoData]
    public void PopWorksCorrectly(object element)
    {
      //arrange
      ThreadStack<object>.PushValue(element);

      //act
      ThreadStack<object>.PopValue();

      //assert
      ThreadStack<object>.Current.Should().BeNull();
    }

    [Theory]
    [AutoData]
    public void ScopeWorksCorrectly(object element)
    {
      //act & assert
      using (ThreadStack<object>.EnterScope(element))
      {
        ThreadStack<object>.Current.Should().Be(element);
      }

      ThreadStack<object>.Current.Should().BeNull();
    }

    [Theory]
    [AutoData]
    public void StackIsThreadBound(object elementThr1, object elementThr2)
    {
      try
      {
        //arrange
        ThreadStack<object>.PushValue(elementThr1);

        var otherThreadInvoked = false;
        object otherThreadCaptured = null;

        var thread = new Thread(() =>
        {
          otherThreadInvoked = true;
          otherThreadCaptured = ThreadStack<object>.Current;

          ThreadStack<object>.PushValue(elementThr2);
        });

        //act
        thread.Start();
        thread.Join();

        //assert
        otherThreadInvoked.Should().BeTrue();
        otherThreadCaptured.Should().BeNull();
        ThreadStack<object>.Current.Should().Be(elementThr1);
      }
      finally
      {
        ThreadStack<object>.PopValue();
      }
    }

    [Fact]
    public void CurrentDoesntFailIfStackIsNull()
    {
      //act
      Action getter = () => ThreadStack<object>.GetCurrentValue();

      //assert
      getter.Should().NotThrow();
    }

    [Fact]
    public void PopDoesntThrowIfNotAsserted()
    {
      //act
      Action pop = () => ThreadStack<object>.PopValue(false);

      //assert
      pop.Should().NotThrow();
    }

    [Fact]
    public void PopThrowsIfAsserted()
    {
      //act
      Action pop = () => ThreadStack<object>.PopValue();

      //assert
      pop.Should().Throw<InvalidOperationException>();
    }
  }
}