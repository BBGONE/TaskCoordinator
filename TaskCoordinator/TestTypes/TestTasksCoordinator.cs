using TasksCoordinator.Interface;

namespace TasksCoordinator.Test
{
    public class TestTasksCoordinator: BaseTasksCoordinator
    {
        public TestTasksCoordinator(IMessageReaderFactory readerFactory,
            int maxReadersCount, bool isQueueActivationEnabled = false, int maxReadParallelism = 4) :
            base(readerFactory, maxReadersCount,  isQueueActivationEnabled, maxReadParallelism)
        {
        }
    }
}
