using Shared;
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using TasksCoordinator.Interface;

namespace TasksCoordinator.Test
{
    public class TestMessageReader<TMessage> : InMemoryMessageReader<TMessage>
        where TMessage: class
    {
        private readonly int _artificialDelay;
        private static volatile int _current_cnt = 0;
        public static volatile int MaxConcurrentReading = 0;

        public TestMessageReader(long taskId, ITaskCoordinatorAdvanced<TMessage> tasksCoordinator, ILog log, 
            BlockingCollection<TMessage> messageQueue, IMessageDispatcher<TMessage, object> dispatcher, int artificialDelay = 0) :
            base(taskId, tasksCoordinator, log, messageQueue, dispatcher)
        {
            this._artificialDelay = artificialDelay;
        }

        protected override async Task<TMessage> ReadMessage(bool isPrimaryReader, long taskId, CancellationToken token, object state)
        {
            int cnt = Interlocked.Increment(ref _current_cnt);
            if (cnt > MaxConcurrentReading)
            {
                MaxConcurrentReading = cnt;
            }
            try
            {
                if (this._artificialDelay > 0)
                    Thread.SpinWait(10000);
                var res = await base.ReadMessage(isPrimaryReader, taskId, token, state);
                return res;

            }
            finally
            {
                Interlocked.Decrement(ref _current_cnt);
            }
        }
    }
}