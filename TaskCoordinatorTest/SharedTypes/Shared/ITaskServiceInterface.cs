namespace Services.Shared
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

    }
}
