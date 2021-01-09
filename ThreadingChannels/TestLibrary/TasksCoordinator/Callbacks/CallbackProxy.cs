using Microsoft.Extensions.Logging;
using Shared.Errors;
using System;
using System.Threading;
using System.Threading.Tasks;
using TasksCoordinator.Interface;

namespace TasksCoordinator.Callbacks
{
    public class CallbackProxy<T> : ICallbackProxy<T>, IDisposable
    {
        public enum JobStatus : int
        {
            Running = 0,
            Success = 1,
            Error = 2,
            Cancelled = 3
        }

        private readonly ILogger _logger;
        private ICallback<T> _callback;
        private CancellationToken _token;
        private CancellationTokenRegistration _register;
        private volatile int _processedCount;
        private volatile int _status;

        public CallbackProxy(ICallback<T> callback, ILoggerFactory loggerFactory, CancellationToken? token = null)
        {
            this._callback = callback;
            this._token = token ?? CancellationToken.None;
            this._logger = loggerFactory.CreateLogger<CallbackProxy<T>>();

            this._register = this._token.Register(() => {
                try
                {
                    ((ICallbackProxy<T>)this).JobCancelled();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ErrorHelper.GetFullMessage(ex));
                }
            }, false);

            this._callback.CompleteAsync.ContinueWith((t) => {
                int oldstatus = this._status;
                if (oldstatus == 0)
                {
                    var batchInfo = this._callback.BatchInfo;
                    if (batchInfo.IsComplete && this._processedCount == batchInfo.BatchSize && !this._token.IsCancellationRequested)
                    {
                        ((ICallbackProxy<T>)this).JobCompleted(null);
                    }
                }
            }, TaskContinuationOptions.ExecuteSynchronously);
            this._status = 0;
        }

        async Task ICallbackProxy<T>.TaskCompleted(T message, Exception error)
        {
            if (error == null)
            {
                this.TaskSuccess(message);
                int count = Interlocked.Increment(ref this._processedCount);
                var batchInfo = this._callback.BatchInfo;
                if (batchInfo.IsComplete && count == batchInfo.BatchSize)
                {
                    ((ICallbackProxy<T>)this).JobCompleted(null);
                }
            }
            else if (error is AggregateException aggex)
            {
                Exception firstError = null;

                aggex.Flatten().Handle((err) => {
                    if (err is OperationCanceledException)
                    {
                        return true;
                    }
                    firstError = firstError ?? err;
                    return true;
                });

                if (firstError != null)
                {
                    await this.TaskError(message, error);
                }
                else
                {
                    ((ICallbackProxy<T>)this).JobCancelled();
                }
            }
            else if (error is OperationCanceledException)
            {
                ((ICallbackProxy<T>)this).JobCancelled();
            }
            else
            {
                await this.TaskError(message, error);
            }
        }

        void TaskSuccess(T message)
        {
            var oldstatus =  this._status;
            if ((JobStatus)oldstatus == JobStatus.Running)
            {
                this._callback.TaskSuccess(message);
            }
        }

        async Task TaskError(T message, Exception error)
        {
            var oldstatus = this._status;
            if ((JobStatus)oldstatus == JobStatus.Running)
            {
                bool res = await this._callback.TaskError(message, error);
                if (!res)
                {
                    ((ICallbackProxy<T>)this).JobCompleted(error);
                }
            }
        }

        void ICallbackProxy<T>.JobCancelled()
        {
            var oldstatus = Interlocked.CompareExchange(ref this._status, (int)JobStatus.Cancelled, 0);
            if ((JobStatus)oldstatus == JobStatus.Running)
            {
                try
                {
                    var task = Task.Run(() =>
                    {
                        try
                        {
                            this._callback.JobCancelled();
                        }
                        catch (Exception ex)
                        {
                            if (!(ex is OperationCanceledException))
                            {
                                _logger.LogError(ErrorHelper.GetFullMessage(ex));
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

        void ICallbackProxy<T>.JobCompleted(Exception error)
        {
            var oldstatus = 0;
            if (error == null)
            {
                oldstatus = Interlocked.CompareExchange(ref this._status, (int)JobStatus.Success, 0);
            }
            else
            {
                oldstatus = Interlocked.CompareExchange(ref this._status, (int)JobStatus.Error, 0);
            }

            if ((JobStatus)oldstatus == JobStatus.Running)
            {
                try
                {
                    var task = Task.Run(() =>
                    {
                        try
                        {
                            this._callback.JobCompleted(error);
                        }
                        catch (Exception ex)
                        {
                            if (!(ex is OperationCanceledException))
                            {
                                _logger.LogError(ErrorHelper.GetFullMessage(ex));
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

        BatchInfo ICallbackProxy<T>.BatchInfo { get { return this._callback.BatchInfo; } }
        public JobStatus Status { get { return (JobStatus)_status; } }

        #region IDisposable Support
        private bool _disposed = false;

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    ((ICallbackProxy<T>)this).JobCancelled();
                }

                _disposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }
        #endregion
    }
}
