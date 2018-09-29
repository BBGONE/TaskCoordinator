using Shared;
using System;
using System.Threading;
using System.Threading.Tasks;
using TasksCoordinator.Interface;

namespace TasksCoordinator
{
    public abstract class MessageReader<TMessage, TState> : IMessageReader
        where TMessage:class
    {
        #region Private Fields
        private int _taskId;
        private readonly ITaskCoordinatorAdvanced<TMessage> _coordinator;
        private readonly ILog _log;
        #endregion

        public MessageReader(int taskId, ITaskCoordinatorAdvanced<TMessage> tasksCoordinator, ILog log)
        {
            this._taskId = taskId;
            this._coordinator = tasksCoordinator;
            this._log = log;
        }

        protected abstract Task<TMessage> ReadMessage(bool isPrimaryReader, int taskId, CancellationToken token, TState state);

        protected abstract Task<MessageProcessingResult> DispatchMessage(TMessage message, int taskId, CancellationToken token, TState state);
      
        protected abstract Task<int> DoWork(bool isPrimaryReader, CancellationToken cancellation);

        protected virtual void OnRollback(TMessage msg, CancellationToken token)
        {
            // NOOP
        }
    
        protected virtual void OnProcessMessageException(Exception ex, TMessage message)
        {
            // NOOP
        }

        async Task<MessageReaderResult> IMessageReader.ProcessMessage(CancellationToken token)
        {
            if (this._coordinator.IsPaused)
            {
                await Task.Delay(1000).ConfigureAwait(false);
                return new MessageReaderResult() { IsWorkDone = true, IsRemoved = false };
            }

            bool isDidWork = await this.DoWork(this.IsPrimaryReader, token).ConfigureAwait(false) > 0;

            return this.AfterProcessedMessage(isDidWork, token);
        }

        protected MessageReaderResult AfterProcessedMessage(bool workDone, CancellationToken token)
        {
            bool isRemoved = false;
            if (token.IsCancellationRequested)
            {
                isRemoved = true;
            }
            else if (!workDone)
            {
                isRemoved = this._coordinator.IsSafeToRemoveReader(this);
            }
            else
            {
                isRemoved = this._coordinator.FreeReadersAvailable < 0;
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