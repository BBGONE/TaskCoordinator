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
        private readonly CancellationToken _token;
        private readonly ILog _log;
       
        #endregion

        public MessageReader(int taskId, ITaskCoordinatorAdvanced<TMessage> tasksCoordinator, ILog log)
        {
            this._taskId = taskId;
            this._coordinator = tasksCoordinator;
            this._token = this._coordinator.Cancellation;
            this._log = log;
        }

        protected abstract Task<IEnumerable<TMessage>> ReadMessages(bool isPrimaryReader, int taskId, CancellationToken token, TState state);

        protected abstract Task<MessageProcessingResult> DispatchMessage(TMessage message, int taskId, CancellationToken token, TState state);
      
        protected abstract Task<int> DoWork(bool isPrimaryReader, CancellationToken cancellation);

        protected virtual void OnRollback(TMessage msg, CancellationToken cancellation)
        {
            // NOOP
        }
    
        protected virtual void OnProcessMessageException(Exception ex, TMessage message)
        {
            // NOOP
        }

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
                isDidWork = await this.DoWork(isPrimaryReader, this._token).ConfigureAwait(false) > 0;
            }

            return this.AfterProcessedMessage(isDidWork);
        }

        protected MessageReaderResult AfterProcessedMessage(bool workDone)
        {
            bool isRemoved = false;
            if (this._token.IsCancellationRequested)
            {
                isRemoved = true;
            }
            else if (!workDone)
            {
                isRemoved = this._coordinator.IsSafeToRemoveReader(this);
            }

            return new MessageReaderResult() { IsRemoved = isRemoved, IsWorkDone = workDone };
        }

        protected ILog Log
        {
            get { return _log; }
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