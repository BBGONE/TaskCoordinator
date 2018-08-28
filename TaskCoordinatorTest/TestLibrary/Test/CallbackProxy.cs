using System.Threading.Tasks;
using TasksCoordinator.Test.Interface;

namespace TasksCoordinator.Test
{
    public class CallbackProxy : ICallback
    {
        private ICallback _callback;

        public CallbackProxy(ICallback callback)
        {
            this._callback = callback;
        }

        void ICallback.TaskCompleted(Message message, string error)
        {
            Task.Factory.StartNew(() => { this._callback.TaskCompleted(message, error); });
        }
    }
}
