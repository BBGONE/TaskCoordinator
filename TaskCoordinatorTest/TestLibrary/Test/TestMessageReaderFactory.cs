using System.Collections.Concurrent;
using TasksCoordinator.Interface;

namespace TasksCoordinator.Test
{
    public class TestMessageReaderFactory: IMessageReaderFactory<Message>
    {
        private BlockingCollection<Message> messageQueue;

        public TestMessageReaderFactory(BlockingCollection<Message> messageQueue)
        {
            this.messageQueue = messageQueue;
        }

        public IMessageReader CreateReader(int taskId, BaseTasksCoordinator<Message> coordinator)
        {
            return new InMemoryMessageReader<Message>(taskId, coordinator, messageQueue);
        }
    }
}
