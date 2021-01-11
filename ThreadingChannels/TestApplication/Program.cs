using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TasksCoordinator.Common;
using TPLBlocks;
using TPLBlocks.Options;

namespace TestApplication
{
    class Program
    {
        public enum BlockType
        {
            Transform,
            TaskTransform,
            Buffer,
            LinkMany,
            Predicate
        }

        static async Task Main(string[] args)
        {
            CancellationTokenSource cts = new CancellationTokenSource();

            var task = Task.Run(async () =>
            {
                Console.WriteLine($"Starting {DateTime.Now.ToString("hh:mm:ss")} BlockType.Predicate (Several (3) Blocks are linked with predicate) test...");

                await ExecuteBlock(BlockType.Predicate, cts.Token);

                Console.WriteLine();

                // *************************************************************    
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

        private static async Task ExecuteBlock(BlockType blockType = BlockType.Transform, CancellationToken? token = null, bool testException = false)
        {
            if (blockType== BlockType.LinkMany)
            {
                await ExecuteLinkManyBlock(token, testException: testException);
                return;
            }

            if (blockType == BlockType.Predicate)
            {
                await ExecuteLinkPredicateBlock(token, testException: testException);
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
               
                if (testException)
                {
                    if (processedCount == 4999999)
                    {
                        throw new ApplicationException("ApplicationException: Test that all dataflow is stopped");
                    }
                }
               
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

                    var dummy2 = lastBlock.Completion.ContinueWith((antecedent) => Console.WriteLine($"LastBlock completed at {DateTime.Now.ToString("hh:mm:ss")} with {(antecedent.IsCanceled ? "Cancellation" : antecedent.Exception?.Message ?? "No Error")}"));

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

        private static async Task ExecuteLinkPredicateBlock(CancellationToken? token = null, bool testException = false)
        {
            int cnt1 = 0;
            int cnt2 = 0;
            int cnt3 = 0;
            int processedCount = 0;
            var transforms = Enumerable.Range(1, 3).Select((i) => CreateBlock(blockType: BlockType.TaskTransform, token: token)).ToList();


            using (ITransformBlock<string, string> inputBlock = CreateBlock(blockType: BlockType.Buffer, token: token, body: (msg) => {
                return Task.FromResult(msg);
            }))
            using (var transformsDisposal = new CompositeDisposable(transforms))
            using (ITransformBlock<string, string> outputBlock = CreateBlock(blockType: BlockType.Buffer, token: token, body: (msg) => {
                Interlocked.Increment(ref processedCount);
                return Task.FromResult(msg);
            }))
            {
                Stopwatch sw = new Stopwatch();
                try
                {
                    inputBlock.LinkWithPredicateTo(transforms.First(), (msg) => {
                        var val = Math.Abs(GetDeterministicHashCode(msg)) % 3;
                        return val == 0;
                    });
                    inputBlock.LinkWithPredicateTo(transforms.Skip(1).First(), (msg) => {
                        var val = Math.Abs(GetDeterministicHashCode(msg)) % 3;
                        return val == 1;
                    });
                    inputBlock.LinkWithPredicateTo(transforms.Skip(2).First(), (msg) => {
                        var val = Math.Abs(GetDeterministicHashCode(msg)) % 3;
                        return val == 2;
                    });
                    
                    var lastBlock = transforms.LinkManyTo(outputBlock);


                    int cnt = 0;
                    foreach (var transform in transforms)
                    {

                        ++cnt;
                        int num = cnt;
                        transform.OutputSink += (msg) =>{
                            switch (num)
                            {
                                case 1:
                                    Interlocked.Increment(ref cnt1);
                                    break;
                                case 2:
                                    Interlocked.Increment(ref cnt2);
                                    break;
                                case 3:
                                    Interlocked.Increment(ref cnt3);
                                    break;
                            }
                            return Task.CompletedTask; 
                        };
                    }


                    sw.Start();
                    
                    // var dummy1 = inputBlock.Completion.ContinueWith((antecedent) => Console.WriteLine($"Input completed at {DateTime.Now.ToString("hh:mm:ss")} with {(antecedent.IsCanceled ? "Cancellation" : antecedent.Exception?.Message ?? "No Error")}"));

                    var t1 = Task.Run(async () =>
                    {
                        for (int i = 0; i < 1000000; ++i)
                        {
                            await inputBlock.Post(Guid.NewGuid().ToString());
                        }

                        inputBlock.Complete();
                    });

                    // var dummy2 = lastBlock.Completion.ContinueWith((antecedent) => Console.WriteLine($"LastBlock completed at {DateTime.Now.ToString("hh:mm:ss")} with {(antecedent.IsCanceled ? "Cancellation" : antecedent.Exception?.Message ?? "No Error")}"));

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
                    Console.WriteLine($"Elapsed time: {sw.ElapsedMilliseconds} Milliseconds, BatchSize: 1000000 Processed Count: {cnt1} {cnt2} {cnt3} result: {processedCount}");
                }
            }
        }

        static int GetDeterministicHashCode(ReadOnlySpan<char> str)
        {
            unchecked
            {
                int hash1 = (5381 << 16) + 5381;
                int hash2 = hash1;

                for (int i = 0; i < str.Length; i += 2)
                {
                    hash1 = ((hash1 << 5) + hash1) ^ str[i];
                    if (i == str.Length - 1)
                        break;
                    hash2 = ((hash2 << 5) + hash2) ^ str[i + 1];
                }

                return hash1 + (hash2 * 1566083941);
            }
        }
    }

}

