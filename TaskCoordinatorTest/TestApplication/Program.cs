﻿using System;
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
        private const int BATCH_SIZE = 200;
        private const int MAX_TASK_COUNT = 4;
        private const bool ENABLE_PARRALEL_READING = true;
        private const bool IS_ACTIVATION_ENABLED = false;


        static void Main(string[] args)
        {
            Program.Start().Wait();
        }

        private class CallBack : ICallback
        {
            public void TaskCompleted(Message message, string error)
            {
                if (string.IsNullOrEmpty(error))
                {
                    Interlocked.Increment(ref Program.ProcessedCount);
                    var payload = _serializer.Deserialize<Payload>(message.Body);
                    string result = System.Text.Encoding.UTF8.GetString(payload.Result);
                    Console.WriteLine($"SEQNUM: {message.SequenceNumber} Result: {result}");
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
                }

                if (ProcessedCount == BATCH_SIZE)
                {
                     Console.WriteLine($"BATCH WITH {BATCH_SIZE} messages completed after: {stopwatch.ElapsedMilliseconds} ms");
                }
            }
        }

        private static async Task Start()
        {
            stopwatch = new Stopwatch();
            SEQUENCE_NUM = 0;
            ProcessedCount = 0;
            ErrorCount = 0;
            svc = new TestService(_serializer, "TestService", MAX_TASK_COUNT, IS_ACTIVATION_ENABLED, ENABLE_PARRALEL_READING);
            svc.RegisterCallback(ClientID, new CallBack());

            for (int i = 0; i < BATCH_SIZE; ++i)
            {
                svc.AddToQueue(CreateNewPayload(), Interlocked.Increment(ref SEQUENCE_NUM), typeof(Payload).Name);
            }

            Console.WriteLine(string.Format("QueueLength: {0}", svc.QueueLength));
            stopwatch.Start();

            await svc.Start();
            // var producerTask = QueueAdditionalData();
            // svc.StartActivator(50);
            // stop after 10 seconds
            var stopTask = Stop(10);
            Console.ReadLine();
            svc.Stop();
        }

        public static async Task Stop(int delaySeconds)
        {
            await Task.Delay(1000 * delaySeconds);
            svc.Stop();
            svc.UnRegisterCallback(ClientID);

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
