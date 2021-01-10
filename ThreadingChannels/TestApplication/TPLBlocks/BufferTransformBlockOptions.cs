using Microsoft.Extensions.Logging;
using Shared;
using System.Threading;

namespace TPLBlocks
{
    public class BufferTransformBlockOptions
    {
        public static readonly BufferTransformBlockOptions Default = new BufferTransformBlockOptions(LogFactory.Instance);

        public BufferTransformBlockOptions(ILoggerFactory loggerFactory)
        {
            LoggerFactory = loggerFactory;
            BoundedCapacity = 100;
        }

        public ILoggerFactory LoggerFactory { get; }

        public int? BoundedCapacity { get; set; }

        public CancellationToken? CancellationToken { get; set; }
    }

}
