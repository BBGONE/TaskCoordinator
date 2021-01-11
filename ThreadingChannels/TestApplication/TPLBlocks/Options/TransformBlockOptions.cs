using System;
using System.Threading;

namespace TPLBlocks.Options
{
    public class TransformBlockOptions
    {
        public static readonly TransformBlockOptions Default = new TransformBlockOptions();

        public TransformBlockOptions()
        {
            BoundedCapacity = 100;
            MaxDegreeOfParallelism = Environment.ProcessorCount;
        }

        public int? BoundedCapacity { get; set; }

        public int MaxDegreeOfParallelism { get; set; }

        public CancellationToken? CancellationToken { get; set; }
    }

}
