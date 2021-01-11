using System;
using System.Threading.Tasks;

namespace TPLBlocks
{
    public interface ISource<TOutput>
    {
        Task Completion { get; }

        event Func<TOutput, Task> OutputSink;
    }
}