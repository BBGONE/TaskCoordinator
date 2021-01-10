using Shared;
using System;
using System.Diagnostics;
using System.Linq;
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
            TaskTransform,
            Buffer,
            LinkMany
        }

        static async Task Main(string[] args)
        {
            CancellationTokenSource cts = new CancellationTokenSource();

            var task = Task.Run(async () =>
            {
                Console.WriteLine($"Starting {DateTime.Now.ToString("hh:mm:ss")} BlockType.LinkMany (Several (5) Blocks are linked to one) test...");

                await ExecuteBlock(BlockType.LinkMany, cts.Token);

                Console.WriteLine();
             
                // *************************************************************    

                // cts.CancelAfter(3000);
                Console.WriteLine($"Starting {DateTime.Now.ToString("hh:mm:ss")} BlockType.Buffer (Single Threaded) test...");

                await ExecuteBlock(BlockType.Buffer, cts.Token);

                Console.WriteLine();
                // *************************************************************    
                // cts.CancelAfter(1000);

                Console.WriteLine($"Starting {DateTime.Now.ToString("hh:mm:ss")} BlockType.TaskTransform (just several of Task.Run) test...");

                await ExecuteBlock(BlockType.TaskTransform, cts.Token);

                Console.WriteLine();
                // *************************************************************    
                // cts.CancelAfter(1000);

                Console.WriteLine($"Starting {DateTime.Now.ToString("hh:mm:ss")} BlockType.Transform (TaskCoordinator) test...");

                await ExecuteBlock(BlockType.Transform, cts.Token);

            });

            /*
            // Can be used to monitor execution (at needed intervals)
            Task comletedTask = null;

            while(comletedTask != task)
            {
                comletedTask = await Task.WhenAny(task, Task.Delay(1000));
            }
            */

            try
            {
                await task;
                Console.WriteLine();
                Console.WriteLine($"Now: {DateTime.Now.ToString("hh:mm:ss")} Press any key to continue ...");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unexpected exception: {ex.Message} Now: {DateTime.Now.ToString("hh:mm:ss")} Press any key to continue ...");
            }

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
                case BlockType.TaskTransform:
                    {
                        block = new TaskTransformBlock<string, string>(body, new TransformBlockOptions(LogFactory.Instance) { CancellationToken = token });
                    }
                    break;
                case BlockType.Buffer:
                    {
                        block = new BufferBlock<string, string>(body, new BufferBlockOptions(LogFactory.Instance) { CancellationToken= token });
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
            if (blockType== BlockType.LinkMany)
            {
                await ExecuteLinkManyBlock(token, true);
                return;
            }

            int processedCount = 0;

            using (ITransformBlock<string, string> transformBlock1 = CreateBlock(blockType: blockType, token: token))
            using (ITransformBlock<string, string> transformBlock2 = CreateBlock(blockType: blockType, token: token))
            {
                Stopwatch sw = new Stopwatch();
                try
                {

                    var lastBlock = transformBlock1.LinkTo(transformBlock2);

                    lastBlock.OutputSink += (async (output) => { await Task.CompletedTask; Interlocked.Increment(ref processedCount); });

                    sw.Start();

                    for (int i = 0; i < 1000000; ++i)
                    {
                        await transformBlock1.Post(Guid.NewGuid().ToString());
                    }

                    transformBlock1.Complete();


                    await lastBlock.Completion;

                    await Task.Yield();
                }
                catch (OperationCanceledException)
                {
                    Console.WriteLine($"Block execution was cancelled {DateTime.Now.ToString("hh:mm:ss")}");
                }
                finally
                {
                    sw.Stop();
                    Console.WriteLine($"Elapsed time: {sw.ElapsedMilliseconds} Milliseconds, BatchSize: {transformBlock1.BatchInfo.BatchSize} Processed Count: {processedCount}");
                }
            }
        }

        private static async Task ExecuteLinkManyBlock(CancellationToken? token = null, bool testException=false)
        {
            int processedCount = 0;
            var inputs = Enumerable.Range(1, 5).Select((i) => CreateBlock(blockType: BlockType.TaskTransform, token: token)).ToList();


            using (var inputsDisposal = new CompositeDisposable(inputs))
            using (ITransformBlock<string, string> bufferBlock = CreateBlock(blockType: BlockType.Buffer, token: token, body: (msg) => {
                Interlocked.Increment(ref processedCount);
               /*
                if (testException)
                {
                    if (processedCount == 4999999)
                    {
                        throw new ApplicationException("ApplicationException: Test that all dataflow is stopped");
                    }
                }
               */
                return Task.FromResult(msg); 
            }))
            {
                Stopwatch sw = new Stopwatch();
                try
                {

                    var lastBlock = inputs.LinkManyTo(bufferBlock);


                    sw.Start();
                    foreach(var input in inputs)
                    {
                        var dummy1 = input.Completion.ContinueWith((antecedent) => Console.WriteLine($"Input completed at {DateTime.Now.ToString("hh:mm:ss")} with {(antecedent.IsCanceled ? "Cancellation": antecedent.Exception?.Message?? "No Error")}"));

                        var t1 = Task.Run(async () =>
                        {
                            for (int i = 0; i < 1000000; ++i)
                            {
                                await input.Post(Guid.NewGuid().ToString());
                            }

                            input.Complete();
                        });
                    }

                    var dummy2 = lastBlock.Completion.ContinueWith((antecedent) => Console.WriteLine($"Lastblock completed at {DateTime.Now.ToString("hh:mm:ss")} with {(antecedent.IsCanceled ? "Cancellation" : antecedent.Exception?.Message ?? "No Error")}"));

                    await lastBlock.Completion;

                    await Task.Yield();
                }
                catch (OperationCanceledException)
                {
                    Console.WriteLine($"Block execution was cancelled {DateTime.Now.ToString("hh:mm:ss")}");
                }
                finally
                {
                    sw.Stop();
                    Console.WriteLine($"Elapsed time: {sw.ElapsedMilliseconds} Milliseconds, BatchSize: 1000000 Processed Count: {processedCount}");
                }
            }
        }


    }

}

