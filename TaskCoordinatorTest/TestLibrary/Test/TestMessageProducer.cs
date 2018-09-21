using Shared.Services;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TasksCoordinator.Interface;

namespace TasksCoordinator.Test
{
    public class TestMessageProducer<M>: IMessageProducer<M>
    {
        private TimeSpan DefaultWaitForTimeout = TimeSpan.FromSeconds(30);
        private ITaskService _service;
        private readonly BlockingCollection<M> _messageQueue;
   

        public TestMessageProducer(ITaskService service, BlockingCollection<M> messageQueue)
        {
            this._service = service;
            this._messageQueue = messageQueue;
            this.DefaultWaitForTimeout = this._service.isQueueActivationEnabled ? TimeSpan.FromSeconds(3) : TimeSpan.FromSeconds(5);
        }

        private BlockingCollection<M> MessageQueue
        {
            get { return _messageQueue; }
        }

        public bool IsQueueActivationEnabled {
            get { return this._service.isQueueActivationEnabled; }
        }

        public async Task<IEnumerable<M>> ReadMessages(bool isPrimaryReader, int taskId, CancellationToken cancellation, object state)
        {
            await Task.FromResult(0);
            LinkedList<M> messages = new LinkedList<M>();
            M msg;
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
    }
}
