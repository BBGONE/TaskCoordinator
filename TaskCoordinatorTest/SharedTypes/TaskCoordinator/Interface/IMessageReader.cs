﻿using System.Threading;
using System.Threading.Tasks;

namespace TasksCoordinator.Interface
{
    public interface IMessageReader
    {
        int taskId { get; }
        Task<MessageReaderResult> ProcessMessage(CancellationToken token);
        bool IsPrimaryReader { get; }
    }
}
