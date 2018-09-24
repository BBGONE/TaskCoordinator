using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using TasksCoordinator;
using TasksCoordinator.Callbacks;
using TasksCoordinator.Test;
using TasksCoordinator.Test.Interface;

namespace TestApplication
{
    public class CallBack : BaseCallback<Message>
    {
        private const bool SHOW_TASK_SUCESS = false;
        private const bool SHOW_TASK_ERROR = false;

        private volatile int _ProcessedCount;
        private volatile int _ErrorCount;

        private readonly ISerializer _serializer;
        private TestService _svc;
        private readonly Stopwatch stopwatch;

        public int ProcessedCount { get => this._ProcessedCount; }
        public int ErrorCount { get => _ErrorCount; }

        public CallBack(TestService svc, ISerializer serializer)
        {
            this._svc = svc;
            this._serializer = serializer;
            this.stopwatch = new Stopwatch();
        }

        public void StartTiming()
        {
            this.stopwatch.Start();
        }

        public override void TaskSuccess(Message message)
        {
            Interlocked.Increment(ref _ProcessedCount);
            var payload = _serializer.Deserialize<Payload>(message.Body);
            string result = System.Text.Encoding.UTF8.GetString(payload.Result);
            if (SHOW_TASK_SUCESS)
            {
                Console.WriteLine($"SEQNUM: {message.SequenceNumber} Result: {result}");
            }
        }
        public override async Task<bool> TaskError(Message message, string error)
        {
            await Task.FromResult(0);
            Interlocked.Increment(ref _ErrorCount);
            if (SHOW_TASK_ERROR)
            {
                Console.WriteLine($"SEQNUM: {message.SequenceNumber} Error: {error}");
            }
            var payload = _serializer.Deserialize<Payload>(message.Body);
            if (payload.TryCount <= 3)
            {
                this._svc.AddToQueue(payload, (int)message.SequenceNumber, typeof(Payload).Name);
                return true;
            }
            else
            {
                return false;
            }
        }

        public override void JobCancelled()
        {
            this.stopwatch.Stop();
            Console.WriteLine($"BATCH WITH {this.BatchInfo.BatchSize} messages Cancelled after: {stopwatch.ElapsedMilliseconds} ms");
            base.JobCancelled();
            Console.WriteLine(string.Format("ProcessedCount: {0}, ErrorCount: {1}", ProcessedCount, ErrorCount));
        }

        public override void JobCompleted(string error)
        {
            this.stopwatch.Stop();
            Console.WriteLine($"BATCH WITH {this.BatchInfo.BatchSize} messages Completed after: {stopwatch.ElapsedMilliseconds} ms");
            base.JobCompleted(error);
            Console.WriteLine(string.Format("ProcessedCount: {0}, ErrorCount: {1}", ProcessedCount, ErrorCount));
        }
    }
}
