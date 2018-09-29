using TasksCoordinator;

namespace SSSB
{
    public class SSSBTasksCoordinator: BaseTasksCoordinator<SSSBMessage>
    {
        public SSSBTasksCoordinator(SSSBMessageReaderFactory messageReaderFactory,
             int maxReadersCount, int parallelReadingLimit = 1, bool isQueueActivationEnabled = false) :
             base(messageReaderFactory, maxReadersCount, parallelReadingLimit, isQueueActivationEnabled)
        {
        }
    }
}
