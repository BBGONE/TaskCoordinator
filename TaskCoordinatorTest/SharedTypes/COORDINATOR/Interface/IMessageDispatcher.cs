using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace TasksCoordinator.Interface
{
    public interface IMessageDispatcher<M>
    {
        Task<MessageProcessingResult> DispatchMessages(IEnumerable<M> messages, WorkContext context, Action<M> onProcessStart);
    }
}
