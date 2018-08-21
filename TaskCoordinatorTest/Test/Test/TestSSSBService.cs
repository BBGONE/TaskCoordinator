﻿using Shared;
using Shared.Errors;
using Shared.Services;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TasksCoordinator;
using TasksCoordinator.Interface;
using TasksCoordinator.Test;

namespace SSSB
{
    /// <summary>
    /// Message Dispatcher to process messages read from queue.
    /// </summary>
    public class TestSSSBService : ISSSBService
    {
        private static ErrorMessages _errorMessages = new ErrorMessages();
        internal static ILog _log = Log.GetInstance("TestSSSBService");

        #region Private Fields
        private string _name;
        private string _queueName;
        private Dictionary<string, IMessageHandler<ServiceMessageEventArgs>> _messageHandlers;
        private Dictionary<string, IMessageHandler<ErrorMessageEventArgs>> _errorMessageHandlers;
        private volatile bool _isStopped;
        private ITaskCoordinator _tasksCoordinator;

        private BlockingCollection<Message> _MessageQueue;
        private ConcurrentBag<Message> _ProcessedMessages;
        #endregion


        public TestSSSBService(string name, int maxReadersCount, bool isQueueActivationEnabled, bool isEnableParallelReading = false, TaskWorkType workType = TaskWorkType.LongCPUBound)
        {
            _name = name;
            _messageHandlers = new Dictionary<string, IMessageHandler<ServiceMessageEventArgs>>();
            _errorMessageHandlers = new Dictionary<string, IMessageHandler<ErrorMessageEventArgs>>();
            _isStopped = true;
            _MessageQueue = new BlockingCollection<Message>();
            _ProcessedMessages = new ConcurrentBag<Message>();
            var dispatcher = new TestMessageDispatcher(ProcessedMessages, workType);
            var producer = new TestMessageProducer(MessageQueue);
            var readerFactory = new TestMessageReaderFactory();
            _tasksCoordinator = new TestTasksCoordinator(dispatcher, producer, readerFactory,
                maxReadersCount, isQueueActivationEnabled, isEnableParallelReading);
        }

        #region Properties
        public ITaskCoordinator  TasksCoordinator { get => _tasksCoordinator; }

        public bool IsStopped
        {
            get { return _isStopped; }
        }

        public bool IsPaused
        {
            get 
            {
                return this._tasksCoordinator.IsPaused; 
            }
        }

        /// <summary>
        /// Название сервиса.
        /// </summary>
        public string Name
        {
            get { return _name; }
        }

        /// <summary>
        /// Название очереди.
        /// </summary>
        public string QueueName
        {
            get { return _queueName; }
        }
    
        #endregion

        internal void InternalStart()
        {
            try
            {
                _queueName = "test";
                this._tasksCoordinator.Start();
                this.OnStart();
            }
            catch (Exception ex)
            {
                throw new PPSException($"The Service to handle messages from the queue: {this.QueueName} failed to start", ex, _log);
            }
        }

        #region OnEvent Methods
        protected virtual void OnStarting()
        {
        }

        protected virtual void OnStart()
        {
        }

        protected virtual void OnStop()
        {
           
        }
        #endregion

        #region Service Public Methods
        /// <summary>
        /// Запуск сервиса.
        /// Запускается QueueReadersCount читателей очереди сообщений с бесконечным циклом обработки.
        /// </summary>
        public async Task Start()
        {
            this.OnStarting();
            this.InternalStart();
        }

    	/// <summary>
		/// Остановка сервиса.
		/// </summary>
		public void Stop()
		{
            try
            {
                lock (this)
                {
                    _isStopped = true;
                    _tasksCoordinator.Stop().Wait();
                }
            }
            catch (AggregateException ex)
            {
                ex.Flatten().Handle((err) => {
                    if (err is OperationCanceledException)
                    {
                        return true;
                    } else
                    {
                        _log.Error(ex);
                        return true;
                    }
                });
           
            }
            catch (OperationCanceledException )
            {
               
            }
            catch (Exception ex)
            {
                _log.Error(ex);
            }
            this.OnStop();
		}

        /// <summary>
        /// приостанавливает обработку сообщений
        /// </summary>
        public void Pause()
        {
            this._tasksCoordinator.IsPaused = true;
        }

        /// <summary>
        /// возобновляет обработку сообщений, если была приостановлена
        /// </summary>
        public void Resume()
        {
            this._tasksCoordinator.IsPaused = false;
        }
        #endregion

        #region ITaskService Members

        string ITaskService.Name
        {
            get
            {
                return this.Name;
            }
        }


        public IQueueActivator QueueActivator
        {
            get
            {
                return _tasksCoordinator as IQueueActivator;
            }
        }

        public BlockingCollection<Message> MessageQueue { get => _MessageQueue; set => _MessageQueue = value; }
        public ConcurrentBag<Message> ProcessedMessages { get => _ProcessedMessages; set => _ProcessedMessages = value; }

        ErrorMessage ISSSBService.GetError(Guid messageID)
        {
            return _errorMessages.GetError(messageID);
        }

        int ISSSBService.AddError(Guid messageID, Exception err)
        {
            return _errorMessages.AddError(messageID, err);
        }
        #endregion

        // immitate an activator
        public void StartActivator(int delay)
        {
            if (!this._tasksCoordinator.IsQueueActivationEnabled)
                return;
            var task = Activator(delay);
        }


        private int _isActivate = 0;

        public void Activate()
        {
            Interlocked.CompareExchange(ref this._isActivate, 1, 0);
        }

        // immitate an activator
        private async Task Activator(int delay)
        {
            while (true)
            {
                await Task.Delay(delay);
                int isActivated = Interlocked.CompareExchange(ref this._isActivate, 0, 1);
                if (isActivated == 1)
                {
                    bool res = this.QueueActivator.ActivateQueue();
                    Console.WriteLine("activation occured: " + res.ToString());
                }
            }
        }
    }
}