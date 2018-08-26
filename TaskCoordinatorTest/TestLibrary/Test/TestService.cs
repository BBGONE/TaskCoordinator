using Shared;
using Shared.Errors;
using Shared.Services;
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using TasksCoordinator.Interface;
using TasksCoordinator.Test.Interface;

namespace TasksCoordinator.Test
{
    /// <summary>
    /// Message Dispatcher to process messages read from queue.
    /// </summary>
    public class TestService : ITaskService
    {
        internal static ILog _log = Log.GetInstance("TestService");

        #region Private Fields
        private string _name;
        private volatile bool _isStopped;
        private ITaskCoordinator _tasksCoordinator;
        private TestMessageDispatcher _dispatcher;
        private BlockingCollection<Message> _messageQueue;
        private ISerializer _serializer;
        #endregion

        public TestService(ISerializer serializer, string name, int maxReadersCount, bool isQueueActivationEnabled, bool isEnableParallelReading = false)
        {
            this._name = name;
            this._isStopped = true;
            this.isQueueActivationEnabled = isQueueActivationEnabled;
            this._serializer = serializer;
            this._messageQueue = new BlockingCollection<Message>();
            this._dispatcher = new TestMessageDispatcher(this._serializer);
            var producer = new TestMessageProducer(this, this._messageQueue);
            var readerFactory = new TestMessageReaderFactory();
            this._tasksCoordinator = new TestTasksCoordinator(this._dispatcher, producer, readerFactory,
                maxReadersCount, isEnableParallelReading);
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
                throw new PPSException($"The Service: {this.Name} failed to start", ex, _log);
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
            catch (OperationCanceledException)
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
                    bool res = await this.QueueActivator.ActivateQueue();
                    Console.WriteLine("activation occured: " + res.ToString());
                }
            }
        }

        #endregion

        public void RegisterCallback(Guid clientID, ICallback callback) {
            this._dispatcher.RegisterCallback(clientID, callback);
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