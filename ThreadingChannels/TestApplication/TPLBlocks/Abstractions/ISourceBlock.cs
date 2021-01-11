using System;
using System.Threading.Tasks;

namespace TPLBlocks
{
    public interface ISourceBlock<TOutput>: IDataflowBlock
    {
        event Func<TOutput, Task> OutputSink;
    }
}