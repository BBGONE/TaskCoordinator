using System;
using System.Threading.Tasks;
using TasksCoordinator.Interface;

namespace TPLBlocks
{
    public interface ITransformBlock<TInput, TOutput>
    {
        BatchInfo BatchInfo { get; }
        Task Completion { get; }
        Func<TOutput, Task> OutputSink { get; set; }

        void Complete();
        void Dispose();
        ValueTask<bool> Post(TInput msg);
    }
}