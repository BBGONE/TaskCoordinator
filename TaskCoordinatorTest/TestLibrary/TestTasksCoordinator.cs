using TasksCoordinator.Interface;

namespace TasksCoordinator.Test
{
    public class TestTasksCoordinator: BaseTasksCoordinator
    {
        public TestTasksCoordinator(IMessageReaderFactory readerFactory,
            int maxReadersCount, bool isQueueActivationEnabled = false) :
            base(readerFactory, maxReadersCount,  isQueueActivationEnabled)
        {
        }
    }
}
