namespace TasksCoordinator.Test.Interface
{
    public interface ICallback
    {
        void PostTaskCompleted(Message message, string error);
    }
}
