using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Schedulers;
using TasksCoordinator.Callbacks;
using TasksCoordinator.Interface;
using TasksCoordinator.Test.Interface;

namespace TasksCoordinator.Test
{
    public class TestWorkLoad : IWorkLoad<Payload>, IDisposable
    {
        private readonly ConcurrentDictionary<Guid, ICallbackProxy<Payload>> _callbacks;
        private readonly TaskScheduler _longRunningTasksScheduler;
        private readonly ILoggerFactory _loggerFactory;

        public TestWorkLoad(ILoggerFactory loggerFactory)
        {
            this._loggerFactory = loggerFactory;
            this._callbacks = new ConcurrentDictionary<Guid, ICallbackProxy<Payload>>();
            this._longRunningTasksScheduler= new WorkStealingTaskScheduler();
        }

        public async Task<bool> DispatchMessage(Payload message, long taskId, CancellationToken token)
        {
            // возвратить ли сообщение назад в очередь?
            bool rollBack = false;
            message.TryCount += 1;
            TaskWorkType workType = message.WorkType;
            switch (workType)
            {
                case TaskWorkType.LongCPUBound:
                    await RUN_LONG_RUN_SYNC_ACTION(() =>
                    {
                        LONG_RUN_CPU_TASK(message, token, taskId);
                    }, taskId, message, token);
                    break;
                case TaskWorkType.UltraShortCPUBound:
                    await RUN_ASYNC_ACTION(async () =>
                    {
                        await CPU_TASK(message, token, 0, taskId);
                    }, taskId, message, token);
                    break;
                case TaskWorkType.ShortCPUBound:
                    await RUN_ASYNC_ACTION(async () =>
                    {
                        await CPU_TASK(message, token, 100, taskId);
                    }, taskId, message, token);
                    break;
                default:
                    throw new InvalidOperationException($"Unknown WorkType {workType}");
            }
            return rollBack;
        }

        public void RegisterCallback(Guid clientID, ICallback<Payload> callback, CancellationToken? token = null)
        {
            var proxyCallback = new CallbackProxy<Payload>(callback, _loggerFactory, token);
            this._callbacks.TryAdd(clientID, proxyCallback);
        }

        public bool UnRegisterCallback(Guid clientID)
        {
            if (this._callbacks.TryRemove(clientID, out var res))
            {
                (res as IDisposable)?.Dispose();
                return true;
            }
            return false;
        }

        #region TEST TASKS
        // Sync TASK CPU Bound
        private void LONG_RUN_CPU_TASK(Payload message, CancellationToken token, long taskId)
        {
            int iterations = 1000000;
            int cnt = iterations;
            for (int i = 0; i < cnt; ++i)
            {
                token.ThrowIfCancellationRequested();
                //rollBack = !rollBack;
                //Do some CPU work 
                message.Result = System.Text.Encoding.UTF8.GetBytes(string.Format("qwertyuiop[;lkjhngbfd--cnt={0}", cnt));
            }
            if (message.RaiseError && message.TryCount < 2)
            {
                throw new Exception($"Test Exception TryCount: {message.TryCount}");
            }
            token.ThrowIfCancellationRequested();
            message.Result = System.Text.Encoding.UTF8.GetBytes(string.Format("LONG_RUN_CPU_TASK cnt={0} Try: {1}", cnt, message.TryCount));
            token.ThrowIfCancellationRequested();
        }

        // Async Task CPU Bound
        private async Task CPU_TASK(Payload message, CancellationToken token, int iterations, long taskId)
        {
            await Task.FromResult(0);
            int cnt = iterations;
            for (int i = 0; i < cnt; ++i)
            {
                token.ThrowIfCancellationRequested();
                message.Result = System.Text.Encoding.UTF8.GetBytes(string.Format("qwertyuiop[;lkjhngbfd--cnt={0}", cnt));
            }
            if (message.RaiseError && message.TryCount < 2)
            {
                throw new Exception($"Test Exception TryCount: {message.TryCount}");
            }
            token.ThrowIfCancellationRequested();
            message.Result = System.Text.Encoding.UTF8.GetBytes(string.Format("CPU_TASK cnt={0} Try: {1}", cnt, message.TryCount));
            token.ThrowIfCancellationRequested();
        }

        // Async Task IO Bound
        private async Task IO_TASK(Payload message, CancellationToken token, int durationMilliseconds)
        {
            if (message.RaiseError && message.TryCount < 2)
            {
                throw new Exception($"Test Exception TryCount: {message.TryCount}");
            }
            await Task.Delay(durationMilliseconds, token);
            token.ThrowIfCancellationRequested();

            message.Result = System.Text.Encoding.UTF8.GetBytes(string.Format("IO_TASK time={0} ms Try: {1}", durationMilliseconds, message.TryCount));
            // Console.WriteLine($"THREAD: {Thread.CurrentThread.ManagedThreadId}");
            token.ThrowIfCancellationRequested();
        }
        #endregion


        #region HELPER FUNCTIONS
        private async Task RUN_LONG_RUN_SYNC_ACTION(Action action, long taskId, Payload message, CancellationToken token)
        {
            await Task.Factory.StartNew(async () =>
            {
                ICallbackProxy<Payload> callback;
                if (!this._callbacks.TryGetValue(message.ClientID, out callback))
                {
                    return;
                }
                try
                {
                    action();
                    await callback.TaskCompleted(message, null);
                }
                catch (Exception ex)
                {
                    await callback.TaskCompleted(message, ex);
                }

            }, token, TaskCreationOptions.DenyChildAttach, this._longRunningTasksScheduler).Unwrap();
        }

        private async Task RUN_ASYNC_ACTION(Func<Task> action, long taskId, Payload message, CancellationToken token)
        {
            ICallbackProxy<Payload> callback;
            if (!this._callbacks.TryGetValue(message.ClientID, out callback))
            {
                return;
            }
            try
            {
                // Console.WriteLine($"Thread: {Thread.CurrentThread.ManagedThreadId} Task: {taskId}");
                await action();
                await callback.TaskCompleted(message, null);
            }
            catch (Exception ex)
            {
                await callback.TaskCompleted(message, ex);
            }
        }

        public void Dispose()
        {
            (this._longRunningTasksScheduler as IDisposable)?.Dispose();
        }
        #endregion

    }
}
