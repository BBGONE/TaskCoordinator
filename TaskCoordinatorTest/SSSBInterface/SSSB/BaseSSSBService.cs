using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Transactions;
using Shared;
using Shared.Errors;
using Shared.Services;
using TasksCoordinator;

namespace SSSB
{
    /// <summary>
    /// Message Dispatcher to process messages read from queue.
    /// </summary>
    public class BaseSSSBService : ITaskService, IMessageDispatcher<Message>
    {
        #region Private Fields
        internal static ILog _log = Log.GetInstance("SSSB");
        private static ErrorMessages _errorMessages = new ErrorMessages();
        private string _name;
        private string _queueName;
        private int _queueReadersCount;
        private Dictionary<string, IMessageHandler<ServiceMessageEventArgs>> _messageHandlers;
        private Dictionary<string, IMessageHandler<ErrorMessageEventArgs>> _errorMessageHandlers;
        private volatile bool _isStopped;
        private volatile bool _isPaused;
        private TestTasksCoordinator _tasksCoordinator;
        #endregion

        #region TEST RESULTS
        public static ConcurrentBag<Message> _processedMessages = new ConcurrentBag<Message>();
        public static ConcurrentBag<Message> ProcessedMessages
        {
            get { return _processedMessages; }
        }
        #endregion

        public BaseSSSBService(string name, int queueReadersCount)
        {
            _name = name;
            _queueReadersCount = queueReadersCount;
            _messageHandlers = new Dictionary<string, IMessageHandler<ServiceMessageEventArgs>>();
            _errorMessageHandlers = new Dictionary<string, IMessageHandler<ErrorMessageEventArgs>>();
            _isStopped = true;
            _isPaused = false;
            this._tasksCoordinator = new TestTasksCoordinator(this, this._queueReadersCount, isQueueActivationEnabled: true, isEnableParallelReading: false);
        }

        #region Properties
        public bool IsStopped
        {
            get { return _isStopped; }
        }

        public bool IsPaused
        {
            get 
            {
                return _isPaused; 
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
           
        /// <summary>
        /// Число одновременно запускаемых читателей очереди.
        /// </summary>
        public int QueueReadersCount
        {
            get { return _queueReadersCount; }
        }
        #endregion
          
        private async Task<bool> DispatchMessage(Message message, CancellationToken cancellation)
        {
            //возвратить ли сообщение назад в очередь?
            bool rollBack = false; 
            
            bool stress = true;
            if (stress)
            {
                /*
                //Testing long running task - позволяет запустить длительные задачи
                Task task = new Task(() => {
                    CPU_TASK(message, cancellation);
                }, cancellation, TaskCreationOptions.LongRunning);
                cancellation.ThrowIfCancellationRequested();
                if (!task.IsCanceled)
                {
                    task.Start();
                    await task;
                }
                */

                CPU_TASK(message, cancellation);
            }
            else
                await Task.Delay(500, cancellation);

            //if (!ProcessedMessages.Contains(message))
                ProcessedMessages.Add(message);
            Console.WriteLine(string.Format("{0} - {1} - {2} - {3}", message.SequenceNumber, message.ServiceName, Thread.CurrentThread.ManagedThreadId, this._tasksCoordinator.TasksCount));
            return rollBack;
        }

        private static void CPU_TASK(Message message, CancellationToken cancellation) {
            Random rnd = new Random();
            int cnt = rnd.Next(1000000, 2000000);
            for (int i = 0; i < cnt; ++i)
            {
                cancellation.ThrowIfCancellationRequested();
                //rollBack = !rollBack;
                message.Body = System.Text.Encoding.UTF8.GetBytes(string.Format("i={0}---cnt={1}", i, cnt));
            }
        }

        internal static int AddError(Guid messageID, Exception err)
        {
            return BaseSSSBService._errorMessages.AddError(messageID, err);
        }

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
                throw new PPSException(ServiceBrokerResources.StartErrMsg, ex, _log);
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
        public void Start()
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
                    _isPaused = false;
                    _tasksCoordinator.Stop();
                }
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
            this._isPaused = true;
        }

        /// <summary>
        /// возобновляет обработку сообщений, если была приостановлена
        /// </summary>
        public void Resume()
        {
            this._isPaused = false;
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
                return _tasksCoordinator;
            }
        }
        #endregion

        //immitate an activator
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

        //immitate an activator
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

        async Task<MessageProcessingResult> IMessageDispatcher<Message>.DispatchMessages(IEnumerable<Message> messages, WorkContext context, Action<Message> onProcessStart)
        {
            bool rollBack = false;
            Message currentMessage = null;
            onProcessStart(currentMessage);

            //SSSBMessageProducer передает Connection в объекте state
            //var dbconnection = context.state as SqlConnection;
            try
            {
                //Как бы обработка сообщений
                foreach (Message message in messages)
                {
                    currentMessage = message;
                    onProcessStart(currentMessage);
                    rollBack = await this.DispatchMessage(message, context.cancellation);

                    if (rollBack)
                        break;
                }
            }
            finally
            {
                onProcessStart(null);
            }
            return new MessageProcessingResult() { isRollBack = rollBack };
        }
    }
}