using System;
using System.Threading.Tasks;
using TPLBlocks.Core;

namespace TPLBlocks
{
    public interface IDataflowBlock:  IDisposable
    {
        BatchInfo BatchInfo { get; }

        Task Completion { get; }

        long Complete(Exception exception = null);
    }
}