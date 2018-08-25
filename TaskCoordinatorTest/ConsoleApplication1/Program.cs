using System;
using System.Threading;
using System.Threading.Tasks;
using TasksCoordinator;
using TasksCoordinator.Test;

namespace ConsoleApplication1
{
    class Program
    {
        private static TestService svc;
        private static string messageType = Enum.GetName(typeof(TaskWorkType), TaskWorkType.UltraShortCPUBound);
        private static volatile int SEQUENCE_NUM = 0;

        static void Main(string[] args)
        {
            Program.Start().Wait();
        }

        private static async Task Start()
        {
            SEQUENCE_NUM = 0;
            svc = new TestService("TestService", 4, false, true);

            for (int i = 0; i < 5000000; ++i)
            {
                svc.MessageQueue.Add(new Message() { SequenceNumber = Interlocked.Increment(ref SEQUENCE_NUM), MessageType= messageType });
            }
            Console.WriteLine(string.Format("Messages in queue: {0}", svc.MessageQueue.Count));
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
            Console.WriteLine("**************************************");
            Console.WriteLine("Service is stopped.");
            Console.WriteLine(string.Format("Messages in queue: {0}", svc.MessageQueue.Count));
            Console.WriteLine(string.Format("ProcessedCount: {0}", svc.ProcessedCount));
        }
        
    

        public static async Task QueueAdditionalData()
        {
            await Task.Delay(7500);
            Console.WriteLine($"Delayed TasksCount: {svc.TasksCoordinator.TasksCount}");

            for (int i = 0; i < 10; ++i)
            {
                svc.MessageQueue.Add(new Message() { SequenceNumber = Interlocked.Increment(ref SEQUENCE_NUM), MessageType = messageType });
            }

            svc.Activate();

            while (true && !svc.IsStopped)
            {
                await Task.Delay(1000);
                int num = 50;
                for (int i = 0; i < num; ++i)
                {
                    svc.MessageQueue.Add(new Message() { SequenceNumber = Interlocked.Increment(ref SEQUENCE_NUM), MessageType = messageType });
                }

                svc.Activate();
            }
        }

    }
}
