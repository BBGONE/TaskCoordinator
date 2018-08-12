using Shared.Services;

namespace SSSB
{
    public interface ISSSBService: ITaskService
    {
        string QueueName
        {
            get;
        }
    }
}
