using TasksCoordinator;
using TasksCoordinator.Interface;

namespace SSSB
{
    public class SSSBMessageReaderFactory : IMessageReaderFactory<SSSBMessage, ISSSBDispatcher>
    {
        private ISSSBService _service;

        public SSSBMessageReaderFactory(ISSSBService service)
        {
            this._service = service;
        }

        public IMessageReader<SSSBMessage> CreateReader(int taskId, IMessageProducer<SSSBMessage> messageProducer, BaseTasksCoordinator<SSSBMessage, ISSSBDispatcher> coordinator)
        {
            return new SSSBMessageReader(this._service, taskId, messageProducer, coordinator);
        }
    }
}
