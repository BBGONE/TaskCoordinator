using TasksCoordinator;

namespace SSSB
{
    public class SSSBTasksCoordinator: BaseTasksCoordinator<SSSBMessage, ISSSBDispatcher>
    {
        public SSSBTasksCoordinator(ISSSBDispatcher messageDispatcher, SSSBMessageProducer messageProducer,
            SSSBMessageReaderFactory messageReaderFactory, 
            int maxReadersCount, bool isQueueActivationEnabled = false, bool isEnableParallelReading = false):
            base(messageDispatcher, messageProducer, messageReaderFactory , maxReadersCount, isQueueActivationEnabled, isEnableParallelReading)
        {
        }

        public string ServiceName
        {
            get { return this.MessageDispatcher.Name; }
        }

        public string QueueName
        {
            get { return this.MessageDispatcher.QueueName; }
        }
    }
}
