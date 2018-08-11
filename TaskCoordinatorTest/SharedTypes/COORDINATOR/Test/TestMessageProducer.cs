using System.Linq;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Threading;
using TasksCoordinator.Interface;

namespace TasksCoordinator.Test
{
    public class TestMessageProducer: IMessageProducer<Message>
    {
        private TimeSpan DefaultWaitForTimeout = TimeSpan.FromSeconds(30);
        private bool _IsQueueActivationEnabled = false;
        private CancellationToken _cancellation;
        private readonly BlockingCollection<Message> _messageQueue;

        public TestMessageProducer(BlockingCollection<Message> messageQueue)
        {
            this._messageQueue = messageQueue;
            this._cancellation = CancellationToken.None;
        }

        public BlockingCollection<Message> MessageQueue
        {
            get { return _messageQueue; }
        }

        public bool IsQueueActivationEnabled {
            get { return _IsQueueActivationEnabled; }
            set {
                _IsQueueActivationEnabled = value;
                this.DefaultWaitForTimeout = this.IsQueueActivationEnabled ? TimeSpan.FromSeconds(3) : TimeSpan.FromSeconds(30);
            }
        }

        public CancellationToken Cancellation {
            get { return _cancellation; }
            set { _cancellation = value; }
        }

        async Task<int> IMessageProducer<Message>.GetMessages(IMessageWorker<Message> worker, bool isWaitForEnabled)
        {
            int cnt = 0;
            //Console.WriteLine(string.Format("begin {0}", worker.taskId));
            IEnumerable<Message> messages = await this.ReadMessages(isWaitForEnabled, worker.taskId).ConfigureAwait(false);
            cnt = messages.Count();
            //Console.WriteLine(string.Format("end {0}", worker.taskId));
          
            if (cnt > 0)
            {
                bool isOk = worker.OnBeforeDoWork();
                try
                {
                    //обработка сообщений
                    MessageProcessingResult res = await worker.OnDoWork(messages, null);

                    if (res.isRollBack)
                    {
                        // returns the Message to the queue
                        foreach (var msg in messages)
                        {
                            this._messageQueue.Add(msg);
                        }
                    }
                }
                finally
                {
                    worker.OnAfterDoWork();
                }
            }
            return cnt;
        }

        private async Task<IEnumerable<Message>> ReadMessages(bool isWaitForEnabled, int taskId)
        {
            LinkedList<Message> messages = new LinkedList<Message>();
            Random rnd = new Random();
            Message msg;
            if (isWaitForEnabled)
            {
                try {
                    if (!_messageQueue.TryTake(out msg, Convert.ToInt32(DefaultWaitForTimeout.TotalMilliseconds), this.Cancellation))
                    {
                        msg = null;
                    }
                    await Task.Delay(50).ConfigureAwait(false);
                   // Console.WriteLine(string.Format("Primary reading {0}", taskId));
                }
                catch (OperationCanceledException)
                {
                    return messages;
                }
                if (msg != null)
                {
                    // msg.ServiceName = $"TaskID:{taskId.ToString()}";
                    messages.AddLast(msg);
                }
            }
            else
            {
                if (_messageQueue.TryTake(out msg))
                {
                    // msg.ServiceName = $"TaskID:{taskId.ToString()}";
                    await Task.Delay(50).ConfigureAwait(false);
                    //Console.WriteLine(string.Format("Secondary reading {0}", taskId));
                    messages.AddLast(msg);
                }
            }

            return messages;
        }
    }
}
