﻿using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using TasksCoordinator.Test.Interface;

namespace TPLBlocks
{
    public class TaskTransformBlock<TInput, TOutput> : BaseTransformBlock<TInput, TOutput>
    {
        private Task[] _tasks;
        private Channel<TInput> _channel;
        private ChannelWriter<TInput> _messageQueue;
        private volatile int _started = 0;
        private readonly TransformBlockOptions _blockOptions;

        public TaskTransformBlock(Func<TInput, Task<TOutput>> body, TransformBlockOptions blockOptions = null):
            base(body, (blockOptions?? TransformBlockOptions.Default).LoggerFactory, blockOptions?.CancellationToken)
        {
            this._blockOptions = blockOptions ?? TransformBlockOptions.Default;
            if (this._blockOptions.BoundedCapacity == null)
            {
                this._channel = Channel.CreateUnbounded<TInput>(new UnboundedChannelOptions
                {
                    SingleWriter = false,
                    SingleReader = false,
                    AllowSynchronousContinuations = true,
                });
            }
            else
            {
                this._channel = Channel.CreateBounded<TInput>(new BoundedChannelOptions(this._blockOptions.BoundedCapacity.Value)
                {
                    FullMode = BoundedChannelFullMode.Wait,
                    SingleWriter = false,
                    SingleReader = false,
                    AllowSynchronousContinuations = true,
                });
            }
            this._messageQueue = this._channel.Writer;
        }

        private void Start()
        {
            var reader = this._channel.Reader;
            var token = this.GetCancellationToken();
            Task[] tasks = new Task[this._blockOptions.MaxDegreeOfParallelism];
            for (int i = 0; i < this._blockOptions.MaxDegreeOfParallelism; ++i)
            {
                tasks[i] = Task.Run(async () =>
                {
                    try
                    {
                        while (this._started == 1)
                        {
                            var msg = await reader.ReadAsync(token);
                            await (this as IWorkLoad<TInput>).DispatchMessage(msg, 1, token);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                    }
                });
            }
            this._tasks = tasks;
        }

        protected override void OnCompetion()
        {
            Interlocked.CompareExchange(ref _started, 0, 1);
        }

        public override async ValueTask<bool> Post(TInput msg)
        {
            if (!this.IsCompleted)
            {
                var oldStarted = Interlocked.CompareExchange(ref _started, 1, 0);
                if (oldStarted == 0)
                {
                    this.Start();
                }
            }

            await _messageQueue.WriteAsync(msg, this.GetCancellationToken());
            this.UpdateBatchSize(1, false);
            return true;
        }

        protected override void OnDispose()
        {
            Interlocked.CompareExchange(ref _started, 0, 1);
            _messageQueue.TryComplete();
            this._tasks = null;
            base.OnDispose();
        }
    }
}
