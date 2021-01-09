using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;
using TasksCoordinator.Callbacks;
using TasksCoordinator.Interface;
using TasksCoordinator.Test.Interface;

namespace TPLBlocks
{
    public abstract class BaseTransformBlock<TInput, TOutput> : IWorkLoad<TInput>, IDisposable, ITransformBlock<TInput, TOutput>
    {
        private ICallbackProxy<TInput> _callbackProxy;
        private bool _isDisposed = false;
        private readonly ILoggerFactory _loggerFactory;
        private readonly TCallBack<TInput> _callBack;
        private Func<TInput, Task<TOutput>> _body;
        private Func<TOutput, Task> _outputSink;
        private Guid _id = new Guid();
        private Object _SyncLock = new Object();

        public BaseTransformBlock(Func<TInput, Task<TOutput>> body, ILoggerFactory loggerFactory)
        {
            _body = body;
            _outputSink = null;
            _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
            _callBack = new TCallBack<TInput>(_loggerFactory.CreateLogger(this.GetType().Name));
            _callbackProxy = null;
        }

        public abstract ValueTask<bool> Post(TInput msg);
        
        protected abstract CancellationToken GetCancellationToken();

        protected long UpdateBatchSize(long addValue, bool isComplete)
        {
            return this._callBack.UpdateBatchSize(addValue, isComplete);
        }

        protected ICallbackProxy<TInput> CallbackProxy
        {
            get
            {
                if (!_isDisposed)
                {
                    if (_callbackProxy == null)
                    {
                        LazyInitializer.EnsureInitialized(ref _callbackProxy, ref _SyncLock, () => new CallbackProxy<TInput>(_callBack, _loggerFactory, this.GetCancellationToken()));
                    }
                }
                else
                {
                    throw new ObjectDisposedException(this.GetType().Name);
                }

                return _callbackProxy;
            }
        }

        public Task Completion
        {
            get
            {
                return _callBack.ResultAsync;
            }
        }

        public event Func<TOutput, Task> OutputSink { add => _outputSink+= value; remove => _outputSink -= value; }

        public BatchInfo BatchInfo { get => _callBack.BatchInfo; }
        public Guid Id { get => _id; set => _id = value; }

        public long Complete(Exception exception = null)
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException(this.GetType().Name);
            }

            if (exception == null)
            {
                return this.UpdateBatchSize(0, true);
            }
            else
            {
                if (exception is AggregateException aggex)
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
                        CallbackProxy.JobCompleted(firstError);
                    }
                    else
                    {
                        CallbackProxy.JobCancelled();
                    }
                }
                else if (exception is OperationCanceledException)
                {
                    CallbackProxy.JobCancelled();
                }
                else
                {
                    CallbackProxy.JobCompleted(exception);
                }

                return this.UpdateBatchSize(0, true);
            }
        }

        protected virtual void OnDispose()
        {
        }

        public void Dispose()
        {
            if (!_isDisposed)
            {
                this.OnDispose();

                _outputSink = null;
                var oldCallbackProxy = Interlocked.Exchange(ref _callbackProxy, null);
                if (oldCallbackProxy != null)
                {
                    (oldCallbackProxy as IDisposable)?.Dispose();
                }

                _isDisposed = true;
            }
        }

        async Task<bool> IWorkLoad<TInput>.DispatchMessage(TInput message, long taskId, CancellationToken token)
        {
            try
            {
                var output = await _body(message);

                var sinkDelegates = _outputSink?.GetInvocationList();
                if (sinkDelegates != null)
                {
                    foreach (var sinkDelegate in sinkDelegates)
                    {
                        await ((Func<TOutput, Task>)sinkDelegate)(output);
                    }
                }

                await CallbackProxy.TaskCompleted(message, null);
            }
            catch (Exception ex)
            {
                await CallbackProxy.TaskCompleted(message, ex);
            }
            return false;
        }
    }
}
