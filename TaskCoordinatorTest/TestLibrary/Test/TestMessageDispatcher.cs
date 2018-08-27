using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
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
            // возвратить ли сообщение назад в очередь?
            bool rollBack = false;
            CancellationToken cancellation = context.Cancellation;
            Payload payload = this._serializer.Deserialize<Payload>(message.Body);
            payload.TryCount += 1;
            TaskWorkType workType = payload.WorkType;
            switch (workType)
            {
                case TaskWorkType.LongCPUBound:
                    Task task = new Task(async () =>
                    {
                        try
                        {
                            await CPU_TASK(message, payload, cancellation, 10000000, context.taskId).ConfigureAwait(false);
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
            ICallback callback;
            if (!this._callbacks.TryGetValue(payload.ClientID, out callback))
            {
                return;
            }
            Stopwatch stopwatch = new Stopwatch();
            try
            {
                stopwatch.Start();
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
                if (message.SequenceNumber % 20 == 0 && payload.TryCount < 2)
                {
                    throw new Exception($"Test Exception TryCount: {payload.TryCount}");
                }
                cancellation.ThrowIfCancellationRequested();
                stopwatch.Stop();
                payload.Result = System.Text.Encoding.UTF8.GetBytes(string.Format("CPU_TASK TimeElapsed={0} ticks---cnt={1}  TryCount: {2}", stopwatch.ElapsedTicks, cnt, payload.TryCount));
                 cancellation.ThrowIfCancellationRequested();
                message.Body = this._serializer.Serialize(payload);
                callback.PostTaskCompleted(message, null);
            }
            catch (OperationCanceledException)
            {
                message.Body = this._serializer.Serialize(payload);
                callback.PostTaskCompleted(message, "CANCELLED");
            }
            catch (Exception ex)
            {
                message.Body = this._serializer.Serialize(payload);
                callback.PostTaskCompleted(message, ex.Message);
            }
            finally
            {
                if (stopwatch.IsRunning)
                    stopwatch.Stop();
            }
        }

        // Test Task IO Bound
        private async Task IO_TASK(Message message, Payload payload, CancellationToken cancellation, int durationMilliseconds)
        {
            Stopwatch stopwatch = new Stopwatch();
            try
            {
                stopwatch.Start();
                await Task.Delay(durationMilliseconds, cancellation);
                cancellation.ThrowIfCancellationRequested();
                stopwatch.Stop();
                payload.Result = System.Text.Encoding.UTF8.GetBytes(string.Format("IO_TASK TimeElapsed={0} ticks--- durationMilliseconds={1}", stopwatch.ElapsedTicks, durationMilliseconds));
                // Console.WriteLine($"THREAD: {Thread.CurrentThread.ManagedThreadId}");
                ICallback callback;
                if (this._callbacks.TryGetValue(payload.ClientID, out callback))
                {
                    cancellation.ThrowIfCancellationRequested();
                    message.Body = this._serializer.Serialize(payload);
                    callback.PostTaskCompleted(message, null);
                }
            }
            catch (OperationCanceledException)
            {
                if (stopwatch.IsRunning)
                {
                    stopwatch.Stop();
                }
                //NOOP
            }
            catch (Exception ex)
            {
                if (stopwatch.IsRunning)
                {
                    stopwatch.Stop();
                }
                ICallback callback;
                if (this._callbacks.TryGetValue(payload.ClientID, out callback))
                {
                    message.Body = this._serializer.Serialize(payload);
                    callback.PostTaskCompleted(message, ex.Message);
                }
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
