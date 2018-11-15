using Shared.Services;
using System;
using System.Threading.Tasks;

namespace TasksCoordinator.Interface
{
    public interface ITaskCoordinatorAdvanced : ITaskCoordinator, IQueueActivator
    {
        bool StartNewTask();
        bool IsSafeToRemoveReader(IMessageReader reader, bool workDone);
        bool IsPrimaryReader(IMessageReader reader);

        void OnBeforeDoWork(IMessageReader reader);
        void OnAfterDoWork(IMessageReader reader);
        Task<IDisposable> ReadThrottleAsync(bool isPrimaryReader);
        IDisposable ReadThrottle(bool isPrimaryReader);
    }
}
