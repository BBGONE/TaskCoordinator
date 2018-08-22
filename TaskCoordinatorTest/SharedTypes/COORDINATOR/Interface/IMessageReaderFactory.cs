namespace TasksCoordinator.Interface
{
    public interface IMessageReaderFactory<M>
    {
        IMessageReader<M> CreateReader(int taskId, IMessageProducer<M> messageProducer, BaseTasksCoordinator<M> coordinator);
    }
}
