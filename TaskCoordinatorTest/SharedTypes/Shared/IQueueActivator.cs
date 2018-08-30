using System.Threading.Tasks;

namespace Shared.Services
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
