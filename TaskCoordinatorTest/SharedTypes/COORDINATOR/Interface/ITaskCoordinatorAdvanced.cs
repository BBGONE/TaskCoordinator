using Shared.Services;
using System.Threading.Tasks;

namespace TasksCoordinator.Interface
{
    public interface ITaskCoordinatorAdvanced<M> : ITaskCoordinator, IQueueActivator
    {
        void StartNewTask();
        void RemoveReader(IMessageReader<M> reader);
        void AddReader(IMessageReader<M> reader);
        bool IsSafeToRemoveReader(IMessageReader<M> reader);
        bool IsPrimaryReader(IMessageReader<M> reader);
        IMessageReader<M> PrimaryReader { get; }
        IMessageDispatcher<M> Dispatcher { get; }
    }
}
