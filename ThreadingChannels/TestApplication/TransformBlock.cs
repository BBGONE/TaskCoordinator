using Microsoft.Extensions.Logging;
using Shared;
using System;
using System.Threading;
using System.Threading.Tasks;
using TasksCoordinator.Callbacks;
using TasksCoordinator.Interface;
using TasksCoordinator.Test;
using TasksCoordinator.Test.Interface;

namespace TestApplication
{
    class TCallBack<TMsg> : BaseCallback<TMsg>
    {
        private volatile int _ProcessedCount;
        private volatile int _ErrorCount;
        
        public int ProcessedCount { get => this._ProcessedCount; }
        public int ErrorCount { get => _ErrorCount; }

        public TCallBack()
        {
        }

        public override void TaskSuccess(TMsg message)
        {
            Interlocked.Increment(ref _ProcessedCount);
        }
        public override async Task<bool> TaskError(TMsg message, string error)
        {
            await Task.FromResult(0);
            Interlocked.Increment(ref _ErrorCount);
            return false;
        }
    }

    public class TransformBlock<TInput, TOutput> : IWorkLoad<TInput>, IDisposable
    {
        private ICallbackProxy<TInput> _callbackProxy;
        private readonly MessageService<TInput> _svc;
        private readonly ILoggerFactory _loggerFactory;
        private readonly TCallBack<TInput> _callBack;
        private Func<TInput, Task<TOutput>> _body;
        private Func<TOutput, Task> _outputSink;
        private Guid _id = new Guid();
        private TransformBlockOptions _blockOptions;

        public TransformBlock(Func<TInput, Task<TOutput>> body, TransformBlockOptions blockOptions= null)
        {
            _body = body;
            _blockOptions = blockOptions ?? TransformBlockOptions.Default;
            _outputSink = null;
            _callbackProxy = null;
            _loggerFactory = _blockOptions.LoggerFactory;
            _svc = new MessageService<TInput>(_id.ToString(), this, _loggerFactory, _blockOptions.MaxDegreeOfParallelism, _blockOptions.MaxDegreeOfParallelism, _blockOptions.QueueCapacity);
            _svc.Start();
            _callBack = new TCallBack<TInput>();
            _callbackProxy = new CallbackProxy<TInput>(_callBack, _loggerFactory, _svc.TasksCoordinator.Token);
        }

        public async ValueTask<bool> Post(TInput msg)
        {
            bool res = await _svc.Post(msg, _svc.TasksCoordinator.Token);
            if (res)
            {
                _callBack.UpdateBatchSize(1, false);
            }
            return res;
        }

        public Task Completion
        {
            get
            {
                return _callBack.ResultAsync;
            }
        }

        public Func<TOutput, Task> OutputSink { get => _outputSink; set => _outputSink = value; }

        public BatchInfo BatchInfo { get => _callBack.BatchInfo; }

        public void Complete()
        {
            _callBack.UpdateBatchSize(0, true);
        }

        public void Dispose()
        {
            (_callbackProxy as IDisposable)?.Dispose();
            _callbackProxy = null;
            _svc.Stop();
        }

        async Task<bool> IWorkLoad<TInput>.DispatchMessage(TInput message, long taskId, CancellationToken token)
        {
            try
            {
                var output = await _body(message);
                var outputTask = _outputSink?.Invoke(output);
                if (outputTask != null)
                {
                    await outputTask;
                }
                await _callbackProxy.TaskCompleted(message, null);
            }
            catch (OperationCanceledException)
            {
                await _callbackProxy.TaskCompleted(message, "CANCELLED");
            }
            catch (Exception ex)
            {
                await _callbackProxy.TaskCompleted(message, ex.Message);
            }
            return false;
        }
    }
}
