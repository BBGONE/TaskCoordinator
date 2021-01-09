using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using TasksCoordinator.Test.Interface;

namespace TPLBlocks
{
    public class BufferTransformBlock<TInput, TOutput> : BaseTransformBlock<TInput, TOutput>
    {
        private Task _task;
        private Channel<TInput> _channel;
        private ChannelWriter<TInput> _messageQueue;
        private volatile int _started = 0;

        public BufferTransformBlock(Func<TInput, Task<TOutput>> body, BufferTransformBlockOptions blockOptions = null):
            base(body, (blockOptions?? BufferTransformBlockOptions.Default).LoggerFactory)
        {
            blockOptions = blockOptions ?? BufferTransformBlockOptions.Default;
            if (blockOptions.QueueCapacity == null)
            {
                this._channel = Channel.CreateUnbounded<TInput>(new UnboundedChannelOptions
                {
                    SingleWriter = false,
                    SingleReader = true,
                    AllowSynchronousContinuations = true,
                });
            }
            else
            {
                this._channel = Channel.CreateBounded<TInput>(new BoundedChannelOptions(blockOptions.QueueCapacity.Value)
                {
                    FullMode = BoundedChannelFullMode.Wait,
                    SingleWriter = false,
                    SingleReader = true,
                    AllowSynchronousContinuations = true,
                });
            }
            this._messageQueue = this._channel.Writer;
        }

        private void Start()
        {
            var reader = this._channel.Reader;
            var token = this.GetCancellationToken();
            this._task = Task.Run(async () => {
                try
                {
                    while (this._started == 1)
                    {
                        var msg = await reader.ReadAsync(token);
                        await (this as IWorkLoad<TInput>).DispatchMessage(msg, 1, token);
                    }
                }
                catch
                {

                }
            });
        }

        public override async ValueTask<bool> Post(TInput msg)
        {
            var oldStarted = Interlocked.CompareExchange(ref _started, 1, 0);
            if (oldStarted == 0)
            {
                this.Start();
            }
            await _messageQueue.WriteAsync(msg, this.GetCancellationToken());
            this.UpdateBatchSize(1, false);
            return true;
        }

        protected override CancellationToken GetCancellationToken()
        {
            return CancellationToken.None;
        }

        
        protected override void OnDispose()
        {
            Interlocked.CompareExchange(ref _started, 0, 1);
            _messageQueue.TryComplete();
            base.OnDispose();
        }
    }
}
