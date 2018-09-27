using System.Threading.Tasks;

namespace TasksCoordinator.Interface
{
    public interface ICallbackProxy<T>
    {
        BatchInfo BatchInfo { get; }
        Task TaskCompleted(T message, string error);
    }
}
