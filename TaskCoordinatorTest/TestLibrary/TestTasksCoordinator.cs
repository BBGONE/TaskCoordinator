using TasksCoordinator.Interface;

namespace TasksCoordinator.Test
{
    public class TestTasksCoordinator: BaseTasksCoordinator<Message>
    {
        public TestTasksCoordinator(IMessageReaderFactory<Message> readerFactory,
            int maxReadersCount, bool isQueueActivationEnabled = false, int maxParallelReading = 2) :
            base(readerFactory, maxReadersCount,  isQueueActivationEnabled, maxParallelReading)
        {
        }
    }
}
