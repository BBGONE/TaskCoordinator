using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace Shared.TaskCoordinator
{
    public class SemaphoreAsync: IDisposable
    {
        private readonly ConcurrentQueue<TaskCompletionSource<IDisposable>> _readWaitQueue;
        private volatile int _readingCount;
        private readonly int _maxParallelism;
        private readonly CancellationToken _token;
        private readonly CancellationTokenRegistration _tokenRegistration;

        public SemaphoreAsync(int maxParallelism,  CancellationToken token)
        {
            this._maxParallelism = maxParallelism;
            this._token = token;
            this._readingCount = 0;
            this._readWaitQueue = new ConcurrentQueue<TaskCompletionSource<IDisposable>>();
            this._tokenRegistration = this._token.Register(() => { this.Cancel(); });
        }

        private class AsyncReadWait : IDisposable
        {
            private readonly SemaphoreAsync _owner;

            public AsyncReadWait(SemaphoreAsync owner)
            {
                this._owner = owner;
                Interlocked.Increment(ref this._owner._readingCount);
            }

            void IDisposable.Dispose()
            {
                Interlocked.Decrement(ref this._owner._readingCount);
                this._owner._LetNextToEnter();
            }
        }

        private void _LetNextToEnter()
        {
            TaskCompletionSource<IDisposable> temp = null;
            if (this._readWaitQueue.TryPeek(out temp))
            {
                var token = this._token;
                Task.Run(() =>
                {
                    TaskCompletionSource<IDisposable> tcs = null;
                    if (this._readWaitQueue.TryDequeue(out tcs))
                    {
                        if (token.IsCancellationRequested)
                            tcs.SetCanceled();
                        else
                            tcs.SetResult(new AsyncReadWait(this));
                    }
                }, token);
            }
        }

        public Task<IDisposable> WaitEnterAsync()
        {
            if (this._maxParallelism > this._readingCount)
            {
                return Task.FromResult<IDisposable>(new AsyncReadWait(this));
            }
            else
            {
                TaskCompletionSource<IDisposable> tcs = new TaskCompletionSource<IDisposable>();
                this._readWaitQueue.Enqueue(tcs);
                return tcs.Task;
            }
        }

        public void Cancel()
        {
            TaskCompletionSource<IDisposable> temp = null;
            while (this._readWaitQueue.TryDequeue(out temp))
            {
                var tcs = temp;
                Task.Run(() =>
                {
                    try
                    {
                        tcs.SetCanceled();
                    }
                    catch
                    {

                    }
                });
            }
        }

        public void Dispose()
        {
            this.Cancel();
            this._tokenRegistration.Dispose();
        }
    }
}
