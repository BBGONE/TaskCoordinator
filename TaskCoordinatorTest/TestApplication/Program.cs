using System;
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
        private static TaskWorkType _workType = TaskWorkType.ShortCPUBound;
        private static volatile int SEQUENCE_NUM = 0;
        private static readonly Guid ClientID = Guid.NewGuid();
        private static volatile int ProcessedCount;
        private static readonly ISerializer _serializer = new Serializer();

        static void Main(string[] args)
        {
            Program.Start().Wait();
        }

        private class CallBack : ICallback
        {
            public void TaskCompleted(Message message, string error)
            {
                Interlocked.Increment(ref Program.ProcessedCount);
                var payload = _serializer.Deserialize<Payload>(message.Body);
                string result = System.Text.Encoding.UTF8.GetString(payload.Result);
                Console.WriteLine($"SEQNUM: {message.SequenceNumber} Result: {result}");
            }
        }

        private static async Task Start()
        {
            SEQUENCE_NUM = 0;
            ProcessedCount = 0;
            svc = new TestService(_serializer, "TestService", 4, false, true);
            svc.RegisterCallback(ClientID, new CallBack());

            for (int i = 0; i < 50000; ++i)
            {
                svc.AddToQueue(new Payload() { CreateDate = DateTime.Now, WorkType = _workType, ClientID = ClientID }, Interlocked.Increment(ref SEQUENCE_NUM),  typeof(Payload).Name);
            }

            Console.WriteLine(string.Format("QueueLength: {0}", svc.QueueLength));
            await svc.Start();
            var producerTask = QueueAdditionalData();
            svc.StartActivator(50);
            var stopTask = Stop(10);
            Console.ReadLine();
            svc.Stop();
        }

        public static async Task Stop(int delaySeconds)
        {
            await Task.Delay(1000 * delaySeconds);
            svc.Stop();
            svc.UnRegisterCallback(ClientID);

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            Console.WriteLine("**************************************");
            Console.WriteLine("Service is stopped.");
            Console.WriteLine(string.Format("QueueLength: {0}", svc.QueueLength));
            Console.WriteLine(string.Format("ProcessedCount: {0}", Program.ProcessedCount));
        }
        
    

        public static async Task QueueAdditionalData()
        {
            await Task.Delay(7500);
            Console.WriteLine($"Delayed TasksCount: {svc.TasksCoordinator.TasksCount}");

            for (int i = 0; i < 10; ++i)
            {
                svc.AddToQueue(new Payload() { CreateDate = DateTime.Now, WorkType = _workType, ClientID = ClientID }, Interlocked.Increment(ref SEQUENCE_NUM), typeof(Payload).Name);
            }

            svc.Activate();

            while (true && !svc.IsStopped)
            {
                await Task.Delay(1000);
                int num = 50;
                for (int i = 0; i < num; ++i)
                {
                    svc.AddToQueue(new Payload() { CreateDate = DateTime.Now, WorkType = _workType, ClientID = ClientID }, Interlocked.Increment(ref SEQUENCE_NUM), typeof(Payload).Name);
                }

                svc.Activate();
            }
        }

    }
}
