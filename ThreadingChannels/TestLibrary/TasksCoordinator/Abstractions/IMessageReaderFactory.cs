namespace TasksCoordinator
{
    public interface IMessageReaderFactory
    {
        IMessageReader CreateReader(long taskId, BaseTasksCoordinator coordinator);
    }
}
