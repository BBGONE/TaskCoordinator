using System.Threading.Tasks;

namespace TasksCoordinator.Interface
{
    public interface IMessageReader
    {
        int taskId { get; }
        Task<MessageReaderResult> ProcessMessage();
        bool IsPrimaryReader { get; }
    }
}
