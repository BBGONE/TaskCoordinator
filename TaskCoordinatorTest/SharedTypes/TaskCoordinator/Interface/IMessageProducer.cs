using System.Threading;
using System.Threading.Tasks;

namespace TasksCoordinator.Interface
{
    public interface IMessageProducer<out M>
    {
        Task<int> DoWork(IMessageWorker<M> worker, bool isPrimaryReader, CancellationToken cancellation);

        bool IsQueueActivationEnabled
        {
            get;
        }
    }
}
