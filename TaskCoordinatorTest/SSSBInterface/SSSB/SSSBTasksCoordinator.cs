using TasksCoordinator;

namespace SSSB
{
    public class SSSBTasksCoordinator: BaseTasksCoordinator<SSSBMessage, ISSSBDispatcher>
    {
        public SSSBTasksCoordinator(ISSSBDispatcher messageDispatcher, int maxReadersCount, bool isQueueActivationEnabled = false, bool isEnableParallelReading = false):
            base(messageDispatcher, maxReadersCount, isQueueActivationEnabled, isEnableParallelReading)
        {
        }

        protected override IMessageProducer<SSSBMessage> CreateNewMessageProducer()
        {
            return new SSSBMessageProducer(this);
        }

        protected override IMessageReader<SSSBMessage> CreateNewMessageReader(int taskId)
        {
            return new SSSBMessageReader(taskId, this);
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
