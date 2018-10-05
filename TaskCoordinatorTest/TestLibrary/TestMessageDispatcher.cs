using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using TasksCoordinator.Interface;
using TasksCoordinator.Test.Interface;

namespace TasksCoordinator.Test
{
    public class TestMessageDispatcher: IMessageDispatcher<Message, object>
    {
        private readonly ISerializer _serializer;
        private readonly ConcurrentDictionary<Guid, ICallbackProxy<Message>> _callbacks;
        private readonly TaskScheduler _longRunningTasksScheduler;


        public TestMessageDispatcher(ISerializer serializer, TaskScheduler longRunningTasksScheduler)
        {
            this._serializer = serializer ?? throw new ArgumentException(nameof(serializer));
            this._longRunningTasksScheduler = longRunningTasksScheduler ?? throw new ArgumentException(nameof(longRunningTasksScheduler));
            this._callbacks = new ConcurrentDictionary<Guid, ICallbackProxy<Message>>();
        }

        private async Task<bool> _DispatchMessage(Message message, long taskId, CancellationToken token)
        {
            // возвратить ли сообщение назад в очередь?
            bool rollBack = false;
            Payload payload = this._serializer.Deserialize<Payload>(message.Body);
            payload.TryCount += 1;
            TaskWorkType workType = payload.WorkType;
            switch (workType)
            {
                case TaskWorkType.LongCPUBound:
                    await RUN_LONG_RUN_SYNC_ACTION(() =>
                    {
                        LONG_RUN_CPU_TASK(message, payload, token, taskId);
                    }, taskId, message, payload, token);
                    break;
                case TaskWorkType.LongIOBound:
                    await RUN_ASYNC_ACTION(async ()=>
                    {
                        await IO_TASK(message, payload, token, 5000).ConfigureAwait(false);
                    }, taskId, message, payload, token);
                    break;
                case TaskWorkType.ShortCPUBound:
                    await RUN_ASYNC_ACTION(async () =>
                    {
                        await CPU_TASK(message, payload, token, 100000, taskId).ConfigureAwait(false);
                    }, taskId, message, payload, token);
                    break;
                case TaskWorkType.ShortIOBound:
                    await RUN_ASYNC_ACTION(async () =>
                    {
                        await IO_TASK(message, payload, token, 500).ConfigureAwait(false);
                    }, taskId, message, payload, token);
                    break;
                case TaskWorkType.UltraShortCPUBound:
                    await RUN_ASYNC_ACTION(async () =>
                    {
                        await CPU_TASK(message, payload, token, 5, taskId).ConfigureAwait(false);
                    }, taskId, message, payload, token);
                    break;
                case TaskWorkType.UltraShortIOBound:
                    await RUN_ASYNC_ACTION(async () =>
                    {
                        await IO_TASK(message, payload, token, 10).ConfigureAwait(false);
                    }, taskId, message, payload, token);
                    break;
                case TaskWorkType.Random:
                    throw new InvalidOperationException("Random WorkType is not Supported");
                default:
                    throw new InvalidOperationException($"Unknown WorkType {workType}");
            }
            return rollBack;
        }

        #region HELPER FUNCTIONS
        private async Task RUN_LONG_RUN_SYNC_ACTION(Action action, long taskId, Message message, Payload payload , CancellationToken token) {
            await Task.Factory.StartNew(async () => {
                ICallbackProxy<Message> callback;
                if (!this._callbacks.TryGetValue(payload.ClientID, out callback))
                {
                    return;
                }
                try
                {
                    action();
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
            }, token, TaskCreationOptions.DenyChildAttach, this._longRunningTasksScheduler).Unwrap();
        }

        private async Task RUN_ASYNC_ACTION(Func<Task> action, long taskId, Message message, Payload payload, CancellationToken token)
        {
            ICallbackProxy<Message> callback;
            if (!this._callbacks.TryGetValue(payload.ClientID, out callback))
            {
                return;
            }
            try
            {
                // Console.WriteLine($"Thread: {Thread.CurrentThread.ManagedThreadId} Task: {taskId}");
                await action();
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
        #endregion

        #region TEST TASKS
        // Sync TASK CPU Bound
        private void LONG_RUN_CPU_TASK(Message message, Payload payload, CancellationToken token, long taskId)
        {
            int iterations = 10000000;
            int cnt = iterations;
            for (int i = 0; i < cnt; ++i)
            {
                token.ThrowIfCancellationRequested();
                //rollBack = !rollBack;
                //Do some CPU work 
                payload.Result = System.Text.Encoding.UTF8.GetBytes(string.Format("qwertyuiop[;lkjhngbfd--cnt={0}", cnt));
            }
            if (payload.RaiseError && payload.TryCount < 2)
            {
                throw new Exception($"Test Exception TryCount: {payload.TryCount}");
            }
            token.ThrowIfCancellationRequested();
            payload.Result = System.Text.Encoding.UTF8.GetBytes(string.Format("LONG_RUN_CPU_TASK cnt={0} Try: {1}", cnt, payload.TryCount));
            token.ThrowIfCancellationRequested();
            message.Body = this._serializer.Serialize(payload);
            token.ThrowIfCancellationRequested();
        }

        // Async Task CPU Bound
        private async Task CPU_TASK(Message message, Payload payload, CancellationToken token, int iterations, long taskId)
        {
            await Task.FromResult(0);
            int cnt = iterations;
            for (int i = 0; i < cnt; ++i)
            {
                token.ThrowIfCancellationRequested();
                payload.Result = System.Text.Encoding.UTF8.GetBytes(string.Format("qwertyuiop[;lkjhngbfd--cnt={0}", cnt));
            }
            if (payload.RaiseError && payload.TryCount < 2)
            {
                throw new Exception($"Test Exception TryCount: {payload.TryCount}");
            }
            token.ThrowIfCancellationRequested();
            payload.Result = System.Text.Encoding.UTF8.GetBytes(string.Format("CPU_TASK cnt={0} Try: {1}", cnt, payload.TryCount));
            token.ThrowIfCancellationRequested();
            message.Body = this._serializer.Serialize(payload);
        }

        // Async Task IO Bound
        private async Task IO_TASK(Message message, Payload payload, CancellationToken token, int durationMilliseconds)
        {
            if (payload.RaiseError && payload.TryCount < 2)
            {
                throw new Exception($"Test Exception TryCount: {payload.TryCount}");
            }
            await Task.Delay(durationMilliseconds, token);
            token.ThrowIfCancellationRequested();

            payload.Result = System.Text.Encoding.UTF8.GetBytes(string.Format("IO_TASK time={0} ms Try: {1}", durationMilliseconds, payload.TryCount));
            // Console.WriteLine($"THREAD: {Thread.CurrentThread.ManagedThreadId}");
            token.ThrowIfCancellationRequested();
            message.Body = this._serializer.Serialize(payload);
        }
        #endregion

        async Task<MessageProcessingResult> IMessageDispatcher<Message, object>.DispatchMessage(Message message, long taskId, CancellationToken token, object state)
        {
            bool rollBack = false;
            rollBack = await this._DispatchMessage(message, taskId, token).ConfigureAwait(false);
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
