using System.Threading.Tasks;

namespace TasksCoordinator.Interface
{
    public struct BatchInfo
    {
        public int BatchSize;
        public bool IsComplete;
    }

    public interface ICallback<T>
    {
        void TaskSuccess(T message);
        Task<bool> TaskError(T message, string error);
        void JobCancelled();
        void JobCompleted(string error);

        int UpdateBatchSize(int batchSize, bool isComplete);

        Task ResultAsync { get; }
        Task CompleteAsync { get; }

        int BatchSize { get; }

        BatchInfo BatchInfo { get; }
    }
}
