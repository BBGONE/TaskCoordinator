using TasksCoordinator;

namespace SSSB
{
    public class SSSBTasksCoordinator: BaseTasksCoordinator<SSSBMessage>
    {
        public SSSBTasksCoordinator(ISSSBDispatcher messageDispatcher, 
             SSSBMessageReaderFactory messageReaderFactory,
             int maxReadersCount, bool isEnableParallelReading = false, bool isQueueActivationEnabled = false) :
             base(messageDispatcher, messageReaderFactory, maxReadersCount, isEnableParallelReading, isQueueActivationEnabled)
        {
        }
    }
}
