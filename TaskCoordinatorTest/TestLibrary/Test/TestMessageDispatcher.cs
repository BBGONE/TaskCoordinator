using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using TasksCoordinator.Interface;
using TasksCoordinator.Test.Interface;

namespace TasksCoordinator.Test
{
    public class TestMessageDispatcher: IMessageDispatcher<Message>
    {
        private readonly ISerializer _serializer;
        private readonly ConcurrentDictionary<Guid, ICallbackProxy<Message>> _callbacks;

        public TestMessageDispatcher(ISerializer serializer)
        {
            this._serializer = serializer;
            this._callbacks = new ConcurrentDictionary<Guid, ICallbackProxy<Message>>();
        }

        private async Task<bool> _DispatchMessage(Message message, WorkContext context)
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
                    await CPU_TASK(message, payload, cancellation, 5000000, context.taskId).ConfigureAwait(false);
                    break;
                case TaskWorkType.LongIOBound:
                    await IO_TASK(message, payload, cancellation, 5000).ConfigureAwait(false);
                    break;
                case TaskWorkType.ShortCPUBound:
                    await CPU_TASK(message, payload, cancellation, 100000, context.taskId).ConfigureAwait(false);
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
            ICallbackProxy<Message> callback;
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
                await callback.TaskCompleted(message, null);
            }
            catch (OperationCanceledException)
            {
                message.Body = this._serializer.Serialize(payload);
                await callback.TaskCompleted(message, "CANCELLED");
            }
            catch (Exception ex)
            {
                message.Body = this._serializer.Serialize(payload);
                await callback.TaskCompleted(message, ex.Message);
            }
        }

        // Test Task IO Bound
        private async Task IO_TASK(Message message, Payload payload, CancellationToken cancellation, int durationMilliseconds)
        {
            ICallbackProxy<Message> callback;
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
                await callback.TaskCompleted(message, null);
            }
            catch (OperationCanceledException)
            {
                message.Body = this._serializer.Serialize(payload);
                await callback.TaskCompleted(message, "CANCELLED");
            }
            catch (Exception ex)
            {
                message.Body = this._serializer.Serialize(payload);
                await callback.TaskCompleted(message, ex.Message);
            }
        }

        async Task<MessageProcessingResult> IMessageDispatcher<Message>.DispatchMessage(Message message, 
            WorkContext context)
        {
            bool rollBack = false;
            rollBack = await this._DispatchMessage(message, context).ConfigureAwait(false);
            return new MessageProcessingResult() { isRollBack = rollBack };
        }

        public void RegisterCallback(Guid clientID, ICallbackProxy<Message> callback)
        {
            this._callbacks.AddOrUpdate(clientID, callback, (id, value) => callback);
        }

        public bool UnRegisterCallback(Guid clientID)
        {
            ICallbackProxy<Message> res;
            if (this._callbacks.TryRemove(clientID, out res))
            {
                (res as IDisposable).Dispose();
                return true;
            }
            return false;
        }
    }
}
