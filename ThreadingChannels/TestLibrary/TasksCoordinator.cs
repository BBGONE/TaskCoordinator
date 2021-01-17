using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace TSM.TasksCoordinator.Test
{
    public class TasksCoordinator: BaseTasksCoordinator
    {
        public TasksCoordinator(IMessageReaderFactory readerFactory, ILoggerFactory loggerFactory,
            int maxReadersCount, int maxReadParallelism = 4, TaskScheduler taskScheduler = null) :
            base(readerFactory, loggerFactory, maxReadersCount,  maxReadParallelism, taskScheduler)
        {
        }
    }
}
