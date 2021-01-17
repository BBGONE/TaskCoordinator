using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace TSM.TasksCoordinator.Test
{
    public class ChannelMessageReader<TMessage> : MessageReader<TMessage, object>
    {
        public static readonly TimeSpan DefaultWaitForTimeout = TimeSpan.FromSeconds(10);
        private static readonly Task NOOP = Task.CompletedTask;

        private readonly ChannelReader<TMessage> _messageQueue;
        private readonly IMessageDispatcher<TMessage, object> _dispatcher;

        public ChannelMessageReader(long taskId, ITaskCoordinatorAdvanced tasksCoordinator, ILogger logger,
            ChannelReader<TMessage> messageQueue, IMessageDispatcher<TMessage, object> dispatcher) :
            base(taskId, tasksCoordinator, logger)
        {
            this._messageQueue = messageQueue;
            this._dispatcher = dispatcher;
        }

        protected override async Task<TMessage[]> ReadMessages(bool isPrimaryReader, long taskId, CancellationToken token, object state)
        {
            TMessage[] msgs = null;

            bool isOK;
          
            if (isPrimaryReader)
            {
                // for the Primary reader (it waits for messages when the queue is empty)
                isOK = _messageQueue.TryRead(out var msg);
                if (!isOK)
                {
                    msg = await _messageQueue.ReadAsync(token);
                    isOK = true;
                }

                if (isOK)
                {
                    msgs = new[] { msg };
                }
            }
            else
            {
                isOK = _messageQueue.TryRead(out var msg);

                if (isOK)
                {
                    msgs = new[] { msg };
                }
            }

            token.ThrowIfCancellationRequested();
            return msgs;
        }

        protected override async Task<MessageProcessingResult> DispatchMessage(TMessage message, long taskId, CancellationToken token, object state)
        {
            var res = await this._dispatcher.DispatchMessage(message, taskId, token, null);
            return res;
        }
        
        /// <summary>
        ///  Reads messages from the queue
        /// </summary>
        /// <param name="isPrimaryReader"></param>
        /// <param name="token"></param>
        /// <returns>Returns the number of read messages</returns>
        protected override async Task<int> DoWork(bool isPrimaryReader, CancellationToken token)
        {
            TMessage[] msgs;

            using (var disposable = this.Coordinator.ReadThrottle(isPrimaryReader))
            {
                msgs = await this.ReadMessages(isPrimaryReader, this.taskId, token, null);
            }

            if (msgs != null)
            {
                for (int i = 0; i < msgs.Length; ++i)
                {
                    this.Coordinator.OnBeforeDoWork(this);
                    try
                    {
                        MessageProcessingResult res = await this.DispatchMessage(msgs[i], this.taskId, token, null);
                        if (res.isRollBack)
                        {
                            await this.OnRollback(msgs[i], token);
                        }
                    }
                    catch (Exception ex)
                    {
                        this.OnProcessMessageException(ex, msgs[i]);
                        throw;
                    }
                    finally
                    {
                        this.Coordinator.OnAfterDoWork(this);
                    }
                }
            }

            return msgs?.Length??0;
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