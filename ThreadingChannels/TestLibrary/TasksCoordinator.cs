﻿using Microsoft.Extensions.Logging;

namespace TasksCoordinator.Test
{
    public class TasksCoordinator: BaseTasksCoordinator
    {
        public TasksCoordinator(IMessageReaderFactory readerFactory, ILoggerFactory loggerFactory,
            int maxReadersCount, int maxReadParallelism = 4) :
            base(readerFactory, loggerFactory, maxReadersCount,  maxReadParallelism)
        {
        }
    }
}
