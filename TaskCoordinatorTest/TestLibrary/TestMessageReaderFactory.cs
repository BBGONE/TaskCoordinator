using Shared;
using System.Collections.Concurrent;
using TasksCoordinator.Interface;

namespace TasksCoordinator.Test
{
    public class TestMessageReaderFactory: IMessageReaderFactory
    {
        private readonly ILog _log;
        private readonly BlockingCollection<Message> _messageQueue;
        private readonly IMessageDispatcher<Message, object> _messageDispatcher;
        private readonly int _artificialDelay;

        public TestMessageReaderFactory(BlockingCollection<Message> messageQueue, IMessageDispatcher<Message, object> messageDispatcher, int artificialDelay = 0)
        {
            this._log = LogFactory.GetInstance("TestMessageReader");
            this._messageQueue = messageQueue;
            this._messageDispatcher = messageDispatcher;
            this._artificialDelay = artificialDelay;
        }

        public IMessageReader CreateReader(long taskId, BaseTasksCoordinator coordinator)
        {
            return new TestMessageReader<Message>(taskId, coordinator, _log, _messageQueue, _messageDispatcher, this._artificialDelay);
        }
    }
}
