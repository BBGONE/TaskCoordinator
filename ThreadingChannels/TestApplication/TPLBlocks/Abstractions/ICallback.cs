using System;
using System.Threading.Tasks;
using TPLBlocks.Core;

namespace TPLBlocks
{
    public interface ICallback<T>
    {
        void TaskSuccess(T message);
        Task<bool> TaskError(T message, Exception error);
        bool JobCancelled();
        bool JobCompleted(Exception error);
        long UpdateBatchSize(long addValue, bool isComplete);

        Task ResultAsync { get; }
        Task CompleteAsync { get; }

        long BatchSize { get; }

        BatchInfo BatchInfo { get; }
    }
}
