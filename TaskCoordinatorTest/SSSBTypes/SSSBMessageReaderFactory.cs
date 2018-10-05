using Shared;
using TasksCoordinator;
using TasksCoordinator.Interface;

namespace SSSB
{
    public class SSSBMessageReaderFactory : IMessageReaderFactory<SSSBMessage>
    {
        private readonly ISSSBService _service;
        private readonly ILog _log;
        private readonly ISSSBDispatcher _messageDispatcher;

        public SSSBMessageReaderFactory(ISSSBService service, ISSSBDispatcher messageDispatcher)
        {
            this._log = LogFactory.GetInstance("SSSBMessageReader");
            this._service = service;
            this._messageDispatcher = messageDispatcher;
        }

        public IMessageReader CreateReader(long taskId, BaseTasksCoordinator<SSSBMessage> coordinator)
        {
            return new SSSBMessageReader(taskId, coordinator, _log, _service, _messageDispatcher);
        }
    }
}
