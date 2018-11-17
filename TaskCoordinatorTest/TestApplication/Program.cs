using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TasksCoordinator;
using TasksCoordinator.Interface;
using TasksCoordinator.Test;
using TasksCoordinator.Test.Interface;

namespace TestApplication
{
    class Program
    {
        private static TestService svc;
        private static volatile int SEQUENCE_NUM = 0;
        private static readonly Guid ClientID = Guid.NewGuid();
        private static readonly ISerializer _serializer = new Serializer();
        // OPTIONS
        private const TaskWorkType TASK_WORK_TYPE = TaskWorkType.UltraShortCPUBound;
        private const int BATCH_SIZE = 500000;
        private const int MAX_TASK_COUNT = 6;
        private const int MAX_READ_PARALLELISM = 4;
        private const bool SHOW_TASK_SUCESS = false;
        private const bool SHOW_TASK_ERROR = false;
        private const bool IS_ACTIVATION_ENABLED = false;
        private const int ARTIFICIAL_READ_DELAY = 0;
        private const int CANCEL_AFTER = 0;
        private static readonly double ERROR_MESSAGES_PERCENT = 0;

        static void Main(string[] args)
        {
            Program.Start().Wait();
        }

        #region Enqueue the messages
        static IEnumerable<IGrouping<int, T>> PartitionCollection<T>(IEnumerable<T> collection, int partionCount)
        {
            IEnumerable<IGrouping<int, T>> groups = collection.Select((item, index) => new
            {
                item,
                index
            }).GroupBy(group => group.index % partionCount, element => element.item);
            return groups;
        }

        static async Task<int> EnqueWithPartitioning(TestService svc, ICallback<Message> callback, int messageCount, int partionCount)
        {
            int sentMessagesCount = 0;

            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            try
            {
                var range = Enumerable.Range(0, messageCount);
                var partions = PartitionCollection(range, partionCount: partionCount);
                int partionSize = messageCount / partionCount;

                List<Task> tasks = new List<Task>();
                foreach (var partion in partions)
                {
                    //Console.WriteLine("Partion: {0}", group.Key);
                    var localPartion = partion;
                    // for each partition send messages to the queue in its own task
                    var task1 = SendPartition(partion, partionSize, svc, callback);
                    var task2 = task1.ContinueWith((t) =>
                    {
                        Interlocked.Add(ref sentMessagesCount, t.Result);
                    }, TaskContinuationOptions.ExecuteSynchronously | TaskContinuationOptions.OnlyOnRanToCompletion);
                    tasks.Add(task2);
                }

                await Task.WhenAll(tasks);
            }
            finally
            {
                var batchInfo = callback.UpdateBatchSize(0, true);
                stopwatch.Stop();
                Console.WriteLine($"BatchSize queued {sentMessagesCount} time: {stopwatch.ElapsedMilliseconds} ms");
            }

            return sentMessagesCount;
        }

        static Task<int> SendPartition(IGrouping<int, int> partion, int partionSize, TestService svc, ICallback<Message> callback)
        {
            int sentMessagesCount = 0;

            var task = Task.Run(async () =>
            {
                int cnt = 0;
                foreach (var num in partion)
                {
                    ++cnt;
                    svc.AddToQueue(CreatePayload(), Interlocked.Increment(ref SEQUENCE_NUM), typeof(Payload).Name);
                    if (cnt % 1000 == 0) await Task.Yield();
                    Interlocked.Increment(ref sentMessagesCount);
                }

                Console.WriteLine($"Send patrition #{partion.Key} Size: {sentMessagesCount} offset: {partion.Key * partionSize}");

                callback.UpdateBatchSize(sentMessagesCount, false);
                return sentMessagesCount;
            });

            return task;
        }

        static async Task EnqueueData(TestService svc, ICallback<Message> callback, int messageCount)
        {
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            var task1 = Task.Run(() =>
            {
                for (int i = 0; i < messageCount; ++i)
                {
                    svc.AddToQueue(CreatePayload(), Interlocked.Increment(ref SEQUENCE_NUM), typeof(Payload).Name);
                }
                return callback.UpdateBatchSize(messageCount, false);
            });

            await task1;

            var batchInfo = callback.UpdateBatchSize(0, true);
            stopwatch.Stop();
            Console.WriteLine($"BatchSize queued {batchInfo.BatchSize} time: {stopwatch.ElapsedMilliseconds} ms");
        }

        private static Payload CreatePayload()
        {
            TaskWorkType workType = TASK_WORK_TYPE;

            if (workType == TaskWorkType.Random)
            {
                int maxVal = 5;
                int val = SEQUENCE_NUM % maxVal;
                workType = (TaskWorkType)val;
            }
            bool raiseError = false;

            if (ERROR_MESSAGES_PERCENT > 0)
            {
                if (SEQUENCE_NUM % ((int)Math.Floor(100 / ERROR_MESSAGES_PERCENT)) == 0)
                    raiseError = true;
            }

            return new Payload()
            {
                CreateDate = DateTime.Now,
                WorkType = workType,
                ClientID = ClientID,
                TryCount = 0,
                RaiseError = raiseError
            };
        }
        #endregion

        private static async Task Start()
        {
/*
            int minWork, minIO;
            int needThreads = MAX_TASK_COUNT + 2;
            ThreadPool.GetMinThreads(out minWork, out minIO);
            ThreadPool.SetMinThreads(needThreads > minWork? needThreads : minWork, minIO);
 */           

            SEQUENCE_NUM = 0;
            svc = new TestService(_serializer, "TestService", 0, IS_ACTIVATION_ENABLED, MAX_READ_PARALLELISM, ARTIFICIAL_READ_DELAY);
            try
            {
                svc.Start();
                var callBack = new CallBack(svc, _serializer, SHOW_TASK_SUCESS, SHOW_TASK_ERROR);
                svc.RegisterCallback(ClientID, callBack);
                Console.WriteLine($"Initial TasksCount: {svc.TasksCoordinator.TasksCount}");
                Console.WriteLine(string.Format("Initial QueueLength: {0}", svc.QueueLength));
                TestMessageReader<Message>.MaxConcurrentReading = 0;

                //await EnqueueData(svc, callBack, BATCH_SIZE);
                await EnqueWithPartitioning(svc, callBack, BATCH_SIZE, 4);

                callBack.StartTiming();
                svc.MaxTasksCount = MAX_TASK_COUNT;

                if (CANCEL_AFTER > 0)
                {
                    await Task.Delay(CANCEL_AFTER).ConfigureAwait(false);
                    svc.Stop();
                }

               /*               
                Console.WriteLine($"Set MaxTasksCount to {MAX_TASK_COUNT}");
                await Task.Delay(1000);
                Console.WriteLine($"In Processing TasksCount: {svc.TasksCoordinator.TasksCount}  QueueLength: {svc.QueueLength}");
                svc.MaxTasksCount = 0;
                Console.WriteLine($"Set MaxTasksCount to 0");
                await Task.Delay(5000);
                Console.WriteLine($"Suspended TasksCount: {svc.TasksCoordinator.TasksCount} MaxTasksCount: {svc.MaxTasksCount}  QueueLength: {svc.QueueLength}");
                svc.MaxTasksCount = MAX_TASK_COUNT;
                await Task.Delay(1000);
                Console.WriteLine($"Resumed Processing TasksCount: {svc.TasksCoordinator.TasksCount} MaxTasksCount: {svc.MaxTasksCount}  QueueLength: {svc.QueueLength}");
               */             

                bool complete = false;
                var task = callBack.ResultAsync;
                while (!complete)
                {
                    complete = task == await Task.WhenAny(task, Task.Delay(2000));
                    if (!complete)
                    {
                        Console.WriteLine($"In Processing TasksCount: {svc.TasksCoordinator.TasksCount}  QueueLength: {svc.QueueLength}");
                    }
                }
                Console.WriteLine($"*** MaxConcurrentReading: { TestMessageReader<Message>.MaxConcurrentReading} ***");
               
                await Task.Delay(1000);
                Console.WriteLine($"Idled TasksCount: {svc.TasksCoordinator.TasksCount} QueueLength: {svc.QueueLength}");
            }
            catch (OperationCanceledException)
            {
                // NOOP
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Processing Exception: {ex.Message}");
            }
            finally
            {
                svc.UnRegisterCallback(ClientID);
                await StopAfter(0).ConfigureAwait(false);
                Console.ReadLine();
            }
        }

        public static async Task StopAfter(int delaySeconds)
        {
            await Task.Delay(1000 * delaySeconds);
            svc.Stop();

            Console.WriteLine("**************************************");
            Console.WriteLine("Service is stopped.");
            Console.WriteLine($"Stopped TasksCount: {svc.TasksCoordinator.TasksCount}");
            Console.WriteLine(string.Format("QueueLength: {0}", svc.QueueLength));
        

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }
    }
}
