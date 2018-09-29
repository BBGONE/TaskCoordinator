using System.Threading;
using System.Threading.Tasks;

namespace TasksCoordinator.Interface
{
    public interface ITaskCoordinator
    {
        bool Start();
        Task Stop();
        bool IsPaused { get; set; }
        int MaxReadersCount { get; set; }
        int FreeReadersAvailable { get; }
        int TasksCount { get; }
        bool IsEnableParallelReading { get; }
        bool IsQueueActivationEnabled { get; }
        CancellationToken Token { get; }
    }
}
