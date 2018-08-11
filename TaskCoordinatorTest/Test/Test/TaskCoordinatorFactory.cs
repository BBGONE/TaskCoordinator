using System.Collections.Concurrent;
using TasksCoordinator.Interface;

namespace TasksCoordinator.Test
{
    public static class TaskCoordinatorFactory
    {
        public static ITaskCoordinator CreateTaskCoordinator(BlockingCollection<Message> MessageQueue, ConcurrentBag<Message> ProcessedMessages, 
            int maxReaders= 4, TaskWorkType workType = TaskWorkType.LongCPUBound)
        {
            var dispatcher = new TestMessageDispatcher(ProcessedMessages, workType);
            return new TestTasksCoordinator(dispatcher, new TestMessageProducer(MessageQueue),
                new TestMessageReaderFactory(), maxReaders, false, false);
        }
    }
}
