using TasksCoordinator;

namespace SSSB
{
    public class SSSBTasksCoordinator: BaseTasksCoordinator
    {
        public SSSBTasksCoordinator(SSSBMessageReaderFactory messageReaderFactory,
             int maxReadersCount, bool isQueueActivationEnabled = false, int maxReadParallelism = 4) :
             base(messageReaderFactory, maxReadersCount, isQueueActivationEnabled, maxReadParallelism)
        {
        }
    }
}
