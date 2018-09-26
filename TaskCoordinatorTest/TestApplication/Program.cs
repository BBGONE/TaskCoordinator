﻿using System;
using System.Threading;
using System.Threading.Tasks;
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
        private const int BATCH_SIZE = 100000;
        private const int MAX_TASK_COUNT = 6;
        private const bool ENABLE_PARRALEL_READING = false;
        private const bool IS_ACTIVATION_ENABLED = false;
        private const int CANCEL_AFTER = 0;
        private static readonly double ERROR_MESSAGES_PERCENT = 0;

        static void Main(string[] args)
        {
            Program.Start().Wait();
        }

        private static async Task Start()
        {
            int minWork, minIO;
            ThreadPool.GetMinThreads(out minWork, out minIO);
            ThreadPool.SetMinThreads((MAX_TASK_COUNT+2) > minWork? (MAX_TASK_COUNT + 2) : minWork, minIO);

         
            SEQUENCE_NUM = 0;
            svc = new TestService(_serializer, "TestService", MAX_TASK_COUNT, IS_ACTIVATION_ENABLED, ENABLE_PARRALEL_READING);
            try
            {
                svc.Start();
                var callBack = new CallBack(svc, _serializer);
                svc.RegisterCallback(ClientID, callBack);

                await Task.Run(() =>
                {
                    callBack.StartTiming();
                    for (int i = 0; i < BATCH_SIZE; ++i)
                    {
                        svc.AddToQueue(CreateNewPayload(), Interlocked.Increment(ref SEQUENCE_NUM), typeof(Payload).Name);
                    }
                    var batchInfo = callBack.UpdateBatchSize(BATCH_SIZE, false);
                });
                await Task.Run(() =>
                {
                    for (int i = 0; i < BATCH_SIZE; ++i)
                    {
                        svc.AddToQueue(CreateNewPayload(), Interlocked.Increment(ref SEQUENCE_NUM), typeof(Payload).Name);
                    }
                    var batchInfo = callBack.UpdateBatchSize(BATCH_SIZE, true);
                });
                Console.WriteLine($"In Processing TasksCount: {svc.TasksCoordinator.TasksCount}");
                Console.WriteLine(string.Format("In Processing  QueueLength: {0}", svc.QueueLength));

                if (CANCEL_AFTER > 0)
                {
                    await Task.Delay(CANCEL_AFTER).ConfigureAwait(false);
                    svc.Stop();
                }
                await callBack.ResultAsync.ConfigureAwait(false);
                
                Console.WriteLine($"Idle TasksCount: {svc.TasksCoordinator.TasksCount}");
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
            Console.WriteLine(string.Format("QueueLength: {0}", svc.QueueLength));
        

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
    }
}
