namespace TasksCoordinator.Test.Interface
{
    public interface ICallback
    {
        void TaskCompleted(Message message, string error);
    }
}
