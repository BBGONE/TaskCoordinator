using System.Threading.Tasks;

namespace TasksCoordinator.Interface
{
    public interface IMessageDispatcher<M>
    {
        Task<MessageProcessingResult> DispatchMessage(M message, WorkContext context);
    }
}
