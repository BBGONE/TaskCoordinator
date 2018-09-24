using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TasksCoordinator.Interface;

namespace TasksCoordinator
{
    public class InMemoryMessageReader<TMessage> : MessageReader<TMessage, Object>
    {
        public static readonly TimeSpan DefaultWaitForTimeout = TimeSpan.FromSeconds(10);

        #region Private Fields
        private readonly BlockingCollection<TMessage> _messageQueue;
        #endregion

        public InMemoryMessageReader(int taskId, ITaskCoordinatorAdvanced<TMessage> tasksCoordinator, BlockingCollection<TMessage> messageQueue) :
            base(taskId, tasksCoordinator)
        {
            this._messageQueue = messageQueue;
        }

        protected override async Task<IEnumerable<TMessage>> ReadMessages(bool isPrimaryReader, int taskId, CancellationToken cancellation, object state)
        {
            await Task.FromResult(0);
            LinkedList<TMessage> messages = new LinkedList<TMessage>();
            TMessage msg;
            // for the Primary reader (it waits for messages when the queue is empty)
            if (isPrimaryReader)
            {
                // Console.WriteLine(string.Format("Primary reading {0}", taskId));
                if (_messageQueue.TryTake(out msg, Convert.ToInt32(DefaultWaitForTimeout.TotalMilliseconds), cancellation))
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

            cancellation.ThrowIfCancellationRequested();


            return messages;
        }

        protected override void OnRollback(TMessage msg, CancellationToken cancellation)
        {
            _messageQueue.Add(msg, cancellation);
        }
    }
}