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
        public enum BlockType
        {
            Transform,
            Buffer
        }

        static async Task Main(string[] args)
        {
            BlockType blockType = BlockType.Transform;

            using (ITransformBlock<string, string> transformBlock1 = CreateBlock(blockType))
            using (ITransformBlock<string, string> transformBlock2 = CreateBlock(blockType))
            {

                var lastBlock = transformBlock1.LinkTo(transformBlock2);

                int processedCount = 0;

                lastBlock.OutputSink += (async (output) => { await Task.CompletedTask; Interlocked.Increment(ref processedCount); });

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
            }

            Console.WriteLine("Press any key to continue ...");
            Console.ReadKey();
        }

        private static  ITransformBlock<string, string> CreateBlock(BlockType blockType, Func<string, Task<string>> body = null, Func<string, Task> outputSink = null)
        {
            body = body ?? new Func<string, Task<string>>((async (msg) =>
               {
                   await Task.CompletedTask;
                // Console.WriteLine(msg);
                char[] charArray = msg.ToCharArray();
                   for (int i = 0; i < 500; ++i)
                   {
                       Array.Reverse(charArray);
                   }
                   return new string(charArray);
               }));

            ITransformBlock<string, string> block;

            switch (blockType)
            {
                case BlockType.Transform:
                    {
                        block = new TransformBlock<string, string>(body);
                    }
                    break;
                case BlockType.Buffer:
                    {
                        block = new BufferTransformBlock<string, string>(body);
                    }
                    break;
                default:
                    throw new InvalidOperationException($"Unknown BlockType {blockType}");
            }

            if (outputSink != null)
            {
                block.OutputSink += outputSink;
            }

            return block;
        }
    }

}

