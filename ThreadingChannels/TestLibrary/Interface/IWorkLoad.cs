﻿using System.Threading;
using System.Threading.Tasks;

namespace TasksCoordinator.Test.Interface
{
    public interface IWorkLoad<TMessage>
    {
        Task<bool> DispatchMessage(TMessage message, long taskId, CancellationToken token);
    }
}