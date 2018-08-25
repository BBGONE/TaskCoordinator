using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TasksCoordinator.Interface;

namespace TasksCoordinator.Test
{
    public class TestMessageDispatcher: IMessageDispatcher<Message>
    {
       internal volatile int MESSAGES_PROCESSED;

        public TestMessageDispatcher()
        {
            this.MESSAGES_PROCESSED = 0;
        }

        private async Task<bool> DispatchMessage(Message message, WorkContext context)
        {
            CancellationToken cancellation = context.Cancellation;
            //возвратить ли сообщение назад в очередь?
            bool rollBack = false;
            TaskWorkType workType = (TaskWorkType)Enum.Parse(typeof(TaskWorkType), message.MessageType);
            switch (workType)
            {
                case TaskWorkType.LongCPUBound:
                    Task task = new Task(async () => {
                        try
                        {
                            await CPU_TASK(message, cancellation, 10000000).ConfigureAwait(false);
                        }
                        catch (OperationCanceledException)
                        {
                            //OK
                        }
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
                    await  CPU_TASK(message, cancellation, 5000).ConfigureAwait(false); 
                    break;
                case TaskWorkType.ShortIOBound:
                    await IO_TASK(message, cancellation, 500).ConfigureAwait(false);
                    break;
                case TaskWorkType.UltraShortCPUBound:
                    await CPU_TASK(message, cancellation, 5).ConfigureAwait(false);
                    break;
                case TaskWorkType.UltraShortIOBound:
                    await IO_TASK(message, cancellation, 10).ConfigureAwait(false);
                    break;
                case TaskWorkType.Mixed:
                    var workTypes = Enum.GetValues(typeof(TaskWorkType)).Cast<int>().Where(v => v < 100).ToArray();
                    Random rnd = new Random();
                    int val = rnd.Next(workTypes.Length-1);
                    TaskWorkType wrkType = (TaskWorkType)workTypes[val];
                    message.MessageType = Enum.GetName(typeof(TaskWorkType), wrkType);
                    var unused = await this.DispatchMessage(message, context).ConfigureAwait(false);
                    return false;
                default:
                    throw new InvalidOperationException("Unknown WorkType");
            }
            Interlocked.Increment(ref MESSAGES_PROCESSED);
            // Console.WriteLine($"SEQNUM:{message.SequenceNumber} - THREAD: {Thread.CurrentThread.ManagedThreadId} - TasksCount:{context.Coordinator.TasksCount} WorkType: {message.MessageType}");
            return rollBack;
        }

        // Test Task which consumes CPU
        private static async Task CPU_TASK(Message message, CancellationToken cancellation, int iterations)
        {
            await Task.FromResult(0);
            // Console.WriteLine($"THREAD: {Thread.CurrentThread.ManagedThreadId}");
            int cnt = iterations;
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
            return Task.Delay(durationMilliseconds, cancellation);
            // Console.WriteLine($"THREAD: {Thread.CurrentThread.ManagedThreadId}");
        }

        async Task<MessageProcessingResult> IMessageDispatcher<Message>.DispatchMessages(IEnumerable<Message> messages, 
            WorkContext context, Action<Message> onProcessStart)
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
                    rollBack = await this.DispatchMessage(message, context).ConfigureAwait(false);

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
