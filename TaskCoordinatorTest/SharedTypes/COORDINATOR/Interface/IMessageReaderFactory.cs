namespace TasksCoordinator.Interface
{
    public interface IMessageReaderFactory<M, D>
         where D : IMessageDispatcher<M>
    {
        IMessageReader<M> CreateReader(int taskId, IMessageProducer<M> messageProducer, BaseTasksCoordinator<M, D> coordinator);
    }
}
