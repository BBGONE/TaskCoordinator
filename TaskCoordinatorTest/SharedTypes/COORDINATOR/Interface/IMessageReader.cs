using System.Threading;
using System.Threading.Tasks;

namespace TasksCoordinator.Interface
{
    public interface IMessageReader<M>
    {
        int taskId { get; }
        Task<MessageReaderResult> ProcessMessage();
        bool IsPrimaryReader { get; }
        CancellationToken Cancellation { get; }
    }
}
