using System;
using System.Threading;
using System.Threading.Tasks;
using Shared;
using Shared.Errors;
using Shared.Services;
using Bell.PPS.SSSB;
using Database.Shared;

namespace SSSB
{
    /// <summary>
    /// Сервис для обработки сообщений.
    /// </summary>
    public class BaseSSSBService : ISSSBService
    {
        internal static readonly ILog Log = Shared.Log.GetInstance("BaseSSSBService");
        private static ErrorMessages _errorMessages = new ErrorMessages();

        #region Private Fields
        private string _name;
        private string _queueName;
        private volatile bool _isStopped;
        private CancellationTokenSource _stopStartingSource;
        private SSSBTasksCoordinator _tasksCoordinator;
        private ISSSBDispatcher _dispatcher;
        #endregion

        public BaseSSSBService(string name, int maxReadersCount, bool isQueueActivationEnabled, bool isEnableParallelReading = false)
        {
            _name = name;
            _isStopped = true;
            this.isQueueActivationEnabled = isQueueActivationEnabled;
            this._dispatcher = new SSSBMessageDispatcher(this);
            var readerFactory = new SSSBMessageReaderFactory(this);
            _tasksCoordinator = new SSSBTasksCoordinator(this._dispatcher, readerFactory, maxReadersCount, isEnableParallelReading, this.isQueueActivationEnabled);
        }

        public EventHandler OnStartedEvent;
        public EventHandler OnStoppedEvent;


        internal async Task InternalStart()
        {
            try
            {
                _queueName = await ServiceBrokerHelper.GetServiceQueueName(_name).ConfigureAwait(false);
                if (_queueName == null)
                    throw new PPSException(string.Format(ServiceBrokerResources.ServiceInitializationErrMsg, _name), Log);
                this._tasksCoordinator.Start();
                this.OnStarted();
            }
            catch (Exception ex)
            {
                throw new PPSException(ServiceBrokerResources.StartErrMsg, ex, Log);
            }
        }

        #region OnEvent Methods
        protected virtual void OnStarting()
        {
        }

        protected virtual void OnStarted()
        {
            if (this.OnStartedEvent != null)
                this.OnStartedEvent(this, EventArgs.Empty);
        }

        protected virtual void OnStopped()
        {
            if (this.OnStoppedEvent != null)
                this.OnStoppedEvent(this, EventArgs.Empty);
        }
        #endregion

        #region Windows Service Public Methods
        /// <summary>
		/// Запуск сервиса.
		/// Запускается QueueReadersCount читателей очереди сообщений с бесконечным циклом обработки.
		/// </summary>
		public async Task Start()
        {
            if (!this.IsStopped)
                throw new InvalidOperationException(string.Format("Service: {0} has not finished the execution", this.Name));
            this.OnStarting();
            this._isStopped = false;

            this._stopStartingSource = new CancellationTokenSource();
            CancellationToken ct = this._stopStartingSource.Token;

            var svc = this;
            try
            {
                int i = 0;
                while (!ct.IsCancellationRequested && !ConnectionManager.IsDbConnectionOK())
                {
                    ++i;
                    if (i >= 3 && i <= 7)
                        Log.Error(string.Format("Не удается установить соединение с БД в SSSB сервисе: {0}", this.Name));
                    if ((i % 20) == 0)
                        throw new Exception(string.Format("После 20 попыток не удается установить соединение с БД при запуске сервиса: {0}!", this.Name));
                    await Task.Delay(10000).ConfigureAwait(false);
                }

                ct.ThrowIfCancellationRequested();
                this._stopStartingSource = null;
                await this.InternalStart().ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                this._isStopped = true;
                this._stopStartingSource = null;
            }
            catch (Exception ex)
            {
                Log.Critical(ex);
                this._stopStartingSource.Cancel();
                this._isStopped = true;
                this._stopStartingSource = null;
            }
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
                    if (this._stopStartingSource != null)
                    {
                        this._stopStartingSource.Cancel();
                        this._stopStartingSource = null;
                        return;
                    }
                    _isStopped = true;
                    this.OnStopped();
                    this._tasksCoordinator.Stop().Wait();
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
                //NOOP
            }
            catch (Exception ex)
            {
                Log.Error(ex);
            }
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

        #region Public Methods
        /// <summary>
		/// Регистрация обработчика сообщений заданного типа.
		/// </summary>
		/// <param name="messageType"></param>
		/// <param name="handler"></param>
		public void RegisterMessageHandler(string messageType, IMessageHandler<ServiceMessageEventArgs> handler)
        {
            this._dispatcher.RegisterMessageHandler(messageType, handler);
        }

        /// <summary>
        /// Регистрация обработчика ошибок обработки сообщений заданного типа.
        /// </summary>
        /// <param name="messageType"></param>
        /// <param name="handler"></param>
        public void RegisterErrorMessageHandler(string messageType, IMessageHandler<ErrorMessageEventArgs> handler)
        {
            this._dispatcher.RegisterErrorMessageHandler(messageType, handler);
        }

        /// <summary>
        /// Отмена регистрации обработчика сообщений заданного типа.
        /// </summary>
        /// <param name="messageType"></param>
        public void UnregisterMessageHandler(string messageType)
        {
            this._dispatcher.UnregisterMessageHandler(messageType);
        }

        /// <summary>
        /// Отмена регистрации обработчика сообщений заданного типа.
        /// </summary>
        /// <param name="messageType"></param>
        public void UnregisterErrorMessageHandler(string messageType)
        {
            this._dispatcher.UnregisterErrorMessageHandler(messageType);
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

        ErrorMessage ISSSBService.GetError(Guid messageID)
        {
            return _errorMessages.GetError(messageID);
        }

        int ISSSBService.AddError(Guid messageID, Exception err)
        {
            return _errorMessages.AddError(messageID, err);
        }
        #endregion

        #region Properties
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
    }
}