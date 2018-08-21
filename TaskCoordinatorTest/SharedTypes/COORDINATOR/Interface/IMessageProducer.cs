using System.Threading;
using System.Threading.Tasks;

namespace TasksCoordinator.Interface
{
    public interface IMessageProducer<M>
    {
        Task<int> GetMessages(IMessageWorker<M> worker, bool isPrimaryReader);

        bool IsQueueActivationEnabled
        {
            get;
            set;
        }

        CancellationToken Cancellation
        {
            get;
            set;
        }
    }
}
