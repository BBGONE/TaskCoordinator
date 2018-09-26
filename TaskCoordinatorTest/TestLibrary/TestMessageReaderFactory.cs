using Shared;
using System.Collections.Concurrent;
using TasksCoordinator.Interface;

namespace TasksCoordinator.Test
{
    public class TestMessageReaderFactory: IMessageReaderFactory<Message>
    {
        private readonly ILog _log;
        private readonly BlockingCollection<Message> _messageQueue;
        private readonly IMessageDispatcher<Message, object> _messageDispatcher;

        public TestMessageReaderFactory(BlockingCollection<Message> messageQueue, IMessageDispatcher<Message, object> messageDispatcher)
        {
            this._log = LogFactory.GetInstance("InMemoryMessageReader");
            this._messageQueue = messageQueue;
            this._messageDispatcher = messageDispatcher;
        }

        public IMessageReader CreateReader(int taskId, BaseTasksCoordinator<Message> coordinator)
        {
            return new InMemoryMessageReader<Message>(taskId, coordinator, _log, _messageQueue, _messageDispatcher);
        }
    }
}
