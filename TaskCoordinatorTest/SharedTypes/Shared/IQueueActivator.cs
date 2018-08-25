using System.Threading.Tasks;

namespace Shared.Services
{
    public interface IQueueActivator
    {
        Task<bool> ActivateQueue();
        bool IsQueueActivationEnabled
        {
            get;
        }
    }
}
