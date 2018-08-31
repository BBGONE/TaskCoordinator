using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TasksCoordinator.Interface;
using TasksCoordinator.Test.Interface;

namespace TasksCoordinator.Test
{
    public class TestMessageDispatcher: IMessageDispatcher<Message>
    {
        private readonly ISerializer _serializer;
        private readonly ConcurrentDictionary<Guid, ICallbackProxy> _callbacks;

        public TestMessageDispatcher(ISerializer serializer)
        {
            this._serializer = serializer;
            this._callbacks = new ConcurrentDictionary<Guid, ICallbackProxy>();
        }

        private async Task<bool> DispatchMessage(Message message, WorkContext context)
        {
            // возвратить ли сообщение назад в очередь?
            bool rollBack = false;
            CancellationToken cancellation = context.Cancellation;
            Payload payload = this._serializer.Deserialize<Payload>(message.Body);
            payload.TryCount += 1;
            TaskWorkType workType = payload.WorkType;
            switch (workType)
            {
                case TaskWorkType.LongCPUBound:
                    Task<Task> task = new Task<Task>(async () =>
                    {
                        try
                        {
                            await CPU_TASK(message, payload, cancellation, 5000000, context.taskId).ConfigureAwait(false);
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
                        var innerTask = await task.ConfigureAwait(false);
                        await innerTask.ConfigureAwait(false);
                        // Console.WriteLine($"CPU_TASK {message.SequenceNumber} ENDED");
                    }
                    break;
                case TaskWorkType.LongIOBound:
                    await IO_TASK(message, payload, cancellation, 5000).ConfigureAwait(false);
                    break;
                case TaskWorkType.ShortCPUBound:
                    await CPU_TASK(message, payload, cancellation, 5000, context.taskId).ConfigureAwait(false);
                    break;
                case TaskWorkType.ShortIOBound:
                    await IO_TASK(message, payload, cancellation, 500).ConfigureAwait(false);
                    break;
                case TaskWorkType.UltraShortCPUBound:
                    await CPU_TASK(message, payload, cancellation, 5, context.taskId).ConfigureAwait(false);
                    break;
                case TaskWorkType.UltraShortIOBound:
                    await IO_TASK(message, payload, cancellation, 10).ConfigureAwait(false);
                    break;
                case TaskWorkType.Random:
                    throw new InvalidOperationException("Random WorkType is not Supported");
                default:
                    throw new InvalidOperationException($"Unknown WorkType {workType}");
            }

            // Console.WriteLine($"SEQNUM:{message.SequenceNumber} - THREAD: {Thread.CurrentThread.ManagedThreadId} - TasksCount:{context.Coordinator.TasksCount} WorkType: {payload.WorkType}");
            return rollBack;
        }

        // Test Task which consumes CPU
        private async Task CPU_TASK(Message message, Payload payload, CancellationToken cancellation, int iterations, int taskId)
        {
            ICallbackProxy callback;
            if (!this._callbacks.TryGetValue(payload.ClientID, out callback))
            {
                return;
            }
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
                if (payload.RaiseError && payload.TryCount < 2)
                {
                    throw new Exception($"Test Exception TryCount: {payload.TryCount}");
                }
                cancellation.ThrowIfCancellationRequested();
                payload.Result = System.Text.Encoding.UTF8.GetBytes(string.Format("CPU_TASK cnt={0} Try: {1}", cnt, payload.TryCount));
                cancellation.ThrowIfCancellationRequested();
                message.Body = this._serializer.Serialize(payload);
                callback.TaskCompleted(message, null);
            }
            catch (OperationCanceledException)
            {
                message.Body = this._serializer.Serialize(payload);
                callback.TaskCompleted(message, "CANCELLED");
            }
            catch (Exception ex)
            {
                message.Body = this._serializer.Serialize(payload);
                callback.TaskCompleted(message, ex.Message);
            }
        }

        // Test Task IO Bound
        private async Task IO_TASK(Message message, Payload payload, CancellationToken cancellation, int durationMilliseconds)
        {
            ICallbackProxy callback;
            if (!this._callbacks.TryGetValue(payload.ClientID, out callback))
            {
                return;
            }
            try
            {
                if (payload.RaiseError && payload.TryCount < 2)
                {
                    throw new Exception($"Test Exception TryCount: {payload.TryCount}");
                }
                await Task.Delay(durationMilliseconds, cancellation);
                cancellation.ThrowIfCancellationRequested();
                payload.Result = System.Text.Encoding.UTF8.GetBytes(string.Format("IO_TASK time={0} ms Try: {1}", durationMilliseconds, payload.TryCount));
                // Console.WriteLine($"THREAD: {Thread.CurrentThread.ManagedThreadId}");
                cancellation.ThrowIfCancellationRequested();
                message.Body = this._serializer.Serialize(payload);
                callback.TaskCompleted(message, null);
            }
            catch (OperationCanceledException)
            {
                message.Body = this._serializer.Serialize(payload);
                callback.TaskCompleted(message, "CANCELLED");
            }
            catch (Exception ex)
            {
                message.Body = this._serializer.Serialize(payload);
                callback.TaskCompleted(message, ex.Message);
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

        public void RegisterCallback(Guid clientID, ICallbackProxy callback)
        {
            this._callbacks.AddOrUpdate(clientID, callback, (id, value) => callback);
        }

        public bool UnRegisterCallback(Guid clientID)
        {
            ICallbackProxy res;
            if (this._callbacks.TryRemove(clientID, out res))
            {
                (res as IDisposable).Dispose();
                return true;
            }
            return false;
        }
    }
}
