namespace TasksCoordinator.Interface
{
    public interface IMessageReaderFactory<M>
    {
        IMessageReader CreateReader(int taskId, BaseTasksCoordinator<M> coordinator);
    }
}
