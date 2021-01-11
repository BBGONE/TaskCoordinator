using System.Threading;
using System.Threading.Tasks;
using TasksCoordinator.Common;

namespace TasksCoordinator.Test
{
    public class MessageDispatcher<TMessage>: IMessageDispatcher<TMessage, object>
    {
        private readonly IWorkLoad<TMessage> _workLoad;

        public MessageDispatcher(IWorkLoad<TMessage> workLoad)
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
