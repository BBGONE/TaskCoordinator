using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace TSM.TasksCoordinator
{
    public abstract class MessageReader<TMessage, TState> : IMessageReader
    {
        #region Private Fields
        private long _taskId;
        private readonly ITaskCoordinatorAdvanced _coordinator;
        private readonly ILogger _logger;
        #endregion

        public MessageReader(long taskId, ITaskCoordinatorAdvanced tasksCoordinator, ILogger logger)
        {
            this._taskId = taskId;
            this._coordinator = tasksCoordinator;
            this._logger = logger;
        }

        protected abstract Task<TMessage[]> ReadMessages(bool isPrimaryReader, long taskId, CancellationToken token, TState state);

        protected abstract Task<MessageProcessingResult> DispatchMessage(TMessage message, long taskId, CancellationToken token, TState state);
      
        protected abstract Task<int> DoWork(bool isPrimaryReader, CancellationToken cancellation);

        protected virtual Task OnRollback(TMessage msg, CancellationToken token)
        {
            return Task.CompletedTask;
        }
    
        protected virtual void OnProcessMessageException(Exception ex, TMessage message)
        {
            // NOOP
        }

        async Task<MessageReaderResult> IMessageReader.TryProcessMessage(CancellationToken token)
        {
            if (this._coordinator.IsPaused)
            {
                await Task.Delay(1000, token);
                return new MessageReaderResult(isWorkDone: false, isRemoved: false);
            }
            token.ThrowIfCancellationRequested();
            bool isDidWork = await this.DoWork(this.IsPrimaryReader, token) > 0;

            return this.AfterProcessedMessage(isDidWork, token);
        }

        protected MessageReaderResult AfterProcessedMessage(bool workDone, CancellationToken token)
        {
            bool isRemoved = token.IsCancellationRequested || this._coordinator.IsSafeToRemoveReader(this, workDone);
            return new MessageReaderResult(isWorkDone: workDone, isRemoved: isRemoved);
        }

        protected ILogger Logger
        {
            get { return _logger; }
        }

        public long taskId
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

        public ITaskCoordinatorAdvanced Coordinator {
            get
            {
               return _coordinator;
            }
        }
    }
}