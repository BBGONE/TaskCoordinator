using Microsoft.Extensions.Logging;
using TasksCoordinator.Interface;

namespace TasksCoordinator.Test
{
    public class TestTasksCoordinator: BaseTasksCoordinator
    {
        public TestTasksCoordinator(IMessageReaderFactory readerFactory, ILoggerFactory loggerFactory,
            int maxReadersCount, int maxReadParallelism = 4) :
            base(readerFactory, loggerFactory, maxReadersCount,  maxReadParallelism)
        {
        }
    }
}
