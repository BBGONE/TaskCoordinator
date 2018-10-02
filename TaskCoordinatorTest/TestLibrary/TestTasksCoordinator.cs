using TasksCoordinator.Interface;

namespace TasksCoordinator.Test
{
    public class TestTasksCoordinator: BaseTasksCoordinator<Message>
    {
        public TestTasksCoordinator(IMessageReaderFactory<Message> readerFactory,
            int maxReadersCount, bool isQueueActivationEnabled = false) :
            base(readerFactory, maxReadersCount,  isQueueActivationEnabled)
        {
        }
    }
}
