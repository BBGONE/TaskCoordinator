using Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TasksCoordinator.Interface;

namespace TasksCoordinator
{
    public class MessageReader<M> : IMessageReader
    {
        #region Private Fields
        private int _taskId;
        private readonly ITaskCoordinatorAdvanced<M> _coordinator;
        private readonly CancellationToken _cancellation;
        private readonly IMessageProducer<M> _producer;

        protected static ILog _log
        {
            get
            {
                return BaseTasksCoordinator<M>.Log;
            }
        }
        #endregion

        public MessageReader(int taskId, ITaskCoordinatorAdvanced<M> tasksCoordinator, IMessageProducer<M> producer)
        {
            this._taskId = taskId;
            this._coordinator = tasksCoordinator;
            this._cancellation = this._coordinator.Cancellation;
            this._producer = producer;
        }

        protected virtual async Task<int> DoWork(bool isPrimaryReader, CancellationToken cancellation)
        {
            int cnt = 0;
            // Console.WriteLine(string.Format("begin {0} Thread: {1}", this.taskId, Thread.CurrentThread.ManagedThreadId));
            IEnumerable<M> messages = await this._producer.ReadMessages(isPrimaryReader, this.taskId, cancellation, null).ConfigureAwait(false);
            cnt = messages.Count();
            // Console.WriteLine(string.Format("end {0} {1} {2}", this.taskId, isPrimaryReader, Thread.CurrentThread.ManagedThreadId));
            if (cnt > 0)
            {
                bool isOk = this._coordinator.OnBeforeDoWork(this);
                try
                {
                    foreach (M msg in messages)
                    {
                        // обработка сообщений
                        try
                        {
                            MessageProcessingResult res = await this._coordinator.OnDoWork(msg, null, this.taskId).ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            this.OnProcessMessageException(ex, msg);
                            throw;
                        }
                    }
                }
                finally
                {
                    this._coordinator.OnAfterDoWork(this);
                }
            }

            return cnt;
        }

    
        protected virtual void OnProcessMessageException(Exception ex, M message)
        {
        }

       
        /// <summary>
        /// If this method returns False then the thread exits from the  loop
        /// </summary>
        /// <returns></returns>
        async Task<MessageReaderResult> IMessageReader.ProcessMessage()
        {
            if (this._coordinator.IsPaused)
            {
                await Task.Delay(1000).ConfigureAwait(false);
                return new MessageReaderResult() { IsWorkDone = true, IsRemoved = false };
            }

            bool isDidWork = false;
            bool isPrimaryReader = this.IsPrimaryReader;
            bool canRead = (isPrimaryReader || this._coordinator.IsEnableParallelReading);
            if (canRead)
            {
                isDidWork = await this.DoWork(isPrimaryReader, this._cancellation).ConfigureAwait(false) > 0;
            }

            return this.AfterProcessedMessage(isDidWork);
        }

        protected MessageReaderResult AfterProcessedMessage(bool workDone)
        {
            bool isRemoved = false;
            if (this._cancellation.IsCancellationRequested)
            {
                isRemoved = true;
            }
            else if (!workDone)
            {
                isRemoved = this._coordinator.IsSafeToRemoveReader(this);
            }

            return new MessageReaderResult() { IsRemoved = isRemoved, IsWorkDone = workDone };
        }

        public int taskId
        {
            get
            {
                return this._taskId;
            }
        }

        public bool IsPrimaryReader
        {
            get
            {
                return this._coordinator.IsPrimaryReader(this);
            }
        }

        public IMessageProducer<M> Producer {
            get
            {
                return _producer;
            }
        }

        public ITaskCoordinatorAdvanced<M> Coordinator {
            get
            {
               return _coordinator;
            }
        }
    }
}