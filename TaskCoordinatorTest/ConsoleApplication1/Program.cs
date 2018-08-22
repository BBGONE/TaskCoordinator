using SSSB;
using System;
using System.Threading.Tasks;
using TasksCoordinator;
using TasksCoordinator.Test;

namespace ConsoleApplication1
{
    class Program
    {
        private static TestSSSBService svc;

        static void Main(string[] args)
        {
            Program.Start().Wait();
        }

        private static async Task Start()
        {

            svc = new TestSSSBService("test", 4,false,false, TaskWorkType.Mixed);
            await svc.Start();
            var producerTask = QueueData();
            Console.WriteLine(string.Format("messages processed: {0}", svc.ProcessedMessages.Count));
            Console.WriteLine(string.Format("messages in queue: {0}", svc.MessageQueue.Count));
            svc.StartActivator(50);
            var stopTask = Stop(30);
            Console.ReadLine();
            svc.Stop();
        }

        public static async Task Stop(int delaySeconds)
        {
            await Task.Delay(1000 * delaySeconds);
            svc.Stop();
            Console.WriteLine("**************************************");
            Console.WriteLine("Service is stopped.");
            Console.WriteLine(string.Format("messages processed: {0}", svc.ProcessedMessages.Count));
            Console.WriteLine(string.Format("messages in queue: {0}", svc.MessageQueue.Count));
        }
        
    

        public static async Task QueueData()
        {
            //await Task.Delay(7000);
            Random rnd = new Random();
            int cnt = 0;
            Console.WriteLine($"Initial TasksCount: {svc.TasksCoordinator.TasksCount}");

            for (int i = 0; i < 10; ++i)
            {
                svc.MessageQueue.Add(new Message() { SequenceNumber = ++cnt });
            }
            await Task.Delay(10000);

            for (int i = 0; i < 10; ++i)
            {
                svc.MessageQueue.Add(new Message() { SequenceNumber = ++cnt });
            }

            svc.Activate();

            while (true)
            {
                await Task.Delay(3000);
                int num = 50;
                for (int i = 0; i < num; ++i)
                {
                    svc.MessageQueue.Add(new Message() { SequenceNumber = ++cnt });
                }

                svc.Activate();
            }
        }

    }
}
