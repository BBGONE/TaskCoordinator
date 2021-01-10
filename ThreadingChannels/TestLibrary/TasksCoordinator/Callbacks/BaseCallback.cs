using System;
using System.Threading;
using System.Threading.Tasks;
using TasksCoordinator.Interface;

namespace TasksCoordinator.Callbacks
{
    public abstract class BaseCallback<T> : ICallback<T>
    {
        private long _batchSize;
        private volatile int _isComplete;
        private readonly TaskCompletionSource<long> _completeAsyncSource;
        private readonly TaskCompletionSource<long> _resultAsyncSource;
        private readonly object _lock = new object();

        public BaseCallback()
        {
            this._resultAsyncSource = new TaskCompletionSource<long>();
            this._completeAsyncSource = new TaskCompletionSource<long>();
        }
        public BatchInfo BatchInfo
        {
            get
            {
                if (Interlocked.CompareExchange(ref this._isComplete, 1, 1) == 1)
                {
                    // after isComplete = 1 the batch size can not be changed, and so it can be read without locking
                    return new BatchInfo { BatchSize = Interlocked.Read(ref this._batchSize), IsComplete = true };
                }
                else
                {
                    lock (this._lock)
                    {
                        return new BatchInfo { BatchSize = this._batchSize, IsComplete = this._isComplete == 1 };
                    }
                }
            }
        }

        public abstract void TaskSuccess(T message);
        public abstract Task<bool> TaskError(T message, Exception error);
        public virtual Task JobCancelled()
        {
            _resultAsyncSource.TrySetCanceled();
            return Task.CompletedTask;
        }
        public virtual Task JobCompleted(Exception error)
        {
            if (error == null)
            {
                _resultAsyncSource.TrySetResult(this._batchSize);
            }
            else
            {
                _resultAsyncSource.TrySetException(error);
            }
            return Task.CompletedTask;
        }

        public long UpdateBatchSize(long addValue, bool isComplete)
        {
            lock (this._lock)
            {
                if (this._isComplete == 0)
                {
                    this._batchSize += addValue;
                    if (isComplete)
                    {
                        this._isComplete = 1;
                        this._completeAsyncSource.SetResult(1);
                    }
                }
                
                return Interlocked.Read(ref this._batchSize);
            }
        }

        public Task ResultAsync => this._resultAsyncSource.Task; 

        public Task CompleteAsync => this._completeAsyncSource.Task;

        public long BatchSize => Interlocked.Read(ref this._batchSize);
    }
}
