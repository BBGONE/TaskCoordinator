using System.Collections.Generic;
using System.Threading.Tasks;

namespace TasksCoordinator.Interface
{
    public interface IMessageWorker<in M>
    {
        bool OnBeforeDoWork();
        Task<MessageProcessingResult> OnDoWork(IEnumerable<M> messages, object state);
        void OnAfterDoWork();
        int taskId { get; }
    }
}
