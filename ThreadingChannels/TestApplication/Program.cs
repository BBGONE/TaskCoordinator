using Shared;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
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
            CancellationTokenSource cts = new CancellationTokenSource();
            cts.CancelAfter(30000);

            Console.WriteLine("Starting BlockType.Buffer (Single Threaded) test...");

            await ExecuteBlock(BlockType.Buffer);

            // *************************************************************    

            Console.WriteLine("Starting BlockType.Transform (TaskCoordinator) test...");

            await ExecuteBlock(BlockType.Transform, cts.Token);


            Console.WriteLine("Press any key to continue ...");
            Console.ReadKey();
        }

        private static  ITransformBlock<string, string> CreateBlock(BlockType blockType, Func<string, Task<string>> body = null, Func<string, Task> outputSink = null, CancellationToken? token= null)
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
                        block = new TransformBlock<string, string>(body, new TransformBlockOptions(LogFactory.Instance) { CancellationToken = token });
                    }
                    break;
                case BlockType.Buffer:
                    {
                        block = new BufferTransformBlock<string, string>(body, new BufferTransformBlockOptions(LogFactory.Instance) { CancellationToken= token });
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

        private static async Task ExecuteBlock(BlockType blockType = BlockType.Transform, CancellationToken? token = null)
        {
            using (ITransformBlock<string, string> transformBlock1 = CreateBlock(blockType: blockType, token: token))
            using (ITransformBlock<string, string> transformBlock2 = CreateBlock(blockType: blockType, token: token))
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
        }


    }

}

