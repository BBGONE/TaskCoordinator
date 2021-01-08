using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace TestApplication
{
    class Program
    {
        static async Task Main(string[] args)
        {
            using TransformBlock<string, string> transformBlock1 = new TransformBlock<string, string>(async (msg) =>
            {
                await Task.CompletedTask;
                // Console.WriteLine(msg);
                char[] charArray = msg.ToCharArray();
                Array.Reverse(charArray);
                return new string(charArray);
            });

            using TransformBlock<string, string> transformBlock2 = new TransformBlock<string, string>(async (msg) =>
            {
                await Task.CompletedTask;
                // Console.WriteLine(msg);

                return "transform2: "+msg;
            });

            transformBlock2.OutputSink = null;  // = (async (output) => { Console.WriteLine("output: " + output.ToString()); await Task.CompletedTask; });

            transformBlock1.LinkTo(transformBlock2);

            Stopwatch sw = new Stopwatch();

            sw.Start();
                
            for (int i = 0; i < 1000000; ++i)
            {
                await transformBlock1.Post(Guid.NewGuid().ToString());
            }

            transformBlock1.Complete();
            
            
            await transformBlock2.Completion;

            sw.Stop();

            await Task.Yield();

            Console.WriteLine($"Elapsed time: {sw.ElapsedMilliseconds} Milliseconds");

            Console.WriteLine("Press any key to continue ...");
            Console.ReadKey();
        }

    }

}

