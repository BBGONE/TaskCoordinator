using System.Threading;
using System.Threading.Tasks;
using TasksCoordinator.Callbacks;

namespace TPLBlocks
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
            await Task.CompletedTask;
            Interlocked.Increment(ref _ErrorCount);
            return false;
        }
    }
}
