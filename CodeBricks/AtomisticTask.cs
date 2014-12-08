﻿#region

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

#endregion

namespace CodeBricks
{
  /// <summary>
  /// Represents a long task that consists of a huge amount of sub tasks.
  /// Each sub task could be processed independently, by different threads.
  /// Sub tasks could be added dynamically, during any sub task processing.
  /// </summary>
  public class AtomisticTask<TSubTaskInfo>
  {
    private volatile bool m_finished;

    private int m_numberOfFreeThreads;

    private volatile int m_numberOfThreads;

    private long m_processedStamp;

    private ConcurrentQueue<TSubTaskInfo> m_subTasks = new ConcurrentQueue<TSubTaskInfo>();

    /// <summary>
    /// Each thread that wants to participate in tasks execution should call this gate method.
    /// Thread will return from the method only after all the tasks are done!
    /// </summary>
    public void ParticipateInTask(Action<TSubTaskInfo> subTaskExecutor)
    {
      //To consider that we have one more working thread
      Interlocked.Increment(ref this.m_numberOfThreads);

      try
      {
        do
        {
          //While we have tasks, we should execute them.
          TSubTaskInfo taskInfo;
          while (this.m_subTasks.TryDequeue(out taskInfo))
          {
            //wake up awaiting threads
            this.UpdateProcessedStamp();

            subTaskExecutor(taskInfo);
          }

          /* If all the tasks are done, we are waiting for more tasks.
           * We exit only if all the threads finished their tasks.
           * Otherwise we use stamp to identify that more tasks are present in queue and start to participate.
           */
        }
        while (this.WaitForTasks());
      }
        /* When we exit, we restore number of threads.
         * That is important, so no other threads will rely on this thread.
         */
      finally
      {
        Interlocked.Decrement(ref this.m_numberOfThreads);

        //If there are awaiting threads relying on current thread's results, they might stuck forever.
        //We should wake up all the awaiting threads if we are exiting.
        this.UpdateProcessedStamp();
      }
    }

    /// <summary>
    /// Add new sub task to the pool.
    /// That is a legal operation only if task is not finished.
    /// </summary>
    /// <param name="taskInfo"></param>
    public void AddSubTask(TSubTaskInfo taskInfo)
    {
      if (!this.m_finished)
      {
        throw new InvalidOperationException("Subtask cannot be added after task is finished.");
      }

      this.m_subTasks.Enqueue(taskInfo);
    }

    /// <summary>
    /// Returns true if more tasks should be processed.
    /// If false is returned, we can exit, no tasks are more present.
    /// </summary>
    protected virtual bool WaitForTasks()
    {
      /* We should capture _processed_ value before other actions. 
       * That ensures if other thread set the exit status and notify us using stamp, we'll wake up.
       * 
       * If we capture it after we check whether all the threads are sleeping, it might happen that we'll stuck forever in case of exception.
       * I.e. Total num of threads: 15. 13 threads are sleeping. The 14th thread incremented free threads counter to 14 and verified that 14 != 15. It decided to go to sleep.
       * If at this moment the 15th thread failes, it exists and decrements the total threads counter. If 14th thread capture stamp now, it will sleep forever - nobody will update it more.
       * 
       * Because of that we should capture stamp before we do our comparison.
       */
      long oldStamp = this.m_processedStamp;

      if (this.m_finished)
      {
        return false;
      }

      /* Increase number of free theads. The only reason why we could exit is that all the threads finished their work.
       */

      int newNumberOfFreeThreads = Interlocked.Increment(ref this.m_numberOfFreeThreads);

      /* To ensure that _total_ is read only after increment operation. Also that is to ensure that _stamp_ is captured before we perform validation below.
       */
      Thread.MemoryBarrier();

      if (newNumberOfFreeThreads == this.m_numberOfThreads)
      {
        this.m_finished = true;
        return false;
      }

      this.DeepSleepPhase(oldStamp);

      Interlocked.Decrement(ref this.m_numberOfFreeThreads);

      return true;
    }

    /// <summary>
    /// During this phase thread spins in cycle until the task in either finished or new tasks appear.
    /// </summary>
    protected virtual void DeepSleepPhase(long oldStamp)
    {
      //So sense to await if tasks are finished. Also shold wake up if stamp was changed.
      while (!this.m_finished && oldStamp == this.m_processedStamp)
      {
        Thread.Sleep(0);
      }
    }

    /// <summary>
    /// This method update tasks stamp. Should be called each time the threads in deep sleep should wake up.
    /// Call to the method doesn't necessarily mean that new tasks are present. Rather it's used to notify about possible tasks.
    /// </summary>
    protected virtual void UpdateProcessedStamp()
    {
      Interlocked.Increment(ref this.m_processedStamp);
    }
  }
}