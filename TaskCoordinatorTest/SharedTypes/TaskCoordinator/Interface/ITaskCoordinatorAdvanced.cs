using Shared.Services;

namespace TasksCoordinator.Interface
{
    public interface ITaskCoordinatorAdvanced<M> : ITaskCoordinator, IQueueActivator
    {
        void StartNewTask();
        bool IsSafeToRemoveReader(IMessageReader reader, bool workDone);
        bool IsPrimaryReader(IMessageReader reader);

        bool OnBeforeDoWork(IMessageReader reader);
        void OnAfterDoWork(IMessageReader reader);
    }
}
