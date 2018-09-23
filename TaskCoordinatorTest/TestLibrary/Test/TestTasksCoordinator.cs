using TasksCoordinator.Interface;

namespace TasksCoordinator.Test
{
    public class TestTasksCoordinator: BaseTasksCoordinator<Message>
    {
        public TestTasksCoordinator(IMessageDispatcher<Message> messageDispatcher, IMessageReaderFactory<Message> readerFactory,
            int maxReadersCount, bool isEnableParallelReading = false, bool isQueueActivationEnabled = false) :
            base(messageDispatcher, readerFactory, maxReadersCount, isEnableParallelReading, isQueueActivationEnabled)
        {
        }
    }
}
