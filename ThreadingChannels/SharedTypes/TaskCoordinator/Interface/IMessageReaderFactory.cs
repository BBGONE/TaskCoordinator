namespace TasksCoordinator.Interface
{
    public interface IMessageReaderFactory
    {
        IMessageReader CreateReader(long taskId, BaseTasksCoordinator coordinator);
    }
}
