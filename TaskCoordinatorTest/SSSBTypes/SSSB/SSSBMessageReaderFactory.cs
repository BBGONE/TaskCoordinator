using TasksCoordinator;
using TasksCoordinator.Interface;

namespace SSSB
{
    public class SSSBMessageReaderFactory : IMessageReaderFactory<SSSBMessage, ISSSBDispatcher>
    {
        private ErrorMessages ErrorMessages;

        public SSSBMessageReaderFactory(ErrorMessages errorMessages)
        {
            this.ErrorMessages = errorMessages;
        }

        public IMessageReader<SSSBMessage> CreateReader(int taskId, IMessageProducer<SSSBMessage> messageProducer, BaseTasksCoordinator<SSSBMessage, ISSSBDispatcher> coordinator)
        {
            return new SSSBMessageReader(taskId, messageProducer, coordinator, this.ErrorMessages);
        }
    }
}
