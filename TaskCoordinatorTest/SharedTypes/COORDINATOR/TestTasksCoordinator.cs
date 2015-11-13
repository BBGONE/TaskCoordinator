namespace TasksCoordinator
{
    public class TestTasksCoordinator: BaseTasksCoordinator<Message, IMessageDispatcher<Message>>
    {
        public TestTasksCoordinator(IMessageDispatcher<Message> messageDispatcher, int maxReadersCount, bool isQueueActivationEnabled = false, bool isEnableParallelReading = false):
            base(messageDispatcher, maxReadersCount, isQueueActivationEnabled, isEnableParallelReading)
        {
        }

        protected override IMessageProducer<Message> CreateNewMessageProducer()
        {
            return new TestMessageProducer(this);
        }
    }
}
