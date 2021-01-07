using System.Threading;
using System.Threading.Tasks;
using TasksCoordinator.Interface;
using TasksCoordinator.Test.Interface;

namespace TasksCoordinator.Test
{
    public class TestMessageDispatcher<TMessage>: IMessageDispatcher<TMessage, object>
    {
        private readonly IWorkLoad<TMessage> _workLoad;

        public TestMessageDispatcher(IWorkLoad<TMessage> workLoad)
        {
            this._workLoad = workLoad;
        }

        async Task<MessageProcessingResult> IMessageDispatcher<TMessage, object>.DispatchMessage(TMessage message, long taskId, CancellationToken token, object state)
        {
            bool rollBack = await this._workLoad.DispatchMessage(message, taskId, token);
            return new MessageProcessingResult() { isRollBack = rollBack };
        }
    }
}
