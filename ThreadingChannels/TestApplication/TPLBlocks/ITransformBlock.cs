using System;
using System.Threading.Tasks;
using TasksCoordinator.Interface;

namespace TPLBlocks
{
    public interface ITransformBlock<TInput, TOutput>: IDisposable
    {
        BatchInfo BatchInfo { get; }
        Task Completion { get; }
        Func<TOutput, Task> OutputSink { get; set; }

        long Complete();
        void Dispose();
        ValueTask<bool> Post(TInput msg);
    }
}