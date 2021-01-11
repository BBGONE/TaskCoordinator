using Microsoft.Extensions.Logging;
using Common;
using System.Threading;

namespace TPLBlocks
{
    public class BufferBlockOptions
    {
        public static readonly BufferBlockOptions Default = new BufferBlockOptions(LogFactory.Instance);

        public BufferBlockOptions(ILoggerFactory loggerFactory)
        {
            LoggerFactory = loggerFactory;
            BoundedCapacity = 100;
        }

        public ILoggerFactory LoggerFactory { get; }

        public int? BoundedCapacity { get; set; }

        public CancellationToken? CancellationToken { get; set; }
    }

}
