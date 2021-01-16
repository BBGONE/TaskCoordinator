using Common;
using Common.Errors;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace TasksCoordinator.Test
{
    /// <summary>
    /// The service which takes messages from the queue and processes them
    /// </summary>
    public class MessageService<TMsg> : ITaskService, IDisposable
    {
        private readonly ILogger _logger;
        
        #region Private Fields
        private volatile bool _isStopped;
        private MessageDispatcher<TMsg> _messageDispatcher;
        private Channel<TMsg> _channel;
        private ChannelWriter<TMsg> _messageQueue;
        #endregion

        public event EventHandler<EventArgs> ServiceStarting;
        public event EventHandler<EventArgs> ServiceStarted;
        public event EventHandler<EventArgs> ServiceStopped;

        public MessageService(string name, 
            IWorkLoad<TMsg> workLoad, 
            ILoggerFactory loggerFactory, 
            int maxDegreeOfParallelism, 
            int maxReadParallelism = 4,
            int? boundedCapacity = null,
            TaskScheduler taskScheduler = null)
        {
            this.Name = name;
            this._isStopped = true;
            this._logger = loggerFactory.CreateLogger<MessageService<TMsg>>();
            if (boundedCapacity == null)
            {
                this._channel = Channel.CreateUnbounded<TMsg>(new UnboundedChannelOptions
                {
                    SingleWriter = false,
                    SingleReader = false,
                    AllowSynchronousContinuations = true,
                });
            }
            else
            {
                this._channel = Channel.CreateBounded<TMsg>(new BoundedChannelOptions(boundedCapacity.Value)
                {
                    FullMode = BoundedChannelFullMode.Wait,
                    SingleWriter = false,
                    SingleReader = false,
                    AllowSynchronousContinuations = true,
                });
            }
            this._messageQueue = this._channel.Writer;
            this._messageDispatcher = new MessageDispatcher<TMsg>(workLoad);
            var readerFactory = new MessageReaderFactory<TMsg>(this._channel.Reader, this._messageDispatcher, loggerFactory);
            this.TasksCoordinator = new TasksCoordinator(readerFactory, loggerFactory, maxDegreeOfParallelism, maxReadParallelism);
        }


        #region Properties
        public ITaskCoordinator TasksCoordinator { get; }

        public bool IsStopped
        {
            get { return _isStopped; }
        }

        public bool IsPaused
        {
            get
            {
                return this.TasksCoordinator.IsPaused;
            }
        }

        /// <summary>
        /// Название сервиса.
        /// </summary>
        public string Name { get; }
        #endregion

        internal void InternalStart()
        {
            try
            {
                this.TasksCoordinator.Start();
                this.OnStart();
            }
            catch (Exception ex)
            {
                _logger.LogError(ErrorHelper.GetFullMessage(ex));
                throw new Exception($"The Service: {this.Name} failed to start", ex);
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
                if (this._isStopped)
                {
                    return;
                }
                lock (this)
                {
                    _isStopped = true;
                    this._CompletePost();
                    TasksCoordinator.Stop().GetAwaiter().GetResult();
                }
            }
            catch (AggregateException ex)
            {
                ex.Flatten().Handle((err) => {
                    if (!(err is OperationCanceledException))
                    {
                        _logger.LogError(ErrorHelper.GetFullMessage(ex));

                    }
                    return true;
                });

            }
            catch (OperationCanceledException)
            {

            }
            catch (Exception ex)
            {
                _logger.LogError(ErrorHelper.GetFullMessage(ex));
            }
            this.OnStop();
        }

        /// <summary>
        /// приостанавливает обработку сообщений
        /// </summary>
        public void Pause()
        {
            this.TasksCoordinator.IsPaused = true;
        }

        /// <summary>
        /// возобновляет обработку сообщений, если была приостановлена
        /// </summary>
        public void Resume()
        {
            this.TasksCoordinator.IsPaused = false;
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
        #endregion

        public async ValueTask<bool> Post(TMsg msg) {
            await this._messageQueue.WriteAsync(msg, this.TasksCoordinator.Token);
            return true;
        }

        private bool _CompletePost()
        {
            return this._messageQueue.TryComplete();
        }

        public void Dispose()
        {
            this._CompletePost();
        }

        public int QueueLength
        {
            get { return -1; }
        }

        public int MaxTasksCount
        {
            get
            {
                return this.TasksCoordinator.MaxTasksCount;
            }
            set
            {
                this.TasksCoordinator.MaxTasksCount = value;
            }
        }

        protected ILogger Logger
        {
            get { return _logger; }
        }
    }
}