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
        private static TaskWorkType _workType = TaskWorkType.Random;
        private static volatile int SEQUENCE_NUM = 0;
        private static readonly Guid ClientID = Guid.NewGuid();
        private static volatile int ProcessedCount;
        private static volatile int ErrorCount;
        private static readonly ISerializer _serializer = new Serializer();
        private static Stopwatch stopwatch;
        private const int BATCH_SIZE = 50;
        private const int MAX_TASK_COUNT = 4;
        private const bool ENABLE_PARRALEL_READING = true;
        private const bool IS_ACTIVATION_ENABLED = false;


        static void Main(string[] args)
        {
            Program.Start().Wait();
        }

        private class CallBack : ICallback
        {
            private readonly int _batchSize;
            private readonly TaskCompletionSource<int> _taskCompletionSource;

            public CallBack(int batchSize)
            {
                this._batchSize = batchSize;
                this._taskCompletionSource = new TaskCompletionSource<int>();
            }

            private void OnTaskOK(Message message)
            {
                Interlocked.Increment(ref Program.ProcessedCount);
                var payload = _serializer.Deserialize<Payload>(message.Body);
                string result = System.Text.Encoding.UTF8.GetString(payload.Result);
                Console.WriteLine($"SEQNUM: {message.SequenceNumber} Result: {result}");

                if (ProcessedCount == this._batchSize)
                {
                    this._taskCompletionSource.TrySetResult(this._batchSize);
                    Console.WriteLine($"BATCH WITH {this._batchSize} messages completed after: {stopwatch.ElapsedMilliseconds} ms");
                }
            }

            public void TaskCompleted(Message message, string error)
            {
                if (string.IsNullOrEmpty(error))
                {
                    this.OnTaskOK(message);
                }
                else
                {
                    if (error == "CANCELLED")
                    {
                        this._taskCompletionSource.TrySetCanceled();
                        Console.WriteLine($"SEQNUM: {message.SequenceNumber} is Cancelled");
                    }
                    else
                    {
                        Interlocked.Increment(ref Program.ErrorCount);
                        Console.WriteLine($"SEQNUM: {message.SequenceNumber} Error: {error}");
                        var payload = _serializer.Deserialize<Payload>(message.Body);
                        if (payload.TryCount <= 3)
                        {
                            svc.AddToQueue(payload, (int)message.SequenceNumber, typeof(Payload).Name);
                        }
                        else
                        {
                            this._taskCompletionSource.TrySetException(new Exception(error));
                        }
                    }
                }
            }

            public Task<int> ResultAsync
            {
                get { return this._taskCompletionSource.Task; }
            }
        }

        private static async Task Start()
        {
            stopwatch = new Stopwatch();
            SEQUENCE_NUM = 0;
            ProcessedCount = 0;
            ErrorCount = 0;
            svc = new TestService(_serializer, "TestService", MAX_TASK_COUNT, IS_ACTIVATION_ENABLED, ENABLE_PARRALEL_READING);
            var callBack = new CallBack(BATCH_SIZE);
            svc.RegisterCallback(ClientID, callBack);
            try
            {
                for (int i = 0; i < BATCH_SIZE; ++i)
                {
                    svc.AddToQueue(CreateNewPayload(), Interlocked.Increment(ref SEQUENCE_NUM), typeof(Payload).Name);
                }

                Console.WriteLine(string.Format("QueueLength: {0}", svc.QueueLength));
                stopwatch.Start();

                await svc.Start();
                // await Task.Delay(1000);
                // svc.Stop();
                await callBack.ResultAsync;
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
            TaskWorkType workType = _workType;

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
