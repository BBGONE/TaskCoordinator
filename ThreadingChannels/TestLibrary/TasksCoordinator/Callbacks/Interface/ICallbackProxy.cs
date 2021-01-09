using System;
using System.Threading.Tasks;

namespace TasksCoordinator.Interface
{
    public interface ICallbackProxy<T>
    {
        BatchInfo BatchInfo { get; }
        Task TaskCompleted(T message, Exception error);
        void JobCancelled();
        void JobCompleted(Exception error);
    }
}
