using Microsoft.Extensions.Logging;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using TasksCoordinator.Interface;

namespace TasksCoordinator.Test
{
    public class MessageReader<TMessage> : ChannelMessageReader<TMessage>
    {
        private static volatile int _current_cnt = 0;
        public static volatile int MaxConcurrentReading = 0;

        public MessageReader(long taskId, ITaskCoordinatorAdvanced tasksCoordinator, ILogger logger,
            ChannelReader<TMessage> messageQueue, IMessageDispatcher<TMessage, object> dispatcher) :
            base(taskId, tasksCoordinator, logger , messageQueue, dispatcher)
        {
        }

        protected override async Task<TMessage> ReadMessage(bool isPrimaryReader, long taskId, CancellationToken token, object state)
        {
            int cnt = Interlocked.Increment(ref _current_cnt);
            if (cnt > MaxConcurrentReading)
            {
                MaxConcurrentReading = cnt;
            }
            try
            {
                var res = await base.ReadMessage(isPrimaryReader, taskId, token, state);
                return res;

            }
            finally
            {
                Interlocked.Decrement(ref _current_cnt);
            }
        }
    }
}