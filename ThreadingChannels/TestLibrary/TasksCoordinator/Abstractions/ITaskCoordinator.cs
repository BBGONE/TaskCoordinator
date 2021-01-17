using System.Threading;
using System.Threading.Tasks;

namespace TSM.TasksCoordinator
{
    public interface ITaskCoordinator
    {
        bool Start();
        Task Stop();
        bool IsPaused { get; set; }
        int MaxTasksCount { get; set; }
        int TasksCount { get; }
        CancellationToken Token { get; }
    }
}
