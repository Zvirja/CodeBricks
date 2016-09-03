using System;
using System.Collections.Generic;

namespace CodeBricks
{
  public static class ThreadStack<TElementType>
  {
    [ThreadStatic] private static Stack<TElementType> _stack;

    private static readonly IDisposable DisposableCleaner = new StackCleaner();

    public static TElementType Current => GetCurrentValue();

    public static TElementType GetCurrentValue(TElementType defValue = default(TElementType))
    {
      if ((_stack == null) || (_stack.Count == 0)) return defValue;

      return _stack.Peek();
    }

    public static void PushValue(TElementType value)
    {
      if (_stack == null) _stack = new Stack<TElementType>();

      _stack.Push(value);
    }

    public static void PopValue(bool assertStackContainValue = true)
    {
      if ((_stack == null) || (_stack.Count == 0))
      {
        if (assertStackContainValue) throw new InvalidOperationException("Thread stack is empty. Unable to pop value.");
        return;
      }

      _stack.Pop();
    }

    public static IDisposable EnterScope(TElementType value)
    {
      PushValue(value);
      return DisposableCleaner;
    }

    private class StackCleaner : IDisposable
    {
      public void Dispose() => PopValue();
    }
  }
}