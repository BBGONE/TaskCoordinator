using Microsoft.Extensions.Logging;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace TSM.TasksCoordinator.Test
{
    public class TestMessageReader<TMessage> : ChannelMessageReader<TMessage>
    {
        public TestMessageReader(long taskId, ITaskCoordinatorAdvanced tasksCoordinator, ILogger logger,
            ChannelReader<TMessage> messageQueue, IMessageDispatcher<TMessage, object> dispatcher) :
            base(taskId, tasksCoordinator, logger , messageQueue, dispatcher)
        {
        }

        protected override async Task<TMessage[]> ReadMessages(bool isPrimaryReader, long taskId, CancellationToken token, object state)
        {
            return await base.ReadMessages(isPrimaryReader, taskId, token, state);
        }
    }
}