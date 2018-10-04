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

        public TestMessageReader(int taskId, ITaskCoordinatorAdvanced<TMessage> tasksCoordinator, ILog log, 
            BlockingCollection<TMessage> messageQueue, IMessageDispatcher<TMessage, object> dispatcher, int artificialDelay = 0) :
            base(taskId, tasksCoordinator, log, messageQueue, dispatcher)
        {
            this._artificialDelay = artificialDelay;
        }

        protected override async Task<TMessage> ReadMessage(bool isPrimaryReader, int taskId, CancellationToken token, object state)
        {
            if (this._artificialDelay > 0)
                await Task.Delay(this._artificialDelay);

            return await base.ReadMessage(isPrimaryReader, taskId, token, state);
        }
    }
}