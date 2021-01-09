using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using TasksCoordinator;
using TPLBlocks;

namespace TestApplication
{
    class Program
    {
        static async Task Main(string[] args)
        {
            using TransformBlock<string, string> transformBlock1 = new TransformBlock<string, string>(async (msg) =>
            {
                await Task.CompletedTask;
                // Console.WriteLine(msg);
                char[] charArray = msg.ToCharArray();
                Array.Reverse(charArray);
                return new string(charArray);
            });

            using TransformBlock<string, string> transformBlock2 = new TransformBlock<string, string>(async (msg) =>
            {
                await Task.CompletedTask;
                // Console.WriteLine(msg);
                char[] charArray = msg.ToCharArray();
                Array.Reverse(charArray);
                return new string(charArray);
            });

            transformBlock2.OutputSink = null;

            var lastBlock = transformBlock1.LinkTo(transformBlock2);

            int processedCount = 0;
            
            lastBlock.OutputSink += (async (output) => { await Task.CompletedTask;  Interlocked.Increment(ref processedCount); });

            Stopwatch sw = new Stopwatch();

            sw.Start();
                
            for (int i = 0; i < 1000000; ++i)
            {
                await transformBlock1.Post(Guid.NewGuid().ToString());
            }

            transformBlock1.Complete();
            
            
            await lastBlock.Completion;

            sw.Stop();

            await Task.Yield();

            Console.WriteLine($"Elapsed time: {sw.ElapsedMilliseconds} Milliseconds, BatchSize: {transformBlock1.BatchInfo.BatchSize} Processed Count: {processedCount}");

            Console.WriteLine("Press any key to continue ...");
            Console.ReadKey();
        }

    }

}

