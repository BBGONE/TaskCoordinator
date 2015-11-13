using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace TasksCoordinator
{
    public interface IMessageDispatcher<M>
    {
        Task<MessageProcessingResult> DispatchMessages(IEnumerable<M> messages, WorkContext context, Action<M> onProcessStart);
        bool IsPaused { get; }
    }
}
