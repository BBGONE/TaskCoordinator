
using System.Threading.Tasks;

namespace TasksCoordinator
{
    public interface IMessageProducer<M>
    {
        Task<int> GetMessages(IMessageWorker<M> worker, bool isWaitForEnabled);
    }
}
