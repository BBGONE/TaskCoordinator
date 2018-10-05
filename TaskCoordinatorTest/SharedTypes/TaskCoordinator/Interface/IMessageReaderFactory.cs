namespace TasksCoordinator.Interface
{
    public interface IMessageReaderFactory<M>
    {
        IMessageReader CreateReader(long taskId, BaseTasksCoordinator<M> coordinator);
    }
}
