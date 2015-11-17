using System;
using System.Threading.Tasks;
using SSSB;
using TasksCoordinator;

namespace ConsoleApplication1
{
    class Program
    {
        private static BaseSSSBService svc;
        
        static void Main(string[] args)
        {
            Program.Start();
        }

        private static void Start()
        {
            svc = new BaseSSSBService("test", 4);
            Message someItem;
            while (!BaseSSSBService.ProcessedMessages.IsEmpty)
            {
                BaseSSSBService.ProcessedMessages.TryTake(out someItem);
            }
            svc.Start();
            var producerTask = QueueData();
            Console.WriteLine(string.Format("messages processed: {0}", BaseSSSBService.ProcessedMessages.Count));
            Console.WriteLine(string.Format("messages in queue: {0}", TestMessageProducer.MessageQueue.Count));
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
            Console.WriteLine(string.Format("messages processed: {0}", BaseSSSBService.ProcessedMessages.Count));
            Console.WriteLine(string.Format("messages in queue: {0}", TestMessageProducer.MessageQueue.Count));
        }
        
    

        public static async Task QueueData()
        {
            //await Task.Delay(7000);
            Random rnd = new Random();
            int cnt = 0;
            
            for (int i = 0; i < 10; ++i)
            {
                TestMessageProducer.MessageQueue.Add(new Message() { SequenceNumber = ++cnt });
            }
            svc.Activate();
            //return;
            while (true)
            {
                //await Task.Delay(5000);
                await Task.Delay(7000);
                int num = 10;// rnd.Next(0, 1000) % 1;
                for (int i = 0; i < num; ++i)
                {
                    TestMessageProducer.MessageQueue.Add(new Message() { SequenceNumber = ++cnt });
                }
                svc.Activate();
                //Console.WriteLine(string.Format("messages in queue: {0}", MessageProducerTest.MessageQueue.Count));
                //return;
            }
        }

    }
}
