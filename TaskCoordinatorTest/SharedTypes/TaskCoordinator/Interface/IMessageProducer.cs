using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace TasksCoordinator.Interface
{
    public interface IMessageProducer<M>
    {
        Task<IEnumerable<M>> ReadMessages(bool isPrimaryReader, int taskId, CancellationToken cancellation, object state);

        bool IsQueueActivationEnabled
        {
            get;
        }
    }
}
