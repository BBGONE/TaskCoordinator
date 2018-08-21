namespace Shared.Services
{
    public interface ITaskService
    {
        string Name
        {
            get;
        }

        IQueueActivator QueueActivator
        {
            get;
        }

        bool isQueueActivationEnabled { get; }
    }
}
