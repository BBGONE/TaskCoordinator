using Microsoft.Extensions.Logging;
using System.Threading.Channels;
using TasksCoordinator.Interface;

namespace TasksCoordinator.Test
{
    public class TestMessageReaderFactory<TMessage>: IMessageReaderFactory
    {
        private readonly ILogger _logger;
        private readonly ChannelReader<TMessage> _messageQueue;
        private readonly IMessageDispatcher<TMessage, object> _messageDispatcher;

        public TestMessageReaderFactory(ChannelReader<TMessage> messageQueue, IMessageDispatcher<TMessage, object> messageDispatcher, ILoggerFactory loggerFactory)
        {
            this._logger = loggerFactory.CreateLogger("TestMessageReader");
            this._messageQueue = messageQueue;
            this._messageDispatcher = messageDispatcher;
        }

        public IMessageReader CreateReader(long taskId, BaseTasksCoordinator coordinator)
        {
            return new TestMessageReader<TMessage>(taskId, coordinator, this._logger, _messageQueue, _messageDispatcher);
        }
    }
}
