using TasksCoordinator.Interface;

namespace TasksCoordinator.Test
{
    public class TestMessageReaderFactory : IMessageReaderFactory<Message>
    {
        public IMessageReader CreateReader(int taskId, IMessageProducer<Message> messageProducer, BaseTasksCoordinator<Message> coordinator)
        {
            return new MessageReader<Message>(taskId, messageProducer, coordinator);
        }
    }
}
