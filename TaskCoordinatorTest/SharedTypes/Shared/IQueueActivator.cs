namespace Services.Shared
{
    public interface IQueueActivator
    {
        bool ActivateQueue();
        bool IsQueueActivationEnabled
        {
            get;
        }
    }
}
