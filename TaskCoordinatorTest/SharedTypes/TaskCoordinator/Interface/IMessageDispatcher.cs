﻿using System.Threading;
using System.Threading.Tasks;

namespace TasksCoordinator.Interface
{
    public interface IMessageDispatcher<TMessage, TState>
    {
        Task<MessageProcessingResult> DispatchMessage(TMessage message, int taskId, CancellationToken token, TState state);
    }
}
