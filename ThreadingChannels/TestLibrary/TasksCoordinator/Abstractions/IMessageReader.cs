using System.Threading;
using System.Threading.Tasks;

namespace TasksCoordinator
{
    public interface IMessageReader
    {
        long taskId { get; }
        Task<MessageReaderResult> TryProcessMessage(CancellationToken token);
        bool IsPrimaryReader { get; }
    }
}
