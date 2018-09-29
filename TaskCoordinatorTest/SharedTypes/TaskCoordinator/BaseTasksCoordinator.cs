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
    public class BaseTasksCoordinator<M> : ITaskCoordinatorAdvanced<M>, IQueueActivator
    {
        private const int MAX_TASK_NUM = int.MaxValue;
        private const int STOP_TIMEOUT = 30000;
        private readonly ILog _log;
        private readonly object _lock = new object();
        private readonly object _semaphoreLock = new object();
        private CancellationTokenSource _stopTokenSource;
        private CancellationToken _token;
        private readonly bool _isQueueActivationEnabled;
        private readonly bool _isEnableParallelReading;
        private volatile int _maxReadersCount;
        private volatile int _taskIdSeq;
        private volatile IMessageReader _primaryReader;
        private volatile int  _isStarted;
        private volatile bool _isPaused;
        private volatile int _semaphore;
        private readonly ConcurrentDictionary<int, Task> _tasks;
        private readonly IMessageReaderFactory<M> _readerFactory;

        public BaseTasksCoordinator(IMessageReaderFactory<M> messageReaderFactory,
            int maxReadersCount, bool isEnableParallelReading = false, bool isQueueActivationEnabled = false)
        {
            this._log = LogFactory.GetInstance("BaseTasksCoordinator");
            this._semaphore = 0;
            this._stopTokenSource = new CancellationTokenSource();
            this._token = this._stopTokenSource.Token;
            this._readerFactory = messageReaderFactory;
            this._maxReadersCount = maxReadersCount;
            this._isQueueActivationEnabled = isQueueActivationEnabled;
            this._isEnableParallelReading = isEnableParallelReading;
            this._taskIdSeq = 0;
            this._primaryReader = null;
            this._tasks = new ConcurrentDictionary<int, Task>();
            this._isStarted = 0;
        }

        public bool Start()
        {
            var oldStarted = Interlocked.CompareExchange(ref this._isStarted, 1, 0);
            if (oldStarted == 1)
                return true;
            this._taskIdSeq = 0;
            this._primaryReader = null;

            lock (this._semaphoreLock)
            {
                this._semaphore = this._maxReadersCount;
            }
            this._TryStartNewTask();
            return true;
        }

        public async Task Stop()
        {
            var oldStarted = Interlocked.CompareExchange(ref this._isStarted, 0, 1);
            if (oldStarted == 0)
                return;

            try
            {
                this._stopTokenSource.Cancel();
                this.IsPaused = false;
                await Task.Delay(1000).ConfigureAwait(false);
                var tasks = this._tasks.Select(p => p.Value).ToArray();
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
                this._semaphore = 0;
                var oldSource = this._stopTokenSource;
                // set new source and token
                this._stopTokenSource = new CancellationTokenSource();
                this._token = this._stopTokenSource.Token;
                oldSource.Dispose();
            }
        }

        private void _ExitTask(int id)
        {
            Task res;
            if (this._tasks.TryRemove(id, out res))
            {
                lock (this._semaphoreLock)
                {
                    this._semaphore += 1;
                }
            }
        }

        private void _TryStartNewTask()
        {
            bool result = false;
            bool semaphoreOK = false;
            int taskId = -1;
            
            try
            {
                var token = this.Token;
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
                    Task<int> task = Task.Run(() => JobRunner(token, taskId), token);
                    this._tasks.TryUpdate(taskId, task, dummy);
                    task.ContinueWith((antecedent, state) => {
                        this._ExitTask((int)state);
                        if (antecedent.IsFaulted)
                        {
                            var err = antecedent.Exception;
                            err.Flatten().Handle((ex) => {
                                _log.Error(ex);
                                return true;
                            });
                        }
                    }, taskId, TaskContinuationOptions.NotOnRanToCompletion | TaskContinuationOptions.ExecuteSynchronously);
                }
            }
            catch (Exception ex)
            {
                this._ExitTask(taskId);
                if (!(ex is OperationCanceledException))
                {
                    _log.Error(ex);
                }
            }
        }

        /// <summary>
        /// снимает с учета слушателя
        /// он может впасть в спячку (ждет события), либо начать обработку сообщения
        /// </summary>
        /// <param name="reader"></param>
        private void RemoveReader(IMessageReader reader)
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
        private void AddReader(IMessageReader reader)
        {
            lock (this._lock)
            {
                if (this._primaryReader == null)
                {
                    this._primaryReader = reader;
                }
            }
        }

        private async Task<int> JobRunner(CancellationToken token, int taskId)
        {
            bool isReaderAdded = false;
            try
            {
                token.ThrowIfCancellationRequested();
                IMessageReader mr = this.GetMessageReader(taskId);
                this.AddReader(mr);
                isReaderAdded = true;
                MessageReaderResult readerResult = new MessageReaderResult() { IsRemoved = false, IsWorkDone = false };
                try
                {
                    //Цикл обработки сообщений
                    while (!readerResult.IsRemoved && !token.IsCancellationRequested)
                    {
                        readerResult = await mr.ProcessMessage(token).ConfigureAwait(false);
                    }
                    token.ThrowIfCancellationRequested();
                }
                finally
                {
                    if (isReaderAdded)
                    {
                        this.RemoveReader(mr);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // NOOP
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
                this._ExitTask(taskId);
            }

            return taskId;
        }

        protected ILog Log
        {
            get { return _log; }
        }

        protected IMessageReader GetMessageReader(int taskId)
        {
            return this._readerFactory.CreateReader(taskId, this);
        }

        void ITaskCoordinatorAdvanced<M>.StartNewTask()
        {
            this._TryStartNewTask();
        }

        bool ITaskCoordinatorAdvanced<M>.IsSafeToRemoveReader(IMessageReader reader)
        {
            bool res = false;
            lock (this._lock)
            {
                res = this._stopTokenSource.IsCancellationRequested || this._isQueueActivationEnabled || !(this as ITaskCoordinatorAdvanced<M>).IsPrimaryReader(reader);
            }
            if (res)
                return res;

            lock (this._semaphoreLock)
            {
                res = (this.TasksCount == 1 && this._semaphore < 0);
            }
            return res;
        }

        bool ITaskCoordinatorAdvanced<M>.IsPrimaryReader(IMessageReader reader)
        {
            lock (this._lock)
            {
                return this._primaryReader != null && object.ReferenceEquals(this._primaryReader, reader);
            }
        }

        bool ITaskCoordinatorAdvanced<M>.OnBeforeDoWork(IMessageReader reader)
        {
            //пока обрабатывается сообщение
            //очередь не прослушивается этим потоком
            //пробуем передать эту роль другому свободному потоку
            this.RemoveReader(reader);
            this._token.ThrowIfCancellationRequested();
            this._TryStartNewTask();
            return true;
        }

        void ITaskCoordinatorAdvanced<M>.OnAfterDoWork(IMessageReader reader)
        {
            this.AddReader(reader);
        }

        #region IQueueActivator
        bool IQueueActivator.ActivateQueue()
        {
            if (!this._isQueueActivationEnabled)
                return false;
            var oldStarted = Interlocked.CompareExchange(ref this._isStarted, 1, 1);
            if (oldStarted == 0)
            {
                return false;
            }
            if (this.TasksCount > 0)
            {
                return false;
            }
            this._TryStartNewTask();
            return true;
        }

        public bool IsQueueActivationEnabled
        {
            get
            {
                return _isQueueActivationEnabled;
            }
        }
        #endregion

        public int MaxReadersCount
        {
            get {
                return this._maxReadersCount;
            }
            set
            {
                if (value < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(MaxReadersCount));
                }

                lock (this._semaphoreLock)
                {
                    int diff = value - this._maxReadersCount;
                    this._maxReadersCount = value;
                    // It can be negative temporarily (before the excess of the tasks stop) 
                    this._semaphore += diff;
                    if (this._semaphore > 0 && this.TasksCount == 0)
                    {
                        this._TryStartNewTask();
                    }
                }
            }
        }

        public int FreeReadersAvailable
        {
            get
            {
                return this._semaphore;
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

        public CancellationToken Token
        {
            get
            {
                return this._token;
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
