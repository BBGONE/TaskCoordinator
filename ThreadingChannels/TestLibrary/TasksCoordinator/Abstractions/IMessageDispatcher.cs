using System.Threading;
using System.Threading.Tasks;

namespace TasksCoordinator
{
    public interface IMessageDispatcher<TMessage, TState>
    {
        Task<MessageProcessingResult> DispatchMessage(TMessage message, long taskId, CancellationToken token, TState state);
    }
}
