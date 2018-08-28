using Shared;
using Shared.Errors;
using Shared.Services;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TasksCoordinator.Interface;

namespace TasksCoordinator
{
    /// <summary>
    /// используется для регулирования количества слушающих очередь потоков
    /// в случае необходимости освобождает из спячки один поток
    /// </summary>
    public abstract class BaseTasksCoordinator<M> : ITaskCoordinatorAdvanced<M>, IQueueActivator
    {
        private const int MAX_TASK_NUM = int.MaxValue;
        private const int STOP_TIMEOUT = 30000;
        internal static ILog _log = Log.GetInstance("BaseTasksCoordinator");

        private readonly object _lock = new object();
        private readonly object _semaphoreLock = new object();
        private readonly bool _isQueueActivationEnabled;
        private readonly bool _isEnableParallelReading;
        private readonly int _maxReadersCount;
        private volatile int _taskIdSeq;
        private volatile IMessageReader _primaryReader;
        private volatile bool _isStarted;
        private volatile bool _isPaused;
        private int _semaphore;
        private CancellationTokenSource _stopSource;
        private CancellationToken _cancellation;
        private readonly IMessageDispatcher<M> _dispatcher;
        private readonly ConcurrentDictionary<int, Task> _tasks;
        protected readonly IMessageReaderFactory<M> _readerFactory;
        protected readonly IMessageProducer<M> _producer;

        public BaseTasksCoordinator(IMessageDispatcher<M> messageDispatcher, IMessageProducer<M> messageProducer,
            IMessageReaderFactory<M> messageReaderFactory,
            int maxReadersCount, bool isEnableParallelReading = false)
        {
            this._semaphore = 0;
            this._stopSource = null;
            this._cancellation = CancellationToken.None;
            this._dispatcher = messageDispatcher;
            this._producer = messageProducer;
            this._readerFactory = messageReaderFactory;
            this._maxReadersCount = maxReadersCount;
            this._isQueueActivationEnabled = this._producer.IsQueueActivationEnabled;
            this._isEnableParallelReading = isEnableParallelReading;
            this._taskIdSeq = 0;
            this._primaryReader = null;
            this._tasks = new ConcurrentDictionary<int, Task>();
            this._isStarted = false;
        }

        public async Task Start()
        {
            if (this._maxReadersCount == 0)
                return;

            lock (this._lock)
            {
                if (this._isStarted)
                    return;
                this._stopSource = new CancellationTokenSource();
                this._cancellation = this._stopSource.Token;
                this._taskIdSeq = 0;
                this._primaryReader = null;
                this._isStarted = true;
            }
            lock (this._semaphoreLock)
            {
                this._semaphore = this.MaxReadersCount;
            }

            if (!await this.StartNewTask())
                throw new Exception("Can not start the initial task to process messages");
        }

        public async Task Stop()
        {
            lock (this._lock)
            {
                if (!this._isStarted)
                    return;
                this._isStarted = false;
            }

            try
            {
                this._stopSource.Cancel();
                this.IsPaused = false;
                await Task.Delay(1000).ConfigureAwait(false);
                var tasks = this._tasks.ToArray().Select(p => p.Value).ToArray();
                if (tasks.Length > 0)
                {
                    await Task.WhenAny(Task.WhenAll(tasks), Task.Delay(STOP_TIMEOUT)).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                //NOOP
            }
            catch (Exception ex)
            {
                _log.Error(ex);
            }
            finally
            {
                this._tasks.Clear();
                this._stopSource.Dispose();
                this._semaphore = 0;
                this._stopSource = null;
            }
        }

        private async Task<bool> StartNewTask()
        {
            bool result = false;
            bool semaphoreOK = false;
            int taskId = -1;
            try
            {
                var cancellation = this.Cancellation;
                lock (this._semaphoreLock)
                {
                    var newcount = this._semaphore - 1;
                    if (newcount >= 0)
                    {
                        semaphoreOK = true;
                        this._semaphore = newcount;
                    }
                }

                if (semaphoreOK)
                {
                    var dummy = Task.FromResult(0);
                    try
                    {
                        Interlocked.CompareExchange(ref this._taskIdSeq, 0, MAX_TASK_NUM);
                        taskId = Interlocked.Increment(ref this._taskIdSeq);
                        result = this._tasks.TryAdd(taskId, dummy);
                    }
                    catch (Exception)
                    {
                        lock (this._semaphoreLock)
                        {
                            this._semaphore += 1;
                        }
                        Task res;
                        if (result)
                        {
                            this._tasks.TryRemove(taskId, out res);
                        }
                        throw;
                    }
                    var startTask = Task<Task<int>>.Factory.StartNew(() => { return JobRunner(cancellation, taskId);  }, cancellation);
                    this._tasks.TryUpdate(taskId, startTask, dummy);
                    var task = await startTask;
                    this._tasks.TryUpdate(taskId, task, startTask);
                }
            }
            catch (Exception ex)
            {
                result = false;
                Task res;
                if (this._tasks.TryRemove(taskId, out res))
                {
                    lock (this._semaphoreLock)
                    {
                        this._semaphore += 1;
                    }
                }
                if (!(ex is OperationCanceledException))
                {
                    _log.Error(ex);
                }
            }
 
            return result;
        }

        protected IMessageReader GetMessageReader(int taskId)
        {
            return this._readerFactory.CreateReader(taskId, this._producer, this);
        }

        private async Task<int> JobRunner(CancellationToken token, int taskId)
        {
            bool isReaderAdded = false;
            try
            {
                token.ThrowIfCancellationRequested();
                IMessageReader mr = this.GetMessageReader(taskId);
                (this as ITaskCoordinatorAdvanced<M>).AddReader(mr);
                isReaderAdded = true;
                MessageReaderResult readerResult = new MessageReaderResult() { IsRemoved = false, IsWorkDone = false };
                try
                {
                    //Цикл обработки сообщений
                    while (!readerResult.IsRemoved && !token.IsCancellationRequested)
                    {
                        readerResult = await mr.ProcessMessage().ConfigureAwait(false);
                    } // while

                    token.ThrowIfCancellationRequested();
                }
                finally
                {
                    if (isReaderAdded)
                    {
                        (this as ITaskCoordinatorAdvanced<M>).RemoveReader(mr);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (PPSException)
            {
                //already logged
            }
            catch (Exception ex)
            {
                _log.Error(ex);
            }
            finally
            {
                Task res;
                if (this._tasks.TryRemove(taskId, out res))
                {
                    lock (this._semaphoreLock)
                    {
                        this._semaphore += 1;
                    }
                }
            }
  
            return taskId;
        }

        void ITaskCoordinatorAdvanced<M>.StartNewTask()
        {
            this.StartNewTask();
        }


        bool ITaskCoordinatorAdvanced<M>.IsSafeToRemoveReader(IMessageReader reader)
        {
            lock (this._lock)
            {
                return  this._stopSource.IsCancellationRequested || this._isQueueActivationEnabled || !(this as ITaskCoordinatorAdvanced<M>).IsPrimaryReader(reader);
            }
        }

        /// <summary>
        /// снимает с учета слушателя
        /// он может впасть в спячку (ждет события), либо начать обработку сообщения
        /// </summary>
        /// <param name="reader"></param>
        /// <param name="isStartedWorking"></param>
        void ITaskCoordinatorAdvanced<M>.RemoveReader(IMessageReader reader)
        {
            lock (this._lock)
            {
                if (Object.ReferenceEquals(this._primaryReader, reader))
                {
                    this._primaryReader = null;
                }
            }
        }

        /// <summary>
        /// добавляет для учета освободившегося слушателя
        /// он мог освободиться от спячки, либо после обработки сообщения
        /// </summary>
        /// <param name="reader"></param>
        /// <param name="isEndedWorking"></param>
        void ITaskCoordinatorAdvanced<M>.AddReader(IMessageReader reader)
        {
            lock (this._lock)
            {
                if (this._primaryReader == null)
                {
                    this._primaryReader = reader;
                }
            }
        }

        bool ITaskCoordinatorAdvanced<M>.IsPrimaryReader(IMessageReader reader)
        {
            lock (this._lock)
            {
                return this._primaryReader != null && object.ReferenceEquals(this._primaryReader, reader);
            }
        }

        IMessageDispatcher<M> ITaskCoordinatorAdvanced<M>.Dispatcher
        {
            get
            {
                return this._dispatcher;
            }
        }

        #region IQueueActivator
        async Task<bool> IQueueActivator.ActivateQueue()
        {
            if (!this._isQueueActivationEnabled)
                return false;
            lock (this._lock)
            {
                if (!this._isStarted)
                    return false;
            }
            if (this.TasksCount > 0)
            {
                return false;
            }
            return await this.StartNewTask();
        }

        public bool IsQueueActivationEnabled
        {
            get
            {
                return _isQueueActivationEnabled;
            }
        }
        #endregion

       
        /// <summary>
        /// если есть потоки читающие сообщения
        /// то один из них должен быть главным
        /// т.е. использовать WaitFor(RECEIVE( , а не просто RECEIVE(.
        /// </summary>
        public IMessageReader PrimaryReader
        {
            get { return _primaryReader; }
            private set { _primaryReader = value; }
        }

        public int MaxReadersCount
        {
            get {
                return this._maxReadersCount;
            }
        }

        /// <summary>
        /// сколько сечас задач создано
        /// </summary>
        public int TasksCount
        {
            get
            {
                return this._tasks.Count;
            }
        }

        public CancellationToken Cancellation
        {
            get
            {
                return this._stopSource.Token;
            }
        }

        public bool IsEnableParallelReading
        {
            get
            {
                return _isEnableParallelReading;
            }
        }

        public bool IsPaused
        {
            get { return this._isPaused; }
            set { this._isPaused = value; }
        }
    }
}
