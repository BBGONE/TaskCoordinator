using TasksCoordinator;

namespace SSSB
{
    public class SSSBTasksCoordinator: BaseTasksCoordinator<SSSBMessage>
    {
        public SSSBTasksCoordinator(SSSBMessageReaderFactory messageReaderFactory,
             int maxReadersCount, bool isEnableParallelReading = false, bool isQueueActivationEnabled = false) :
             base(messageReaderFactory, maxReadersCount, isEnableParallelReading, isQueueActivationEnabled)
        {
        }
    }
}
