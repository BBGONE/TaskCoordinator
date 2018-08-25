namespace TasksCoordinator.Interface
{
    public interface IMessageReaderFactory<M>
    {
        IMessageReader CreateReader(int taskId, IMessageProducer<M> messageProducer, BaseTasksCoordinator<M> coordinator);
    }
}
