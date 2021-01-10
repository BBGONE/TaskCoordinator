using System;
using System.Threading.Tasks;

namespace TasksCoordinator.Interface
{
    public interface ICallbackProxy<T>
    {
        BatchInfo BatchInfo { get; }
        Task TaskCompleted(T message, Exception error);
        Task JobCancelled();
        Task JobCompleted(Exception error);
    }
}
