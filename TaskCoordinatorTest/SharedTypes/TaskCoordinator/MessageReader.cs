using Shared;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TasksCoordinator.Interface;

namespace TasksCoordinator
{
    public class MessageReader<M> : IMessageReader, IMessageWorker<M>
    {
        #region Private Fields
        private int _taskId;
        private readonly ITaskCoordinatorAdvanced<M> _coordinator;
        private readonly IMessageProducer<M> _producer;
        private readonly CancellationToken _cancellation;
        private M _currentMessage = default(M);

        protected static ILog _log
        {
            get
            {
                return BaseTasksCoordinator<M>.Log;
            }
        }
        #endregion

        public MessageReader(int taskId, IMessageProducer<M> messageProducer, ITaskCoordinatorAdvanced<M> tasksCoordinator)
        {
            this._taskId = taskId;
            this._coordinator = tasksCoordinator;
            this._producer = messageProducer;
            this._cancellation = this._coordinator.Cancellation;
        }

        protected virtual void OnProcessMessageException(Exception ex)
        {
        }

        bool IMessageWorker<M>.OnBeforeDoWork()
        {
            //пока обрабатывается сообщение
            //очередь не прослушивается этим потоком
            //пробуем передать эту роль другому свободному потоку
            this._coordinator.RemoveReader(this);
            this._cancellation.ThrowIfCancellationRequested();
            this._coordinator.StartNewTask();
            return true;
        }

        async Task<MessageProcessingResult> IMessageWorker<M>.OnDoWork(IEnumerable<M> messages, object state)
        {
            WorkContext context = new WorkContext(this.taskId, state, this._cancellation, this._coordinator);
            var res = await this._coordinator.Dispatcher.DispatchMessages(messages, context, (msg) => { this._currentMessage = msg; }).ConfigureAwait(false);
            return res;
        }

        void IMessageWorker<M>.OnAfterDoWork()
        {
            this._coordinator.AddReader(this);
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

            this._currentMessage = default(M);
            bool isDidWork = false;
            try
            {
                bool isPrimaryReader = this.IsPrimaryReader;
                bool canRead = (isPrimaryReader || this._coordinator.IsEnableParallelReading);
                if (canRead)
                {
                    isDidWork = await this._producer.DoWork(this, isPrimaryReader, this._cancellation).ConfigureAwait(false) > 0;
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                this.OnProcessMessageException(ex);
                throw;
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

        public M CurrentMessage
        {
            get
            {
                return _currentMessage;
            }
        }
    }
}