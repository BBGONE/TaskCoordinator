using System;
using System.Threading.Tasks;

namespace TPLBlocks
{
    public interface ITarget<TInput>
    {
        long Complete(Exception exception = null);

        ValueTask<bool> Post(TInput msg);
    }

}