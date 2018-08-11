using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TasksCoordinator.Interface;
using System.Linq;

namespace TasksCoordinator.Test
{
    public class TestMessageDispatcher: IMessageDispatcher<Message>
    {
        #region TEST RESULTS
        // Work type (category)  for testing message processing
        private TaskWorkType _WorkType;
        private ConcurrentBag<Message> _processedMessages;
        public ConcurrentBag<Message> ProcessedMessages
        {
            get { return _processedMessages; }
        }
        #endregion

        public TestMessageDispatcher(ConcurrentBag<Message> processedMessages, TaskWorkType workType)
        {
            this._processedMessages = processedMessages;
            this._WorkType = workType;
        }

        private async Task<bool> DispatchMessage(Message message, WorkContext context, TaskWorkType workType)
        {
            CancellationToken cancellation = context.Cancellation;
            //возвратить ли сообщение назад в очередь?
            bool rollBack = false;

            switch(workType)
            {
                case TaskWorkType.LongCPUBound:
                    Task task = new Task(async () => {
                        await CPU_TASK(message, cancellation, 1000000);
                    }, cancellation, TaskCreationOptions.LongRunning);

                    cancellation.ThrowIfCancellationRequested();

                    if (!task.IsCanceled)
                    {
                        task.Start();
                        await task.ConfigureAwait(false);
                    }
                    break;
                case TaskWorkType.LongIOBound:
                    await IO_TASK(message, cancellation, 5000).ConfigureAwait(false);
                    break;
                case TaskWorkType.ShortCPUBound:
                    await  CPU_TASK(message, cancellation, 10000).ConfigureAwait(false); 
                    break;
                case TaskWorkType.ShortIOBound:
                    await IO_TASK(message, cancellation, 500).ConfigureAwait(false);
                    break;
                case TaskWorkType.UltraShortCPUBound:
                    await CPU_TASK(message, cancellation, 1000).ConfigureAwait(false);
                    break;
                case TaskWorkType.UltraShortIOBound:
                    await IO_TASK(message, cancellation, 100).ConfigureAwait(false);
                    break;
                case TaskWorkType.Mixed:
                    var workTypes = Enum.GetValues(typeof(TaskWorkType)).Cast<int>().Where(v => v < 100).ToArray();
                    Random rnd = new Random();
                    int val = rnd.Next(workTypes.Length-1);
                    TaskWorkType wrkType = (TaskWorkType)workTypes[val];
                    var unused =await this.DispatchMessage(message, context, wrkType);
                    return false;
                default:
                    throw new InvalidOperationException("Unknown WorkType");
            }

           ProcessedMessages.Add(message);
            Console.WriteLine($"SEQNUM:{message.SequenceNumber} - THREAD: {Thread.CurrentThread.ManagedThreadId} - TasksCount:{context.Coordinator.TasksCount} WorkType: {workType}");
            return rollBack;
        }

        // Test Task which consumes CPU
        private static async Task CPU_TASK(Message message, CancellationToken cancellation, int iterations)
        {
            await Task.FromResult(0);
            // Console.WriteLine($"THREAD: {Thread.CurrentThread.ManagedThreadId}");
            Random rnd = new Random();
            int cnt = rnd.Next(iterations / 5, iterations);
            for (int i = 0; i < cnt; ++i)
            {
                cancellation.ThrowIfCancellationRequested();
                //rollBack = !rollBack;
                message.Body = System.Text.Encoding.UTF8.GetBytes(string.Format("i={0}---cnt={1}", i, cnt));
            }
        }

        // Test Task IO Bound
        private static Task IO_TASK(Message message, CancellationToken cancellation, int durationMilliseconds)
        {
            Random rnd = new Random();
            int msecs = rnd.Next(durationMilliseconds / 5, durationMilliseconds);

            return Task.Delay(msecs, cancellation);
            // Console.WriteLine($"THREAD: {Thread.CurrentThread.ManagedThreadId}");
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
                    rollBack = await this.DispatchMessage(message, context, this._WorkType);

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
