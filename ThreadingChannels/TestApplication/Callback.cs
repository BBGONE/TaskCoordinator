﻿using System;
using System.Threading;
using System.Threading.Tasks;
using TasksCoordinator.Callbacks;
using TasksCoordinator.Test;

namespace TestApplication
{
    public class CallBack<TMsg> : BaseCallback<TMsg>
        where TMsg: Payload
    {
        private readonly bool showSuccess;
        private readonly bool showError;

        private volatile int _ProcessedCount;
        private volatile int _ErrorCount;
        private MessageService<TMsg> _svc;
        public int ProcessedCount { get => this._ProcessedCount; }
        public int ErrorCount { get => _ErrorCount; }

        public CallBack(MessageService<TMsg> svc,  bool showSuccess= false, bool showError = false)
        {
            this._svc = svc;
            this.showSuccess = showSuccess;
            this.showError = showError;
        }

        public override void TaskSuccess(TMsg message)
        {
            Interlocked.Increment(ref _ProcessedCount);
            var payload = message;
            string result = System.Text.Encoding.UTF8.GetString(payload.Result);
            if (showSuccess)
            {
                Console.WriteLine($"SEQNUM: {message.ClientID} Result: {result}");
            }
        }
        public override async Task<bool> TaskError(TMsg message, string error)
        {
            await Task.FromResult(0);
            Interlocked.Increment(ref _ErrorCount);
            if (showError)
            {
                Console.WriteLine($"SEQNUM: {message.ClientID} Error: {error}");
            }
            if (message.TryCount <= 3)
            {
                await Task.Run(async () =>
                {
                    await this._svc.Post(message, CancellationToken.None);
                });
                return true;
            }
            else
            {
                return false;
            }
        }

        public override void JobCancelled()
        {
            base.JobCancelled();
            Console.WriteLine(string.Format("ProcessedCount: {0}, ErrorCount: {1}", ProcessedCount, ErrorCount));
        }

        public override void JobCompleted(string error)
        {
            base.JobCompleted(error);
            Console.WriteLine(string.Format("ProcessedCount: {0}, ErrorCount: {1}", ProcessedCount, ErrorCount));
        }
    }
}
