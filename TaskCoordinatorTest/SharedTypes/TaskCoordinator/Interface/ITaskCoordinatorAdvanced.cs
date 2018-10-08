using Shared.Services;

namespace TasksCoordinator.Interface
{
    public interface ITaskCoordinatorAdvanced : ITaskCoordinator, IQueueActivator
    {
        void StartNewTask();
        bool IsSafeToRemoveReader(IMessageReader reader, bool workDone);
        bool IsPrimaryReader(IMessageReader reader);

        void OnBeforeDoWork(IMessageReader reader);
        void OnAfterDoWork(IMessageReader reader);
    }
}
