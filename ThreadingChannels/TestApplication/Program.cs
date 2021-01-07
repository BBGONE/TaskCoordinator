using Shared;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using TasksCoordinator.Interface;
using TasksCoordinator.Test;

namespace TestApplication
{
    class Program
    {
        private static TestService svc;
        private static volatile int SEQUENCE_NUM = 0;
        private static readonly Guid ClientID = Guid.NewGuid();
        // OPTIONS
        private const TaskWorkType TASK_WORK_TYPE = TaskWorkType.UltraShortCPUBound;
        private const int BATCH_SIZE = 1000000;
        private const int MAX_TASK_COUNT = 8;
        private const int MAX_READ_PARALLELISM = MAX_TASK_COUNT;
        private const bool SHOW_TASK_SUCESS = false;
        private const bool SHOW_TASK_ERROR = false;
        private static readonly double ERROR_MESSAGES_PERCENT = 0;
        private static readonly TestWorkLoad workLoad = new TestWorkLoad(LogFactory.Instance);

        static async Task Main(string[] args)
        {
            await Program.Start();
        }

        private static async Task EnqueueData(TestService svc, ICallback<Payload> callback, CancellationToken token)
        {
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            var writeBatchTask = Task.Run(async () =>
            {
                for (int i = 0; i < BATCH_SIZE; ++i)
                {
                    await svc.Post(CreateNewPayload(), Interlocked.Increment(ref SEQUENCE_NUM), token);
                }
                return callback.UpdateBatchSize(BATCH_SIZE, false);
            });

            await writeBatchTask;
            // Mark the Writing is finished
            var batchInfo = callback.UpdateBatchSize(0, true);
            
            stopwatch.Stop();

            Console.WriteLine($"BatchSize queued {batchInfo.BatchSize} time: {stopwatch.ElapsedMilliseconds} ms");
        }

        private static async Task Start()
        {
            SEQUENCE_NUM = 0;
            
            svc = new TestService("TestService", workLoad, LogFactory.Instance, MAX_TASK_COUNT, MAX_READ_PARALLELISM);
            try
            {
                svc.Start();
                var callBack = new CallBack(svc, SHOW_TASK_SUCESS, SHOW_TASK_ERROR);
                workLoad.RegisterCallback(ClientID, callBack, svc.TasksCoordinator.Token);

                Console.WriteLine($"Initial TasksCount: {svc.TasksCoordinator.TasksCount}");
                Console.WriteLine($"Initial QueueLength: Not Available");

                Stopwatch stopwatch = new Stopwatch();
                stopwatch.Start();

                // await StopAfter(2);

                var enquetask = EnqueueData(svc, callBack, svc.TasksCoordinator.Token);

                bool complete = false;
                var completionTask = callBack.ResultAsync;

                while (!complete)
                {
                    complete = completionTask == await Task.WhenAny(completionTask, Task.Delay(1000));
                    if (!complete)
                    {
                        Console.WriteLine($"In Processing TasksCount: {svc.TasksCoordinator.TasksCount}  QueueLength: Not Available");
                    }
                }

                stopwatch.Stop();

                Console.WriteLine($"*** ElapsedMilliseconds: {stopwatch.ElapsedMilliseconds}  MaxConcurrentReading: { TestMessageReader<Payload>.MaxConcurrentReading} ***");

                await enquetask;
                await Task.Delay(1000);
                Console.WriteLine($"Idled TasksCount: {svc.TasksCoordinator.TasksCount} QueueLength: Not Available");
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
                workLoad.UnRegisterCallback(ClientID);
                await StopAfter(0);
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
            Console.WriteLine("QueueLength: Not Available");


            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        private static Payload CreateNewPayload()
        {
            TaskWorkType workType = TASK_WORK_TYPE;

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
    }

}

