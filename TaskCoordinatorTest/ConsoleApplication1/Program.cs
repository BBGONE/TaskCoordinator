using System;
using System.Threading.Tasks;
using SSSB;
using TasksCoordinator;
using TasksCoordinator.Test;
using System.Collections.Concurrent;

namespace ConsoleApplication1
{
    class Program
    {
        private static BaseSSSBService svc;
        private static BlockingCollection<Message> MessageQueue;
        private static ConcurrentBag<Message> ProcessedMessages;

        static void Main(string[] args)
        {
            Program.Start();
        }

        private static void Start()
        {
            MessageQueue = new BlockingCollection<Message>();
            ProcessedMessages = new ConcurrentBag<Message>();
            var dispatcher = new TestMessageDispatcher(ProcessedMessages);
            var coordinator = new TestTasksCoordinator(dispatcher, new TestMessageProducer(MessageQueue),
                new TestMessageReaderFactory(), 4,false, false);

            svc = new BaseSSSBService("test", coordinator);
            svc.Start();
            var producerTask = QueueData();
            Console.WriteLine(string.Format("messages processed: {0}", ProcessedMessages.Count));
            Console.WriteLine(string.Format("messages in queue: {0}", MessageQueue.Count));
            svc.StartActivator(500);
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
            Console.WriteLine(string.Format("messages processed: {0}", ProcessedMessages.Count));
            Console.WriteLine(string.Format("messages in queue: {0}", MessageQueue.Count));
        }
        
    

        public static async Task QueueData()
        {
            //await Task.Delay(7000);
            Random rnd = new Random();
            int cnt = 0;
            Console.WriteLine($"Initial TasksCount: {svc.TasksCoordinator.TasksCount}");

            for (int i = 0; i < 10; ++i)
            {
                MessageQueue.Add(new Message() { SequenceNumber = ++cnt });
            }
            svc.Activate();

            while (true)
            {
                await Task.Delay(7000);
                int num = 100; // rnd.Next(0, 1000) % 1;
                for (int i = 0; i < num; ++i)
                {
                    MessageQueue.Add(new Message() { SequenceNumber = ++cnt });
                }
                svc.Activate();
            }
        }

    }
}
