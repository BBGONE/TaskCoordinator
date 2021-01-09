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
        private volatile int _isDisposed = 0;
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
            _loggerFactory = loggerFactory;
            _callBack = new TCallBack<TInput>();
            _callbackProxy = null;
        }

        protected virtual ICallbackProxy<TInput> CreateCallbackProxy(CancellationToken? token= null)
        {
            return new CallbackProxy<TInput>(_callBack, _loggerFactory, token);
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
                if (_isDisposed == 0)
                {
                    if (_callbackProxy == null)
                    {
                        LazyInitializer.EnsureInitialized(ref _callbackProxy, ref _SyncLock, () => CreateCallbackProxy(this.GetCancellationToken()));
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

        public long Complete()
        {
            return this.UpdateBatchSize(0, true);
        }

        protected virtual void OnDispose()
        {
        }

        public void Dispose()
        {
            var old = Interlocked.CompareExchange(ref _isDisposed, 1, 0);
            if (old == 0)
            {
                this.OnDispose();

                _outputSink = null;
                var oldCallbackProxy = Interlocked.Exchange(ref _callbackProxy, null);
                if (oldCallbackProxy != null)
                {
                    (oldCallbackProxy as IDisposable)?.Dispose();
                }
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
            catch (OperationCanceledException)
            {
                await CallbackProxy.TaskCompleted(message, "CANCELLED");
            }
            catch (Exception ex)
            {
                await CallbackProxy.TaskCompleted(message, ex.Message);
            }
            return false;
        }
    }
}
