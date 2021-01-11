using System.Threading.Tasks;

namespace TPLBlocks
{
    public interface ITargetBlock<TInput>: IDataflowBlock
    {
        ValueTask<bool> Post(TInput msg);
    }

}