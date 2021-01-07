using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using TasksCoordinator.Interface;

namespace TasksCoordinator
{
    public class ChannelMessageReader<TMessage> : MessageReader<TMessage, object>
    {
        public static readonly TimeSpan DefaultWaitForTimeout = TimeSpan.FromSeconds(10);
        private static readonly Task NOOP = Task.FromResult(0);

        private readonly ChannelReader<TMessage> _messageQueue;
        private readonly IMessageDispatcher<TMessage, object> _dispatcher;

        public ChannelMessageReader(long taskId, ITaskCoordinatorAdvanced tasksCoordinator, ILogger logger,
            ChannelReader<TMessage> messageQueue, IMessageDispatcher<TMessage, object> dispatcher) :
            base(taskId, tasksCoordinator, logger)
        {
            this._messageQueue = messageQueue;
            this._dispatcher = dispatcher;
        }

        protected override async Task<TMessage> ReadMessage(bool isPrimaryReader, long taskId, CancellationToken token, object state)
        {
            await NOOP;
            TMessage msg = default(TMessage);
            bool isOK = false;
          
            if (isPrimaryReader)
            {
                // for the Primary reader (it waits for messages when the queue is empty)
                if (!_messageQueue.TryRead(out  msg))
                {
                    msg = await _messageQueue.ReadAsync(token);
                }
                isOK = true;
            }
            else
            {
                isOK = _messageQueue.TryRead(out msg);
            }

            token.ThrowIfCancellationRequested();
            return isOK? msg: default(TMessage);
        }

        protected override async Task<MessageProcessingResult> DispatchMessage(TMessage message, long taskId, CancellationToken token, object state)
        {
            var res = await this._dispatcher.DispatchMessage(message, taskId, token, null);
            return res;
        }
        
        protected override async Task<int> DoWork(bool isPrimaryReader, CancellationToken token)
        {
            int cnt = 0;
            TMessage msg = default(TMessage);
            using (var disposable = this.Coordinator.ReadThrottle(isPrimaryReader))
            {
                msg = await this.ReadMessage(isPrimaryReader, this.taskId, token, null);
            }

            cnt = msg == null ? 0 : 1;
            if (cnt > 0)
            {
                this.Coordinator.OnBeforeDoWork(this);
                try
                {
                    MessageProcessingResult res = await this.DispatchMessage(msg, this.taskId, token, null);
                    if (res.isRollBack)
                    {
                        await this.OnRollback(msg, token);
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

        protected override async Task OnRollback(TMessage msg, CancellationToken cancellation)
        {
            // Should cancel message retrieval here
            await Task.CompletedTask;
        }

        protected ChannelReader<TMessage> MessageQueue => _messageQueue;

        protected IMessageDispatcher<TMessage, object> Dispatcher => _dispatcher;
    }
}