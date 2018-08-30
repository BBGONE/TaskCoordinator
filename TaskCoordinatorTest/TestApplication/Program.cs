using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TasksCoordinator;
using TasksCoordinator.Test;
using TasksCoordinator.Test.Interface;

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
        private const TaskWorkType TASK_WORK_TYPE = TaskWorkType.UltraShortCPUBound;
        private const int BATCH_SIZE = 5000;
        private const int MAX_TASK_COUNT = 4;
        private const bool ENABLE_PARRALEL_READING = true;
        private const bool IS_ACTIVATION_ENABLED = false;
        private const int CANCEL_AFTER = 0;
        private const bool SHOW_TASK_SUCESS = false;
        private const bool SHOW_TASK_ERROR = false;


        static void Main(string[] args)
        {
            Program.Start().Wait();
        }

        private class CallBack : ICallback
        {
            private readonly int _batchSize;
            private readonly TaskCompletionSource<int> _completionSource;

            public CallBack(int batchSize)
            {
                this._batchSize = batchSize;
                this._completionSource = new TaskCompletionSource<int>();
            }

            public int BatchSize { get { return this._batchSize; } }
            public void TaskSuccess(Message message)
            {
                Interlocked.Increment(ref Program.ProcessedCount);
                var payload = _serializer.Deserialize<Payload>(message.Body);
                string result = System.Text.Encoding.UTF8.GetString(payload.Result);
                if (SHOW_TASK_SUCESS)
                {
                    Console.WriteLine($"SEQNUM: {message.SequenceNumber} Result: {result}");
                }
            }
            public bool TaskError(Message message, string error)
            {
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
            public void JobCancelled()
            {
                Console.WriteLine($"BATCH WITH {this._batchSize} messages Cancelled after: {stopwatch.ElapsedMilliseconds} ms");
                _completionSource.TrySetCanceled();
            }
            public void JobCompleted(string error)
            {
                Console.WriteLine($"BATCH WITH {this._batchSize} messages Completed after: {stopwatch.ElapsedMilliseconds} ms");
                if (string.IsNullOrEmpty(error))
                {
                    _completionSource.TrySetResult(this._batchSize);
                }
                else
                {
                    _completionSource.TrySetException(new Exception(error));
                }
            }
            
            public Task<int> ResultAsync
            {
                get { return this._completionSource.Task; }
            }
        }

        private static async Task Start()
        {
            stopwatch = new Stopwatch();
            SEQUENCE_NUM = 0;
            ProcessedCount = 0;
            ErrorCount = 0;
            svc = new TestService(_serializer, "TestService", MAX_TASK_COUNT, IS_ACTIVATION_ENABLED, ENABLE_PARRALEL_READING);

            for (int i = 0; i < BATCH_SIZE; ++i)
            {
                svc.AddToQueue(CreateNewPayload(), Interlocked.Increment(ref SEQUENCE_NUM), typeof(Payload).Name);
            }
            Console.WriteLine(string.Format("QueueLength: {0}", svc.QueueLength));

            var callBack = new CallBack(BATCH_SIZE);
            svc.RegisterCallback(ClientID, callBack);
            try
            {
                stopwatch.Start();

                svc.Start();

                if (CANCEL_AFTER > 0)
                {
                    await Task.Delay(CANCEL_AFTER).ConfigureAwait(false);
                    svc.Stop();
                }
                await callBack.ResultAsync.ConfigureAwait(false);
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
                int maxVal = 5;
                int val = SEQUENCE_NUM % maxVal;
                workType = (TaskWorkType)val;
            }
            return new Payload() { CreateDate = DateTime.Now, WorkType = workType, ClientID = ClientID, TryCount = 0 };
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
