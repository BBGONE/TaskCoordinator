using System;

namespace TPLBlocks
{
    public interface ITransformBlock<TInput, TOutput>: ISourceBlock<TOutput>, ITargetBlock<TInput>, IDisposable
    {
    }
}
