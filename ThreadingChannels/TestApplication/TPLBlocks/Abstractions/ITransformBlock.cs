using System;
using TPLBlocks.Core;

namespace TPLBlocks
{
    public interface ITransformBlock<TInput, TOutput>:  ISource<TOutput>, ITarget<TInput>, IDisposable
    {
        BatchInfo BatchInfo { get; }
    }
}