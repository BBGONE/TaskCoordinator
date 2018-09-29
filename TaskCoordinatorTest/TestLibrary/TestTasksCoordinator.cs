using TasksCoordinator.Interface;

namespace TasksCoordinator.Test
{
    public class TestTasksCoordinator: BaseTasksCoordinator<Message>
    {
        public TestTasksCoordinator(IMessageReaderFactory<Message> readerFactory,
            int maxReadersCount, int parallelReadingLimit = 1, bool isQueueActivationEnabled = false) :
            base(readerFactory, maxReadersCount, parallelReadingLimit, isQueueActivationEnabled)
        {
        }
    }
}
