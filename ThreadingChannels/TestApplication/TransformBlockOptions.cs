using Microsoft.Extensions.Logging;
using Shared;
using System;
using System.Threading;

namespace TestApplication
{
    public class TransformBlockOptions
    {
        public static readonly TransformBlockOptions Default = new TransformBlockOptions(LogFactory.Instance);

        public TransformBlockOptions(ILoggerFactory loggerFactory)
        {
            LoggerFactory = loggerFactory;
            QueueCapacity = 100;
            MaxDegreeOfParallelism = Environment.ProcessorCount;
        }

        public ILoggerFactory LoggerFactory { get; }

        public int? QueueCapacity { get; set; }

        public int MaxDegreeOfParallelism { get; set; }

        public CancellationToken? CancellationToken { get; set; }
    }

}
