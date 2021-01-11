using System;
using System.Threading.Tasks;
using TPLBlocks.Core;

namespace TPLBlocks
{
    public interface ICallbackProxy<T>
    {
        BatchInfo BatchInfo { get; }
        Task TaskCompleted(T message, Exception error);
        bool JobCancelled();
        bool JobCompleted(Exception error);
    }
}
