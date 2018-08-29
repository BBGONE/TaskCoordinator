using System.Threading;
using System.Threading.Tasks;

namespace TasksCoordinator.Interface
{
    public interface ITaskCoordinator
    {
        bool Start();
        Task Stop();
        bool IsPaused { get; set; }
        int MaxReadersCount { get; }
        int TasksCount { get; }
        bool IsEnableParallelReading { get; }
        bool IsQueueActivationEnabled { get; }
        CancellationToken Cancellation { get; }
    }
}
