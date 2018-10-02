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

        #region Private Fields
        private readonly BlockingCollection<TMessage> _messageQueue;
        private readonly IMessageDispatcher<TMessage, object> _dispatcher;
        #endregion

        public InMemoryMessageReader(int taskId, ITaskCoordinatorAdvanced<TMessage> tasksCoordinator, ILog log, 
            BlockingCollection<TMessage> messageQueue, IMessageDispatcher<TMessage, object> dispatcher) :
            base(taskId, tasksCoordinator, log)
        {
            this._messageQueue = messageQueue;
            this._dispatcher = dispatcher;
        }

        protected override async Task<TMessage> ReadMessage(bool isPrimaryReader, int taskId, CancellationToken token, object state)
        {
            await NOOP;
            TMessage msg;
            bool isOK = false;
            // Make an artificial slight delay resembling reading over network
            //Thread.SpinWait(5000);
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

        protected override async Task<MessageProcessingResult> DispatchMessage(TMessage message, int taskId, CancellationToken token, object state)
        {
            var res = await this._dispatcher.DispatchMessage(message, taskId, token, null).ConfigureAwait(false);
            return res;
        }
        
        protected override async Task<int> DoWork(bool isPrimaryReader, CancellationToken token)
        {
            int cnt = 0;
            TMessage msg = null;
            IDisposable readDisposable = await this.Coordinator.WaitReadAsync(this).ConfigureAwait(false);
            try
            {
                msg = await this.ReadMessage(isPrimaryReader, this.taskId, token, null).ConfigureAwait(false);
                cnt = msg == null ? 0 : 1;
            }
            finally
            {
                readDisposable.Dispose();
            }
            if (cnt > 0)
            {
                bool isOk = this.Coordinator.OnBeforeDoWork(this);
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
                    if (isOk)
                        this.Coordinator.OnAfterDoWork(this);
                }
            }

            return cnt;
        }

        protected override void OnRollback(TMessage msg, CancellationToken cancellation)
        {
            _messageQueue.Add(msg, cancellation);
        }
    }
}