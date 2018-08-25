using Shared.Services;

namespace TasksCoordinator.Interface
{
    public interface ITaskCoordinatorAdvanced<M> : ITaskCoordinator, IQueueActivator
    {
        void StartNewTask();
        void RemoveReader(IMessageReader reader);
        void AddReader(IMessageReader reader);
        bool IsSafeToRemoveReader(IMessageReader reader);
        bool IsPrimaryReader(IMessageReader reader);
        IMessageReader PrimaryReader { get; }
        IMessageDispatcher<M> Dispatcher { get; }
    }
}
