using Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TasksCoordinator.Interface;

namespace TasksCoordinator
{
    public abstract class MessageReader<TMessage, TState> : IMessageReader
    {
        #region Private Fields
        private int _taskId;
        private readonly ITaskCoordinatorAdvanced<TMessage> _coordinator;
        private readonly CancellationToken _cancellation;

        protected static ILog _log
        {
            get
            {
                return BaseTasksCoordinator<TMessage>.Log;
            }
        }
        #endregion

        public MessageReader(int taskId, ITaskCoordinatorAdvanced<TMessage> tasksCoordinator)
        {
            this._taskId = taskId;
            this._coordinator = tasksCoordinator;
            this._cancellation = this._coordinator.Cancellation;
        }

        protected abstract Task<IEnumerable<TMessage>> ReadMessages(bool isPrimaryReader, int taskId, CancellationToken cancellation, TState state);

        protected virtual async Task<int> DoWork(bool isPrimaryReader, CancellationToken cancellation)
        {
            int cnt = 0;
            // Console.WriteLine(string.Format("begin {0} Thread: {1}", this.taskId, Thread.CurrentThread.ManagedThreadId));
            IEnumerable<TMessage> messages = await this.ReadMessages(isPrimaryReader, this.taskId, cancellation, default(TState)).ConfigureAwait(false);
            cnt = messages.Count();
            // Console.WriteLine(string.Format("end {0} {1} {2}", this.taskId, isPrimaryReader, Thread.CurrentThread.ManagedThreadId));
            if (cnt > 0)
            {
                bool isOk = this._coordinator.OnBeforeDoWork(this);
                try
                {
                    foreach (TMessage msg in messages)
                    {
                        // обработка сообщений
                        try
                        {
                            MessageProcessingResult res = await this._coordinator.OnDoWork(msg, null, this.taskId).ConfigureAwait(false);
                            if (res.isRollBack)
                            {
                                this.OnRollback(msg, cancellation);
                            }
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

        protected virtual void OnRollback(TMessage msg, CancellationToken cancellation)
        {
            // NOOP
        }
    
        protected virtual void OnProcessMessageException(Exception ex, TMessage message)
        {
            // NOOP
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

        public ITaskCoordinatorAdvanced<TMessage> Coordinator {
            get
            {
               return _coordinator;
            }
        }
    }
}