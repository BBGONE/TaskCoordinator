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
        BatchInfo BatchInfo { get; }
        void TaskSuccess(T message);
        Task<bool> TaskError(T message, string error);
        void JobCancelled();
        void JobCompleted(string error);

        BatchInfo UpdateBatchSize(int batchSize, bool isComplete);

        Task ResultAsync { get; }
        Task CompleteAsync { get; }
    }
}
