using System.Threading;

namespace TPLBlocks.Options
{
    public class BufferBlockOptions
    {
        public static readonly BufferBlockOptions Default = new BufferBlockOptions();

        public BufferBlockOptions()
        {
            BoundedCapacity = 100;
        }

        public int? BoundedCapacity { get; set; }

        public CancellationToken? CancellationToken { get; set; }
    }

}
