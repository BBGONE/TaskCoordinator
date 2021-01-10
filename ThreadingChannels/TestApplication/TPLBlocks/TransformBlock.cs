﻿using System;
using System.Threading;
using System.Threading.Tasks;
using TasksCoordinator.Test;

namespace TPLBlocks
{
    public class TransformBlock<TInput, TOutput> : BaseTransformBlock<TInput, TOutput>
    {
        private readonly MessageService<TInput> _svc;
        private volatile int _started = 0;
        private volatile int _completed = 0;

        public TransformBlock(Func<TInput, Task<TOutput>> body, TransformBlockOptions blockOptions = null):
            base(body, (blockOptions?? TransformBlockOptions.Default).LoggerFactory, blockOptions?.CancellationToken)
        {
            blockOptions = blockOptions ?? TransformBlockOptions.Default;
            _svc = new MessageService<TInput>(this.Id.ToString(), this, blockOptions.LoggerFactory, blockOptions.MaxDegreeOfParallelism, blockOptions.MaxDegreeOfParallelism, blockOptions.BoundedCapacity);
        }

        public override async ValueTask<bool> Post(TInput msg)
        {
            if (_completed == 0)
            {
                var oldStarted = Interlocked.CompareExchange(ref _started, 1, 0);
                if (oldStarted == 0)
                {
                    _svc.Start();
                }
            }

            this.GetCancellationToken().ThrowIfCancellationRequested();
            bool res = await _svc.Post(msg);
            if (res)
            {
                this.UpdateBatchSize(1, false);
            }
            return res;
        }

        protected override void OnCompetion()
        {
            Interlocked.CompareExchange(ref _completed, 0, 1);
            var oldStarted = Interlocked.CompareExchange(ref _started, 0, 1);
            if (oldStarted == 1)
            {
                _svc.Stop();
            }
        }

        protected override void OnDispose()
        {
            var oldStarted = Interlocked.CompareExchange(ref _started, 0, 1);
            if (oldStarted == 1)
            {
                _svc.Stop();
            }
            _svc.Dispose();
            base.OnDispose();
        }
    }
}
