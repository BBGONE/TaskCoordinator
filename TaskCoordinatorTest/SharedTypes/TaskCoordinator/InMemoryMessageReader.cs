using Shared;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TasksCoordinator.Interface;

namespace TasksCoordinator
{
    public class InMemoryMessageReader<TMessage> : MessageReader<TMessage, object>
    {
        public static readonly TimeSpan DefaultWaitForTimeout = TimeSpan.FromSeconds(10);

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

        protected override async Task<IEnumerable<TMessage>> ReadMessages(bool isPrimaryReader, int taskId, CancellationToken token, object state)
        {
            await Task.FromResult(0);
            LinkedList<TMessage> messages = new LinkedList<TMessage>();
            TMessage msg;
            // for the Primary reader (it waits for messages when the queue is empty)
            if (isPrimaryReader)
            {
                // Console.WriteLine(string.Format("Primary reading {0}", taskId));
                if (_messageQueue.TryTake(out msg, Convert.ToInt32(DefaultWaitForTimeout.TotalMilliseconds), token))
                {
                    // msg.ServiceName = $"TaskID:{taskId.ToString()}";
                    messages.AddLast(msg);
                }
            }
            else
            {
                if (_messageQueue.TryTake(out msg, 0))
                {
                    // msg.ServiceName = $"TaskID:{taskId.ToString()}";
                    //Console.WriteLine(string.Format("Secondary reading {0}", taskId));
                    messages.AddLast(msg);
                }
            }

            token.ThrowIfCancellationRequested();


            return messages;
        }

        protected override async Task<MessageProcessingResult> DispatchMessage(TMessage message, int taskId, CancellationToken token, object state)
        {
            // Console.WriteLine(string.Format("DO WORK {0}", taskId));
            // Console.WriteLine(string.Format("Task: {0} Thread: {1}", taskId, Thread.CurrentThread.ManagedThreadId));
            var res = await this._dispatcher.DispatchMessage(message, taskId, token, null).ConfigureAwait(false);
            return res;
        }
        
        protected override async Task<int> DoWork(bool isPrimaryReader, CancellationToken token)
        {
            int cnt = 0;
            // Console.WriteLine(string.Format("begin {0} Thread: {1}", this.taskId, Thread.CurrentThread.ManagedThreadId));
            IEnumerable<TMessage> messages = await this.ReadMessages(isPrimaryReader, this.taskId, token, null).ConfigureAwait(false);
            cnt = messages.Count();
            // Console.WriteLine(string.Format("end {0} {1} {2}", this.taskId, isPrimaryReader, Thread.CurrentThread.ManagedThreadId));
            if (cnt > 0)
            {
                bool isOk = this.Coordinator.OnBeforeDoWork(this);
                try
                {
                    foreach (TMessage msg in messages)
                    {
                        // обработка сообщений
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
                    }
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
    }
}