using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TasksCoordinator.Interface;
using Shared;

namespace TasksCoordinator
{
    public class MessageReader<M, D> : IMessageReader<M>, IMessageWorker<M>
        where D : IMessageDispatcher<M>
    {
        #region Private Fields
        private int _taskId;
        private readonly ITaskCoordinatorAdvanced<M, D> _tasksCoordinator;
        private readonly IMessageProducer<M> _messageProducer;
        private M _currentMessage = default(M);
        private CancellationToken _cancellation;

        protected static ILog _log
        {
            get
            {
                return BaseTasksCoordinator<M, D>._log;
            }
        }
        #endregion

        public MessageReader(int taskId, IMessageProducer<M> messageProducer, ITaskCoordinatorAdvanced<M, D> tasksCoordinator)
        {
            this._taskId = taskId;
            this._tasksCoordinator = tasksCoordinator;
            this._messageProducer = messageProducer;
            this._cancellation = this._tasksCoordinator.Cancellation;
        }

        protected virtual void OnProcessMessageException(Exception ex)
        {
        }

        #region Properties
        protected ITaskCoordinatorAdvanced<M, D> TasksCoordinator
        {
            get
            {
                return this._tasksCoordinator;
            }
        }
        #endregion

        bool IMessageWorker<M>.OnBeforeDoWork()
        {
            //пока обрабатывается сообщение
            //очередь не прослушивается этим потоком
            //пробуем передать эту роль другому свободному потоку
            this.TasksCoordinator.RemoveReader(this, true);
            return true;
        }

        async Task<MessageProcessingResult> IMessageWorker<M>.OnDoWork(IEnumerable<M> messages, object state)
        {
            WorkContext context = new WorkContext(this.taskId, state, this.Cancellation, this._tasksCoordinator);
            var res = await this._tasksCoordinator.MessageDispatcher.DispatchMessages(messages, context, (msg) => { this._currentMessage = msg; });
            return res;
        }

        void IMessageWorker<M>.OnAfterDoWork()
        {
            this.TasksCoordinator.AddReader(this, true);
        }

        /// <summary>
        /// If this method returns False then the thread exits from the  loop
        /// </summary>
        /// <returns></returns>
        async Task<bool> IMessageReader<M>.ProcessMessage()
        {
            bool result = false;
            if (this._tasksCoordinator.IsPaused)
            {
                await Task.Delay(1000);
                return true;
            }

            this._currentMessage = default(M);
            bool isDidWork = false;
            try
            {
                bool isPrimaryReader = this.IsPrimaryReader;
                bool canRead = (isPrimaryReader || this._tasksCoordinator.IsEnableParallelReading);
                if (canRead)
                    isDidWork = await this._messageProducer.GetMessages(this, isPrimaryReader) > 0;
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
            finally
            {
                bool isRemoved = false;
                this.AfterProcessedMessage(isDidWork, out isRemoved);
                result = !isRemoved;
            }
            return result;
        }

        protected void AfterProcessedMessage(bool workDone, out bool isRemoved)
        {
            isRemoved = false;
            if (this.Cancellation.IsCancellationRequested)
            {
                isRemoved = true;
            }
            else if (!workDone)
            {
                isRemoved = this.TasksCoordinator.IsSafeToRemoveReader(this);
            }
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
                return this.TasksCoordinator.IsPrimaryReader(this);
            }
        }


        public CancellationToken Cancellation
        {
            get
            {
                return this._cancellation;
            }
        }

        public IMessageProducer<M> MessageProducer
        {
            get { return _messageProducer; }
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