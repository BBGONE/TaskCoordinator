using System.Threading;
using System.Threading.Tasks;

namespace TasksCoordinator.Interface
{
    public interface IMessageProducer<M>
    {
        Task<int> DoWork(IMessageWorker<M> worker, bool isPrimaryReader);

        bool IsQueueActivationEnabled
        {
            get;
        }

        CancellationToken Cancellation
        {
            get;
            set;
        }
    }
}
