using TasksCoordinator.Interface;

namespace TasksCoordinator.Test
{
    public class TestTasksCoordinator: BaseTasksCoordinator<Message>
    {
        public TestTasksCoordinator(IMessageDispatcher<Message> messageDispatcher, IMessageProducer<Message> producer,
            IMessageReaderFactory<Message> readerFactory,
            int maxReadersCount, bool isEnableParallelReading = false):
            base(messageDispatcher,producer, readerFactory, maxReadersCount, isEnableParallelReading)
        {
        }
    }
}
