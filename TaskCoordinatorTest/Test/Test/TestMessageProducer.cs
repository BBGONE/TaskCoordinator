using System.Linq;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Threading;
using TasksCoordinator.Interface;
using Shared.Services;

namespace TasksCoordinator.Test
{
    public class TestMessageProducer: IMessageProducer<Message>
    {
        // adds a realistic delay - like when the messages are read from the database transport (instead of a local in memory queue)
        private const int READ_MESSAGE_DELAY = 0;
        private TimeSpan DefaultWaitForTimeout = TimeSpan.FromSeconds(30);
        private ITaskService _service;
        private CancellationToken _cancellation;
        private readonly BlockingCollection<Message> _messageQueue;

        public TestMessageProducer(ITaskService service, BlockingCollection<Message> messageQueue)
        {
            this._service = service;
            this._messageQueue = messageQueue;
            this._cancellation = CancellationToken.None;
            this.DefaultWaitForTimeout = this._service.isQueueActivationEnabled ? TimeSpan.FromSeconds(3) : TimeSpan.FromSeconds(30);
        }

        public BlockingCollection<Message> MessageQueue
        {
            get { return _messageQueue; }
        }

        public bool IsQueueActivationEnabled {
            get { return this._service.isQueueActivationEnabled; }
        }

        public CancellationToken Cancellation {
            get { return _cancellation; }
            set { _cancellation = value; }
        }

        async Task<int> IMessageProducer<Message>.DoWork(IMessageWorker<Message> worker, bool isPrimaryReader)
        {
            int cnt = 0;
            //Console.WriteLine(string.Format("begin {0}", worker.taskId));
            IEnumerable<Message> messages = await this.ReadMessages(isPrimaryReader, worker.taskId).ConfigureAwait(false);
            cnt = messages.Count();
            //Console.WriteLine(string.Format("end {0}", worker.taskId));

            if (cnt > 0)
            {
                bool isOk = worker.OnBeforeDoWork();
                try
                {
                    //обработка сообщений
                    MessageProcessingResult res = await worker.OnDoWork(messages, null).ConfigureAwait(false);

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

        private async Task<IEnumerable<Message>> ReadMessages(bool isPrimaryReader, int taskId)
        {
            LinkedList<Message> messages = new LinkedList<Message>();
            Random rnd = new Random();
            Message msg;
            // for the Primary reader (it waits for messages when the queue is empty)
            if (isPrimaryReader)
            {
                try {
                    if (!_messageQueue.TryTake(out msg, Convert.ToInt32(DefaultWaitForTimeout.TotalMilliseconds), this.Cancellation))
                    {
                        msg = null;
                    }
                    await Task.Delay(READ_MESSAGE_DELAY).ConfigureAwait(false);
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
                    await Task.Delay(READ_MESSAGE_DELAY).ConfigureAwait(false);
                    //Console.WriteLine(string.Format("Secondary reading {0}", taskId));
                    messages.AddLast(msg);
                }
            }

            return messages;
        }
    }
}
