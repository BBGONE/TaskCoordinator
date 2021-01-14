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
        public enum TestType
        {
            Transform,
            TaskTransform,
            Buffer,
            LinkMany,
            Predicate
        }

        private static TaskScheduler CurrentScheduler;

        static async Task Main(string[] args)
        {
            using var scheduler = new Threading.Schedulers.WorkStealingTaskScheduler();
            // var scheduler = TaskScheduler.Default;

            CurrentScheduler = scheduler;

            TaskFactory factory = new TaskFactory(TaskScheduler.Default);

            CancellationTokenSource cts = new CancellationTokenSource();

            var task = factory.StartNew(async () =>
            {
                // cts.CancelAfter(1000);

                Console.WriteLine($"Starting {DateTime.Now.ToString("hh:mm:ss")} BlockType.Transform (TaskCoordinator) test...");

                await ExecuteBlock(TestType.Transform, cts.Token);

                Console.WriteLine();
                
                // *************************************************************    

                Console.WriteLine($"Starting {DateTime.Now.ToString("hh:mm:ss")} BlockType.Predicate (Several (3) Blocks are linked with predicate) test...");

                await ExecuteBlock(TestType.Predicate, cts.Token);

                Console.WriteLine();
                
                // *************************************************************    
                
                Console.WriteLine($"Starting {DateTime.Now.ToString("hh:mm:ss")} BlockType.LinkMany (Several (5) Blocks are linked to one) test...");

                await ExecuteBlock(TestType.LinkMany, cts.Token);

                Console.WriteLine();

                // *************************************************************    

                // cts.CancelAfter(3000);
                Console.WriteLine($"Starting {DateTime.Now.ToString("hh:mm:ss")} BlockType.Buffer (Single Threaded) test...");

                await ExecuteBlock(TestType.Buffer, cts.Token);

                Console.WriteLine();
                
                // *************************************************************    
                // cts.CancelAfter(1000);

                Console.WriteLine($"Starting {DateTime.Now.ToString("hh:mm:ss")} BlockType.TaskTransform (just several of Task.Run) test...");

                await ExecuteBlock(TestType.TaskTransform, cts.Token);

                Console.WriteLine();
            }).Unwrap();

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
                Console.WriteLine($"Now: {DateTime.Now.ToString("hh:mm:ss")} Press any key to continue ...");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unexpected exception: {ex.Message} Now: {DateTime.Now.ToString("hh:mm:ss")} Press any key to continue ...");
            }

            Console.ReadKey();
        }

        #region Helpers
        private static  ITransformBlock<string, string> CreateBlock(TestType blockType, Func<string, Task<string>> body = null, Func<string, Task> outputSink = null, CancellationToken? token= null)
        {
            body = body ?? new Func<string, Task<string>>((async (msg) =>
            {
                await Task.CompletedTask;
                // Console.WriteLine(msg);
                char[] charArray = msg.ToCharArray();
                for (int i = 0; i < 100; ++i)
                {
                    Array.Reverse(charArray);
                }
                return new string(charArray);
            }));

            ITransformBlock<string, string> block;

            switch (blockType)
            {
                case TestType.Transform:
                    {
                        block = new TransformBlock<string, string>(body, new TransformBlockOptions() { CancellationToken = token, TaskScheduler= CurrentScheduler });
                    }
                    break;
                case TestType.TaskTransform:
                    {
                        block = new TaskTransformBlock<string, string>(body, new TransformBlockOptions() { CancellationToken = token, TaskScheduler = CurrentScheduler });
                    }
                    break;
                case TestType.Buffer:
                    {
                        block = new BufferBlock<string, string>(body, new BufferBlockOptions() { CancellationToken= token, TaskScheduler = CurrentScheduler });
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

        private static async Task ExecuteBlock(TestType blockType = TestType.Transform, CancellationToken? token = null, bool testException = false)
        {
            if (blockType== TestType.LinkMany)
            {
                await ExecuteLinkManyBlock(token, testException: testException);
                return;
            }

            if (blockType == TestType.Predicate)
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
            var inputs = Enumerable.Range(1, 5).Select((i) => CreateBlock(blockType: TestType.TaskTransform, token: token)).ToList();


            using (var inputsDisposal = new CompositeDisposable(inputs))
            using (ITransformBlock<string, string> bufferBlock = CreateBlock(blockType: TestType.Buffer, token: token, body: (msg) => {
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
                        _ = input.Completion.ContinueWith((antecedent) => Console.WriteLine($"Input completed at {DateTime.Now.ToString("hh:mm:ss")} with {(antecedent.IsCanceled ? "Cancellation": antecedent.Exception?.Message?? "No Error")}"));

                        var t1 = Task.Run(async () =>
                        {
                            for (int i = 0; i < 1000000; ++i)
                            {
                                await input.Post(Guid.NewGuid().ToString());
                            }

                            input.Complete();
                        });
                    }

                    _ = lastBlock.Completion.ContinueWith((antecedent) => Console.WriteLine($"LastBlock completed at {DateTime.Now.ToString("hh:mm:ss")} with {(antecedent.IsCanceled ? "Cancellation" : antecedent.Exception?.Message ?? "No Error")}"));

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
            var transforms = Enumerable.Range(1, 3).Select((i) => CreateBlock(blockType: TestType.TaskTransform, token: token)).ToList();


            using (ITransformBlock<string, string> inputBlock = CreateBlock(blockType: TestType.Buffer, token: token, body: (msg) => {
                return Task.FromResult(msg);
            }))
            using (var transformsDisposal = new CompositeDisposable(transforms))
            using (ITransformBlock<string, string> outputBlock = CreateBlock(blockType: TestType.Buffer, token: token, body: (msg) => {
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
                    
                    // _ = inputBlock.Completion.ContinueWith((antecedent) => Console.WriteLine($"Input completed at {DateTime.Now.ToString("hh:mm:ss")} with {(antecedent.IsCanceled ? "Cancellation" : antecedent.Exception?.Message ?? "No Error")}"));

                    var t1 = Task.Run(async () =>
                    {
                        for (int i = 0; i < 1000000; ++i)
                        {
                            await inputBlock.Post(Guid.NewGuid().ToString());
                        }

                        inputBlock.Complete();
                    });

                    // _ = lastBlock.Completion.ContinueWith((antecedent) => Console.WriteLine($"LastBlock completed at {DateTime.Now.ToString("hh:mm:ss")} with {(antecedent.IsCanceled ? "Cancellation" : antecedent.Exception?.Message ?? "No Error")}"));

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
        #endregion

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

