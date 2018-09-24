﻿using Shared;
using Shared.Errors;
using Shared.Services;
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using TasksCoordinator.Callbacks;
using TasksCoordinator.Interface;
using TasksCoordinator.Test.Interface;

namespace TasksCoordinator.Test
{
    /// <summary>
    /// Message Dispatcher to process messages read from queue.
    /// </summary>
    public class TestService : ITaskService
    {
        internal static ILog Log = Shared.Log.GetInstance("TestService");

        #region Private Fields
        private string _name;
        private volatile bool _isStopped;
        private ITaskCoordinator _tasksCoordinator;
        private TestMessageDispatcher _dispatcher;
        private BlockingCollection<Message> _messageQueue;
        private ISerializer _serializer;
        #endregion

        public event EventHandler<EventArgs> ServiceStarting;
        public event EventHandler<EventArgs> ServiceStarted;
        public event EventHandler<EventArgs> ServiceStopped;

        public TestService(ISerializer serializer, string name, int maxReadersCount, bool isQueueActivationEnabled, bool isEnableParallelReading = false)
        {
            this._name = name;
            this._isStopped = true;
            this.isQueueActivationEnabled = isQueueActivationEnabled;
            this._serializer = serializer;
            this._messageQueue = new BlockingCollection<Message>();
            this._dispatcher = new TestMessageDispatcher(this._serializer);
            var readerFactory = new TestMessageReaderFactory(this._messageQueue);
            this._tasksCoordinator = new TestTasksCoordinator(this._dispatcher, readerFactory,
                maxReadersCount, isEnableParallelReading, this.isQueueActivationEnabled);
        }


        #region Properties
        public ITaskCoordinator TasksCoordinator { get => _tasksCoordinator; }

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
        #endregion

        internal void InternalStart()
        {
            try
            {
                this._tasksCoordinator.Start();
                this.OnStart();
            }
            catch (Exception ex)
            {
                throw new PPSException($"The Service: {this.Name} failed to start", ex, Log);
            }
        }

        #region OnEvent Methods
        protected virtual void OnStarting()
        {
            this.ServiceStarting?.Invoke(this, EventArgs.Empty);
        }

        protected virtual void OnStart()
        {
            this.ServiceStarted?.Invoke(this, EventArgs.Empty);
        }

        protected virtual void OnStop()
        {
            this.ServiceStopped?.Invoke(this, EventArgs.Empty);
        }
        #endregion

        #region Service Public Methods
        /// <summary>
        /// Запуск сервиса.
        /// Запускается QueueReadersCount читателей очереди сообщений с бесконечным циклом обработки.
        /// </summary>
        public void Start()
        {
            if (!this._isStopped)
                return;
            _isStopped = false;
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
                    if (!(err is OperationCanceledException))
                    {
                        Log.Error(ex);

                    }
                    return true;
                });

            }
            catch (OperationCanceledException)
            {

            }
            catch (Exception ex)
            {
                Log.Error(ex);
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

        public bool isQueueActivationEnabled { get; private set; }
        #endregion

        #region Immitate Queue Activation

        // immitate an activator
        public void StartActivator(int delay)
        {
            if (!this._tasksCoordinator.IsQueueActivationEnabled)
                return;
            var task = Activator(delay);
        }


        private volatile int _isActivate = 0;

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
                // Console.WriteLine("isActivated: " + isActivated.ToString());
                if (isActivated == 1)
                {
                    bool res = this.QueueActivator.ActivateQueue();
                    Console.WriteLine("activation occured: " + res.ToString());
                }
            }
        }

        #endregion

        public void RegisterCallback(Guid clientID, ICallback<Message> callback) {
            this._dispatcher.RegisterCallback(clientID, new CallbackProxy<Message>(callback, this._tasksCoordinator.Cancellation));
        }

        public bool UnRegisterCallback(Guid clientID)
        {
            return this._dispatcher.UnRegisterCallback(clientID);
        }

        public void AddToQueue<T>(T msg, int num, string msgType) {
            byte[] bytes = this._serializer.Serialize(msg);
            var message = new Message() { SequenceNumber = num, MessageType = msgType, Body= bytes, ServiceName = this.Name };
            this._messageQueue.Add(message);
        }

        public int QueueLength
        {
            get { return this._messageQueue.Count; }
        }
    }
}