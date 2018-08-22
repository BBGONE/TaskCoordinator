using TasksCoordinator;

namespace SSSB
{
    public class SSSBTasksCoordinator: BaseTasksCoordinator<SSSBMessage>
    {
        public SSSBTasksCoordinator(ISSSBDispatcher messageDispatcher, 
             SSSBMessageProducer messageProducer,
             SSSBMessageReaderFactory messageReaderFactory,
             int maxReadersCount, bool isEnableParallelReading = false) :
             base(messageDispatcher, messageProducer, messageReaderFactory, maxReadersCount, isEnableParallelReading)
        {
        }
    }
}
