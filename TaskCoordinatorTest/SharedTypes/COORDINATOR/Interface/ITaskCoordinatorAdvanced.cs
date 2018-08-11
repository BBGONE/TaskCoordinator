using Shared.Services;

namespace TasksCoordinator.Interface
{
    public interface ITaskCoordinatorAdvanced<M, D> : ITaskCoordinator, IQueueActivator
          where D : IMessageDispatcher<M>
    {
        void RemoveReader(IMessageReader<M> reader, bool isStartedWorking);
        void AddReader(IMessageReader<M> reader, bool isEndedWorking);
        bool IsSafeToRemoveReader(IMessageReader<M> reader);
        bool IsPrimaryReader(IMessageReader<M> reader);
        IMessageReader<M> PrimaryReader { get; }
        D MessageDispatcher { get; }
    }
}
