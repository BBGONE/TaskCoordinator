using System.Threading;
using System.Threading.Tasks;

namespace TasksCoordinator.Interface
{
    public interface ITaskCoordinator
    {
        bool Start();
        Task Stop();
        bool IsPaused { get; set; }
        int MaxTasksCount { get; set; }
        int FreeReadersAvailable { get; }
        int TasksCount { get; }
        int ParallelReadingLimit { get; }
        bool IsQueueActivationEnabled { get; }
        CancellationToken Token { get; }
    }
}
