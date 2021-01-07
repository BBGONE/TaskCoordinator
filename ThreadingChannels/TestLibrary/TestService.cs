using Microsoft.Extensions.Logging;
using Shared.Errors;
using Shared.Services;
using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using TasksCoordinator.Interface;
using TasksCoordinator.Test.Interface;

namespace TasksCoordinator.Test
{
    /// <summary>
    /// Message Dispatcher to process messages read from queue.
    /// </summary>
    public class TestService : ITaskService, IDisposable
    {
        private readonly ILogger _logger;
       /*
        private static readonly UnboundedChannelOptions s_ChannelOptions = new UnboundedChannelOptions
        {
            SingleWriter = true,
            SingleReader = false,
            AllowSynchronousContinuations = false,
        };
        */

        private static readonly BoundedChannelOptions s_ChannelOptions = new BoundedChannelOptions(100)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleWriter = true,
            SingleReader = false,
            AllowSynchronousContinuations = true,
        };
  
        #region Private Fields
        private volatile bool _isStopped;
        private TestMessageDispatcher<Payload> _messageDispatcher;
        private Channel<Payload> _channel;
        private ChannelWriter<Payload> _messageQueue;
        #endregion

        public event EventHandler<EventArgs> ServiceStarting;
        public event EventHandler<EventArgs> ServiceStarted;
        public event EventHandler<EventArgs> ServiceStopped;

        public TestService(string name, IWorkLoad<Payload> workLoad, ILoggerFactory loggerFactory, int maxReadersCount, 
            int maxReadParallelism = 4)
        {
            this.Name = name;
            this._isStopped = true;
            this._logger = loggerFactory.CreateLogger<TestService>();
            this._channel = Channel.CreateBounded<Payload>(s_ChannelOptions);
            this._messageQueue = this._channel.Writer;
            this._messageDispatcher = new TestMessageDispatcher<Payload>(workLoad);
            var readerFactory = new TestMessageReaderFactory<Payload>(this._channel.Reader, this._messageDispatcher, loggerFactory);
            this.TasksCoordinator = new TestTasksCoordinator(readerFactory, loggerFactory, maxReadersCount, maxReadParallelism);
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
                lock (this)
                {
                    _isStopped = true;
                    this._CompletePost();
                    TasksCoordinator.Stop().Wait();
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

        public async ValueTask<bool> Post(Payload msg, int seqNum, CancellationToken token) {
            await this._messageQueue.WriteAsync(msg, token);
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