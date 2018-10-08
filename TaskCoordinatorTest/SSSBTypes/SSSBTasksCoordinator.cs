using TasksCoordinator;

namespace SSSB
{
    public class SSSBTasksCoordinator: BaseTasksCoordinator
    {
        public SSSBTasksCoordinator(SSSBMessageReaderFactory messageReaderFactory,
             int maxReadersCount, bool isQueueActivationEnabled = false) :
             base(messageReaderFactory, maxReadersCount, isQueueActivationEnabled)
        {
        }
    }
}
