using System;
using System.Threading.Tasks;

namespace TasksCoordinator.Interface
{
    public struct BatchInfo
    {
        public long BatchSize;
        public bool IsComplete;
    }

    public interface ICallback<T>
    {
        void TaskSuccess(T message);
        Task<bool> TaskError(T message, Exception error);
        Task JobCancelled();
        Task JobCompleted(Exception error);
        long UpdateBatchSize(long addValue, bool isComplete);

        Task ResultAsync { get; }
        Task CompleteAsync { get; }

        long BatchSize { get; }

        BatchInfo BatchInfo { get; }
    }
}
