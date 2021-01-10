using Microsoft.Extensions.Logging;
using Shared;
using System;
using System.Threading;

namespace TPLBlocks
{
    public class TransformBlockOptions
    {
        public static readonly TransformBlockOptions Default = new TransformBlockOptions(LogFactory.Instance);

        public TransformBlockOptions(ILoggerFactory loggerFactory)
        {
            LoggerFactory = loggerFactory;
            BoundedCapacity = 100;
            MaxDegreeOfParallelism = Environment.ProcessorCount;
        }

        public ILoggerFactory LoggerFactory { get; }

        public int? BoundedCapacity { get; set; }

        public int MaxDegreeOfParallelism { get; set; }

        public CancellationToken? CancellationToken { get; set; }
    }

}
