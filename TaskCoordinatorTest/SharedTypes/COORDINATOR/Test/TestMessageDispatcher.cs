using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TasksCoordinator.Interface;

namespace TasksCoordinator.Test
{
    public class TestMessageDispatcher: IMessageDispatcher<Message>
    {
        #region TEST RESULTS
        private ConcurrentBag<Message> _processedMessages;
        public ConcurrentBag<Message> ProcessedMessages
        {
            get { return _processedMessages; }
        }
        #endregion

        public TestMessageDispatcher(ConcurrentBag<Message> processedMessages)
        {
            this._processedMessages = processedMessages;
        }

        private async Task<bool> DispatchMessage(Message message, WorkContext context)
        {
            CancellationToken cancellation = context.Cancellation;
            //возвратить ли сообщение назад в очередь?
            bool rollBack = false;

            bool stress = true;
            if (stress)
            {
                await CPU_TASK(message, cancellation);
            }
            else
            {
                await Task.Delay(500, cancellation).ConfigureAwait(false);
            }

            ProcessedMessages.Add(message);
            Console.WriteLine($"SEQNUM:{message.SequenceNumber} - THREAD: {Thread.CurrentThread.ManagedThreadId} - TasksCount:{context.Coordinator.TasksCount}");
            return rollBack;
        }

        // Test Task which consumes CPU
        private static async Task CPU_TASK(Message message, CancellationToken cancellation)
        {
            await Task.Delay(50, cancellation).ConfigureAwait(false);
            // Console.WriteLine($"THREAD: {Thread.CurrentThread.ManagedThreadId}");
            Random rnd = new Random();
            int cnt = rnd.Next(100000, 500000);
            for (int i = 0; i < cnt; ++i)
            {
                cancellation.ThrowIfCancellationRequested();
                //rollBack = !rollBack;
                message.Body = System.Text.Encoding.UTF8.GetBytes(string.Format("i={0}---cnt={1}", i, cnt));
            }
        }

        async Task<MessageProcessingResult> IMessageDispatcher<Message>.DispatchMessages(IEnumerable<Message> messages, WorkContext context,
        Action<Message> onProcessStart)
        {
            bool rollBack = false;
            Message currentMessage = null;
            onProcessStart(currentMessage);

            //SSSBMessageProducer передает Connection в объекте state
            //var dbconnection = context.state as SqlConnection;
            try
            {
                //Как бы обработка сообщений
                foreach (Message message in messages)
                {
                    currentMessage = message;
                    onProcessStart(currentMessage);
                    rollBack = await this.DispatchMessage(message, context);

                    if (rollBack)
                        break;
                }
            }
            finally
            {
                onProcessStart(null);
            }
            return new MessageProcessingResult() { isRollBack = rollBack };
        }
    }
}
