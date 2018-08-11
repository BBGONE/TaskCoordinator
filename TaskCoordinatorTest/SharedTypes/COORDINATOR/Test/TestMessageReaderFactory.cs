using TasksCoordinator.Interface;

namespace TasksCoordinator.Test
{
    public class TestMessageReaderFactory : IMessageReaderFactory<Message, IMessageDispatcher<Message>>
    {
        public IMessageReader<Message> CreateReader(int taskId, IMessageProducer<Message> messageProducer, BaseTasksCoordinator<Message, IMessageDispatcher<Message>> coordinator)
        {
            return new MessageReader<Message, IMessageDispatcher<Message>>(taskId, messageProducer, coordinator);
        }
    }
}
