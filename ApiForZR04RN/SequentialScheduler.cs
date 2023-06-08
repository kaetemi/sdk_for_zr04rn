using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace ApiForZR04RN
{
    public class SequentialScheduler : TaskScheduler, IDisposable
    {
        readonly BlockingCollection<Task> m_taskQueue = new BlockingCollection<Task>();
        readonly Thread m_thread;
        readonly CancellationTokenSource m_cancellation; // CR comment: field added
        volatile bool m_disposed;  // CR comment: volatile added

        public SequentialScheduler()
        {
            m_cancellation = new CancellationTokenSource();
            m_thread = new Thread(Run);
            m_thread.Start();
        }
        public override int MaximumConcurrencyLevel
        {
            get { return 1; }
        }

        public void Dispose()
        {
            m_disposed = true;
            m_cancellation.Cancel(); // CR comment: cancellation added
        }

        void Run()
        {
            while (!m_disposed)
            {
                // CR comment: dispose gracefully
                try
                {
                    var task = m_taskQueue.Take(m_cancellation.Token);
                    // Debug.Assert(TryExecuteTask(task));
                    TryExecuteTask(task); // CR comment: not sure about the Debug.Assert here
                }
                catch (OperationCanceledException)
                {
                    Debug.Assert(m_disposed);
                }
            }
        }

        protected override IEnumerable<Task> GetScheduledTasks()
        {
            return m_taskQueue;
        }

        protected override void QueueTask(Task task)
        {
            m_taskQueue.Add(task);
        }

        protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
        {
            if (Thread.CurrentThread == m_thread)
            {
                return TryExecuteTask(task);
            }
            return false;
        }
    }
}
