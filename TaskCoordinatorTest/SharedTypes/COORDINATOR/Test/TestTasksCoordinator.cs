using TasksCoordinator.Interface;

namespace TasksCoordinator.Test
{
    public class TestTasksCoordinator: BaseTasksCoordinator<Message, IMessageDispatcher<Message>>
    {
        public TestTasksCoordinator(IMessageDispatcher<Message> messageDispatcher, IMessageProducer<Message> producer,
            IMessageReaderFactory<Message, IMessageDispatcher<Message>> readerFactory,
            int maxReadersCount, bool isQueueActivationEnabled = false, bool isEnableParallelReading = false):
            base(messageDispatcher,producer, readerFactory, maxReadersCount, isQueueActivationEnabled, isEnableParallelReading)
        {
        }
    }
}
