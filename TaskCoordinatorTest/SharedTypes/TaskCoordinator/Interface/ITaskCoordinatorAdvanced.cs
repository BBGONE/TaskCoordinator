using Shared.Services;
using System.Threading.Tasks;

namespace TasksCoordinator.Interface
{
    public interface ITaskCoordinatorAdvanced<M> : ITaskCoordinator, IQueueActivator
    {
        void StartNewTask();
        bool IsSafeToRemoveReader(IMessageReader reader);
        bool IsPrimaryReader(IMessageReader reader);

        bool OnBeforeDoWork(IMessageReader reader);
        void OnAfterDoWork(IMessageReader reader);
    }
}
