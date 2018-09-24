using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using TasksCoordinator;
using TasksCoordinator.Test;
using TasksCoordinator.Test.Interface;
using TasksCoordinator.Callbacks;

namespace TestApplication
{
    class Program
    {
        private static TestService svc;
        private static volatile int SEQUENCE_NUM = 0;
        private static readonly Guid ClientID = Guid.NewGuid();
        private static volatile int ProcessedCount;
        private static volatile int ErrorCount;
        private static readonly ISerializer _serializer = new Serializer();
        private static Stopwatch stopwatch;

        // OPTIONS
        private const TaskWorkType TASK_WORK_TYPE = TaskWorkType.ShortCPUBound;
        private const int BATCH_SIZE = 50;
        private const int MAX_TASK_COUNT = 8;
        private const bool ENABLE_PARRALEL_READING = false;
        private const bool IS_ACTIVATION_ENABLED = false;
        private const int CANCEL_AFTER = 0;
        private const bool SHOW_TASK_SUCESS = false;
        private const bool SHOW_TASK_ERROR = false;
        private static readonly double ERROR_MESSAGES_PERCENT = 0;

        static void Main(string[] args)
        {
            Program.Start().Wait();
        }

        private class CallBack : BaseCallback<Message>
        {
            public CallBack()
            {
            }

            public override void TaskSuccess(Message message)
            {
                Interlocked.Increment(ref Program.ProcessedCount);
                var payload = _serializer.Deserialize<Payload>(message.Body);
                string result = System.Text.Encoding.UTF8.GetString(payload.Result);
                if (SHOW_TASK_SUCESS)
                {
                    Console.WriteLine($"SEQNUM: {message.SequenceNumber} Result: {result}");
                }
            }
            public override async Task<bool> TaskError(Message message, string error)
            {
                await Task.FromResult(0);
                Interlocked.Increment(ref Program.ErrorCount);
                if (SHOW_TASK_ERROR)
                {
                    Console.WriteLine($"SEQNUM: {message.SequenceNumber} Error: {error}");
                }
                var payload = _serializer.Deserialize<Payload>(message.Body);
                if (payload.TryCount <= 3)
                {
                    svc.AddToQueue(payload, (int)message.SequenceNumber, typeof(Payload).Name);
                    return true;
                }
                else
                {
                    return false;
                }
            }
            public override void JobCancelled()
            {
                Console.WriteLine($"BATCH WITH {this.BatchInfo.BatchSize} messages Cancelled after: {stopwatch.ElapsedMilliseconds} ms");
                base.JobCancelled();
            }
            public override void JobCompleted(string error)
            {
                Console.WriteLine($"BATCH WITH {this.BatchInfo.BatchSize} messages Completed after: {stopwatch.ElapsedMilliseconds} ms");
                base.JobCompleted(error);
            }
        }

        private static void QueueAdditional(CallBack callBack) {
            // Asynchronously adds more items to the batch (NO AWAIT HERE)
            Task.Delay(2000).ContinueWith((t) =>
            {
                Console.WriteLine(string.Format("Before QueueLength1: {0}", svc.QueueLength));
                for (int i = 0; i < BATCH_SIZE; ++i)
                {
                    svc.AddToQueue(CreateNewPayload(), Interlocked.Increment(ref SEQUENCE_NUM), typeof(Payload).Name);
                }
                var batchInfo = callBack.UpdateBatchSize(BATCH_SIZE, false);
                Console.WriteLine(string.Format("After QueueLength1: {0}", svc.QueueLength));
            });

            // Asynchronously adds more items to the batch (NO AWAIT HERE)
            Task.Delay(4000).ContinueWith((t) =>
            {
                Console.WriteLine(string.Format("Before QueueLength2: {0}", svc.QueueLength));
                for (int i = 0; i < BATCH_SIZE; ++i)
                {
                    svc.AddToQueue(CreateNewPayload(), Interlocked.Increment(ref SEQUENCE_NUM), typeof(Payload).Name);
                }
                var batchInfo = callBack.UpdateBatchSize(BATCH_SIZE, true);
                Console.WriteLine(string.Format("After QueueLength2: {0}", svc.QueueLength));
            });
        }

        private static async Task Start()
        {
            int minWork, minIO;
            ThreadPool.GetMinThreads(out minWork, out minIO);
            ThreadPool.SetMinThreads((MAX_TASK_COUNT + 2) > minWork? (MAX_TASK_COUNT + 2): minWork, minIO);

            stopwatch = new Stopwatch();
            SEQUENCE_NUM = 0;
            ProcessedCount = 0;
            ErrorCount = 0;
            svc = new TestService(_serializer, "TestService", MAX_TASK_COUNT, IS_ACTIVATION_ENABLED, ENABLE_PARRALEL_READING);
            try
            {
                stopwatch.Start();
                svc.Start();
                var callBack = new CallBack();
                svc.RegisterCallback(ClientID, callBack);
               
                for (int i = 0; i < BATCH_SIZE; ++i)
                {
                    svc.AddToQueue(CreateNewPayload(), Interlocked.Increment(ref SEQUENCE_NUM), typeof(Payload).Name);
                }
                var batchInfo = callBack.UpdateBatchSize(BATCH_SIZE, false);
                Console.WriteLine(string.Format("QueueLength: {0}", svc.QueueLength));
                QueueAdditional(callBack);

                if (CANCEL_AFTER > 0)
                {
                    await Task.Delay(CANCEL_AFTER).ConfigureAwait(false);
                    svc.Stop();
                }
                await callBack.ResultAsync.ConfigureAwait(false);

                Console.WriteLine($"TasksCount: {svc.TasksCoordinator.TasksCount}");
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

                // var producerTask = QueueAdditionalData();
                // svc.StartActivator(50);
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
            if (stopwatch.IsRunning)
            {
                stopwatch.Stop();
                Console.WriteLine($"Service stopped after: {stopwatch.ElapsedMilliseconds} ms");
            }
            Console.WriteLine(string.Format("QueueLength: {0}", svc.QueueLength));
            Console.WriteLine(string.Format("ProcessedCount: {0}", Program.ProcessedCount));
            Console.WriteLine(string.Format("ErrorCount: {0}", Program.ErrorCount));

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }


        private static Payload CreateNewPayload()
        {
            TaskWorkType workType = TASK_WORK_TYPE;

            if (workType== TaskWorkType.Random)
            {
                int maxVal = 2;
                int val = SEQUENCE_NUM % maxVal;
                workType = val == 0 ? TaskWorkType.LongCPUBound: TaskWorkType.LongIOBound;
            }
            bool raiseError = false;

            if (ERROR_MESSAGES_PERCENT > 0)
            {
                if (SEQUENCE_NUM % ((int)Math.Floor(100 / ERROR_MESSAGES_PERCENT)) == 0)
                    raiseError = true;
            }

            return new Payload() {
                CreateDate = DateTime.Now,
                WorkType = workType,
                ClientID = ClientID,
                TryCount = 0,
                RaiseError= raiseError
            };
        }

        public static async Task QueueAdditionalData()
        {
            await Task.Delay(7500);
            Console.WriteLine($"Delayed TasksCount: {svc.TasksCoordinator.TasksCount}");

            for (int i = 0; i < 10; ++i)
            {
                svc.AddToQueue(CreateNewPayload(), Interlocked.Increment(ref SEQUENCE_NUM), typeof(Payload).Name);
            }

            svc.Activate();

            while (true && !svc.IsStopped)
            {
                await Task.Delay(1000);
                int num = 50;
                for (int i = 0; i < num; ++i)
                {
                    svc.AddToQueue(CreateNewPayload(), Interlocked.Increment(ref SEQUENCE_NUM), typeof(Payload).Name);
                }

                svc.Activate();
            }
        }

    }
}
