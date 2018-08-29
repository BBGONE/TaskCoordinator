using Shared;
using System;
using System.Threading;
using System.Threading.Tasks;
using TasksCoordinator.Test.Interface;

namespace TasksCoordinator.Test
{
    public class CallbackProxy : ICallbackProxy, IDisposable
    {
        public enum JobStatus : int
        {
            Running = 0,
            Success = 1,
            Error = 2,
            Cancelled = 3
        }

        internal static ILog Log = Shared.Log.GetInstance("CallbackProxy");

        private ICallback _callback;
        private CancellationToken _token;
        private CancellationTokenRegistration _register;
        private readonly int _batchSize;
        private volatile int _processedCount;
        private volatile int _status;

        public CallbackProxy(ICallback callback, CancellationToken token)
        {
            this._callback = callback;
            this._batchSize = callback.BatchSize;
            this._token = token;
            this._register = this._token.Register(() => { this.JobCancelled(); });
            this._status = 0;
        }

        void ICallbackProxy.TaskCompleted(Message message, string error)
        {
            if (string.IsNullOrEmpty(error))
            {
                int count = Interlocked.Increment(ref this._processedCount);
                try
                {
                    this.TaskSuccess(message);
                }
                finally
                {
                    if (count == this._batchSize)
                    {
                        this.JobCompleted(null);
                    }
                }
            }
            else if (error == "CANCELLED")
            {
                this.JobCancelled();
            }
            else
            {
                this.TaskError(message, error);
            }
        }

        void TaskSuccess(Message message)
        {
            var oldstatus = Interlocked.CompareExchange(ref this._status, 0, 0);
            if (oldstatus == 0)
            {
                this._callback.TaskSuccess(message);
            }
        }

        void TaskError(Message message, string error)
        {
            var oldstatus = Interlocked.CompareExchange(ref this._status, 0, 0);
            if (oldstatus == 0)
            {
                bool res = this._callback.TaskError(message, error);
                if (!res)
                {
                    this.JobCompleted(error);
                }
            }
        }

        void JobCancelled()
        {
            var oldstatus = Interlocked.CompareExchange(ref this._status, 3, 0);
            if (oldstatus == 0)
            {
                try
                {
                    var task = Task.Factory.StartNew(() =>
                    {
                        try
                        {
                            this._callback.JobCancelled();
                        }
                        catch (Exception ex)
                        {
                            if (!(ex is OperationCanceledException))
                            {
                                Log.Error(ex);
                            }
                        }
                    });
                }
                finally
                {
                    this._register.Dispose();
                }
            }
        }

        void JobCompleted(string error)
        {
            var oldstatus = 0;
            if (string.IsNullOrEmpty(error))
            {
                oldstatus = Interlocked.CompareExchange(ref this._status, 1, 0);
            }
            else
            {
                oldstatus = Interlocked.CompareExchange(ref this._status, 2, 0);
            }

            if (oldstatus == 0)
            {
                try
                {
                    var task = Task.Factory.StartNew(() =>
                    {
                        try
                        {
                            this._callback.JobCompleted(error);
                        }
                        catch (Exception ex)
                        {
                            if (!(ex is OperationCanceledException))
                            {
                                Log.Error(ex);
                            }
                        }
                    });
                }
                finally
                {
                    this._register.Dispose();
                }
            }
        }

        int ICallbackProxy.BatchSize { get { return this._batchSize; } }

        public JobStatus Status { get { return (JobStatus)_status; } }

        #region IDisposable Support
        private bool disposedValue = false;

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    this.JobCancelled();
                }

                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }
        #endregion
    }
}
