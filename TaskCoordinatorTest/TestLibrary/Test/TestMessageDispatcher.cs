using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TasksCoordinator.Interface;
using TasksCoordinator.Test.Interface;

namespace TasksCoordinator.Test
{
    public class TestMessageDispatcher: IMessageDispatcher<Message>
    {
        private readonly ISerializer _serializer;
        private readonly ConcurrentDictionary<Guid, ICallback> _callbacks;

        public TestMessageDispatcher(ISerializer serializer)
        {
            this._serializer = serializer;
            this._callbacks = new ConcurrentDictionary<Guid, ICallback>();
        }

        private async Task<bool> DispatchMessage(Message message, WorkContext context)
        {
            CancellationToken cancellation = context.Cancellation;
            // возвратить ли сообщение назад в очередь?
            bool rollBack = false;
            Payload payload = this._serializer.Deserialize<Payload>(message.Body);
            TaskWorkType workType = payload.WorkType;
            switch (workType)
            {
                case TaskWorkType.LongCPUBound:
                    Task task = new Task(async () => {
                        try
                        {
                            await CPU_TASK(message, payload, cancellation, 10000000).ConfigureAwait(false);
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
                    await IO_TASK(message, payload, cancellation, 5000).ConfigureAwait(false);
                    break;
                case TaskWorkType.ShortCPUBound:
                    await  CPU_TASK(message, payload, cancellation, 5000).ConfigureAwait(false); 
                    break;
                case TaskWorkType.ShortIOBound:
                    await IO_TASK(message, payload, cancellation, 500).ConfigureAwait(false);
                    break;
                case TaskWorkType.UltraShortCPUBound:
                    await CPU_TASK(message, payload, cancellation, 5).ConfigureAwait(false);
                    break;
                case TaskWorkType.UltraShortIOBound:
                    await IO_TASK(message, payload, cancellation, 10).ConfigureAwait(false);
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
          
            // Console.WriteLine($"SEQNUM:{message.SequenceNumber} - THREAD: {Thread.CurrentThread.ManagedThreadId} - TasksCount:{context.Coordinator.TasksCount} WorkType: {payload.WorkType}");
            return rollBack;
        }

        // Test Task which consumes CPU
        private async Task CPU_TASK(Message message, Payload payload, CancellationToken cancellation, int iterations)
        {
            try
            {
                await Task.FromResult(0);
                // Console.WriteLine($"THREAD: {Thread.CurrentThread.ManagedThreadId}");
                int cnt = iterations;
                for (int i = 0; i < cnt; ++i)
                {
                    cancellation.ThrowIfCancellationRequested();
                    //rollBack = !rollBack;
                    //Do some CPU work 
                    payload.Result = System.Text.Encoding.UTF8.GetBytes(string.Format("qwertyuiop[;lkjhngbfd--cnt={0}", cnt));
                }
                payload.Result = System.Text.Encoding.UTF8.GetBytes(string.Format("CPU_TASK Time={0:dd.MM.yyyy HH:mm:ss.fff}---cnt={1}", DateTime.Now, cnt));
                cancellation.ThrowIfCancellationRequested();
                ICallback callback;
                if (this._callbacks.TryGetValue(payload.ClientID, out callback))
                {
                    message.Body = this._serializer.Serialize(payload);
                    callback.TaskCompleted(message, null);
                }
            }
            catch (OperationCanceledException)
            {
                //NOOP
            }
            catch (Exception ex)
            {
                ICallback callback;
                if (this._callbacks.TryGetValue(payload.ClientID, out callback))
                {
                    message.Body = this._serializer.Serialize(payload);
                    callback.TaskCompleted(message, ex.Message);
                }
                throw;
            }
        }

        // Test Task IO Bound
        private async Task IO_TASK(Message message, Payload payload, CancellationToken cancellation, int durationMilliseconds)
        {
            try
            {
                await Task.Delay(durationMilliseconds, cancellation);
                cancellation.ThrowIfCancellationRequested();
                payload.Result = System.Text.Encoding.UTF8.GetBytes(string.Format("IO_TASK Time={0:dd.MM.yyyy HH:mm:ss.fff}", DateTime.Now));
                cancellation.ThrowIfCancellationRequested();
                // Console.WriteLine($"THREAD: {Thread.CurrentThread.ManagedThreadId}");
                ICallback callback;
                if (this._callbacks.TryGetValue(payload.ClientID, out callback))
                {
                    message.Body = this._serializer.Serialize(payload);
                    callback.TaskCompleted(message, null);
                }
            }
            catch (OperationCanceledException)
            {
                //NOOP
            }
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

        public void RegisterCallback(Guid clientID, ICallback callback)
        {
            this._callbacks.AddOrUpdate(clientID, callback, (id, value) => callback);
        }

        public bool UnRegisterCallback(Guid clientID)
        {
            ICallback res;
            return this._callbacks.TryRemove(clientID, out res);
        }
    }
}
