using System;
using System.Threading;
using System.Threading.Tasks;
using TasksCoordinator.Interface;

namespace TasksCoordinator.Callbacks
{
    public abstract class BaseCallback<T> : ICallback<T>
    {
        private volatile int _batchSize;
        private volatile int _isComplete;
        private readonly TaskCompletionSource<int> _completeAsyncSource;
        private readonly TaskCompletionSource<int> _resultAsyncSource;
        private readonly object _lock = new object();

        public BaseCallback()
        {
            this._batchSize = 0;
            this._isComplete = 0;
            this._resultAsyncSource = new TaskCompletionSource<int>();
            this._completeAsyncSource = new TaskCompletionSource<int>();
        }
        public BatchInfo BatchInfo
        {
            get
            {
                if (Interlocked.CompareExchange(ref this._isComplete, 1, 1) == 1)
                {
                    // after isComplete = 1 the batch size can not be changed, and so it can be read without locking
                    return new BatchInfo { BatchSize = this._batchSize, IsComplete = true };
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
        public abstract Task<bool> TaskError(T message, string error);
        public virtual void JobCancelled()
        {
            _resultAsyncSource.TrySetCanceled();
        }
        public virtual void JobCompleted(string error)
        {
            if (string.IsNullOrEmpty(error))
            {
                _resultAsyncSource.TrySetResult(this._batchSize);
            }
            else
            {
                _resultAsyncSource.TrySetException(new Exception(error));
            }
        }
        public BatchInfo UpdateBatchSize(int batchSize, bool isComplete)
        {
            lock (this._lock)
            {
                if (this._isComplete == 0)
                {
                    this._batchSize += batchSize;
                    if (isComplete)
                    {
                        this._isComplete = 1;
                        this._completeAsyncSource.SetResult(1);
                    }
                }
                return new BatchInfo { BatchSize = this._batchSize, IsComplete = this._isComplete == 1 };
            }
        }

        public Task ResultAsync
        {
            get { return this._resultAsyncSource.Task; }
        }

        public Task CompleteAsync
        {
            get { return this._completeAsyncSource.Task; }
        }
    }
}
