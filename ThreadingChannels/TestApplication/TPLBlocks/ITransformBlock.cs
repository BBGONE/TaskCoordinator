using System;
using System.Threading.Tasks;
using TasksCoordinator.Interface;

namespace TPLBlocks
{
    public interface ITransformBlock<TInput, TOutput>: IDisposable
    {
        BatchInfo BatchInfo { get; }
        Task Completion { get; }

        event Func<TOutput, Task> OutputSink;

        long Complete(Exception exception = null);

        ValueTask<bool> Post(TInput msg);
    }
}