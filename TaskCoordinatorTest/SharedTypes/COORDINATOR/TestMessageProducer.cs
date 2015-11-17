using System.Linq;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Collections.Concurrent;

namespace TasksCoordinator
{
    public class TestMessageProducer: IMessageProducer<Message>
    {
        private readonly TimeSpan DefaultWaitForTimeout;
        private TestTasksCoordinator _owner;
        private static BlockingCollection<Message> _messageQueue = new BlockingCollection<Message>();

        public TestMessageProducer(TestTasksCoordinator owner)
        {
            this._owner = owner;
            this.DefaultWaitForTimeout = this._owner.IsQueueActivationEnabled ? TimeSpan.FromSeconds(3) : TimeSpan.FromSeconds(30);
        }

        public static BlockingCollection<Message> MessageQueue
        {
            get { return _messageQueue; }
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
                        //возврат сообщений в очередь
                        foreach (var msg in messages)
                            _messageQueue.Add(msg);
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
                    if (!_messageQueue.TryTake(out msg, Convert.ToInt32(DefaultWaitForTimeout.TotalMilliseconds), this._owner.Cancellation))
                        msg = null;
                    await Task.Delay(50).ConfigureAwait(false);
                   // Console.WriteLine(string.Format("Primary reading {0}", taskId));
                }
                catch (OperationCanceledException)
                {
                    return messages;
                }
                if (msg != null)
                {
                    msg.ServiceName = taskId.ToString();
                    messages.AddLast(msg);
                }
            }
            else
            {
                if (_messageQueue.TryTake(out msg))
                {
                    await Task.Delay(50).ConfigureAwait(false);
                    //Console.WriteLine(string.Format("Secondary reading {0}", taskId));
                    msg.ServiceName = taskId.ToString();
                    messages.AddLast(msg);
                }
            }

            return messages;
        }
    }
}
