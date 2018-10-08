using Shared;
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using TasksCoordinator.Interface;

namespace TasksCoordinator
{
    public class InMemoryMessageReader<TMessage> : MessageReader<TMessage, object>
        where TMessage: class
    {
        public static readonly TimeSpan DefaultWaitForTimeout = TimeSpan.FromSeconds(10);
        private static readonly Task NOOP = Task.FromResult(0);

        private readonly BlockingCollection<TMessage> _messageQueue;
        private readonly IMessageDispatcher<TMessage, object> _dispatcher;

        public InMemoryMessageReader(long taskId, ITaskCoordinatorAdvanced tasksCoordinator, ILog log, 
            BlockingCollection<TMessage> messageQueue, IMessageDispatcher<TMessage, object> dispatcher) :
            base(taskId, tasksCoordinator, log)
        {
            this._messageQueue = messageQueue;
            this._dispatcher = dispatcher;
        }

        protected override async Task<TMessage> ReadMessage(bool isPrimaryReader, long taskId, CancellationToken token, object state)
        {
            await NOOP;
            TMessage msg;
            bool isOK = false;
          
            if (isPrimaryReader)
            {
                // for the Primary reader (it waits for messages when the queue is empty)
                isOK = _messageQueue.TryTake(out msg, Convert.ToInt32(DefaultWaitForTimeout.TotalMilliseconds), token);
            }
            else
            {
                isOK = _messageQueue.TryTake(out msg, 0);
            }

            token.ThrowIfCancellationRequested();


            return isOK? msg: null;
        }

        protected override async Task<MessageProcessingResult> DispatchMessage(TMessage message, long taskId, CancellationToken token, object state)
        {
            var res = await this._dispatcher.DispatchMessage(message, taskId, token, null).ConfigureAwait(false);
            return res;
        }
        
        protected override async Task<int> DoWork(bool isPrimaryReader, CancellationToken token)
        {
            int cnt = 0;
            TMessage msg = null;
            using (var disposable = await this.Coordinator.WaitReadAsync())
            {
                msg = await this.ReadMessage(isPrimaryReader, this.taskId, token, null).ConfigureAwait(false);
            }

            cnt = msg == null ? 0 : 1;
            if (cnt > 0)
            {
                this.Coordinator.OnBeforeDoWork(this);
                try
                {
                    MessageProcessingResult res = await this.DispatchMessage(msg, this.taskId, token, null).ConfigureAwait(false);
                    if (res.isRollBack)
                    {
                        this.OnRollback(msg, token);
                    }
                }
                catch (Exception ex)
                {
                    this.OnProcessMessageException(ex, msg);
                    throw;
                }
                finally
                {
                    this.Coordinator.OnAfterDoWork(this);
                }
            }

            return cnt;
        }

        protected override void OnRollback(TMessage msg, CancellationToken cancellation)
        {
            _messageQueue.Add(msg, cancellation);
        }

        protected BlockingCollection<TMessage> MessageQueue => _messageQueue;

        protected IMessageDispatcher<TMessage, object> Dispatcher => _dispatcher;
    }
}