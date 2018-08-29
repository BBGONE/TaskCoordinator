namespace TasksCoordinator.Test.Interface
{
    public interface ICallback
    {
        int BatchSize { get; }
        void TaskSuccess(Message message);
        bool TaskError(Message message, string error);
        void JobCancelled();
        void JobCompleted(string error);
    }

    public interface ICallbackProxy
    {
        int BatchSize { get; }
        void TaskCompleted(Message message, string error);
    }
}
