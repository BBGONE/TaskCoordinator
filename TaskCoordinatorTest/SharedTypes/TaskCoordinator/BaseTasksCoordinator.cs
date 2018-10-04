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
        private CancellationTokenSource _stopTokenSource;
        private CancellationToken _token;
        private volatile int _maxTasksCount;
        private volatile int _taskIdSeq;
        private volatile int  _isStarted;
        private volatile bool _isPaused;
        private volatile int _tasksCanbeStarted;
        private readonly ConcurrentDictionary<int, Task> _tasks;
        private readonly IMessageReaderFactory<M> _readerFactory;
        private readonly ReadersRegister _readersRegister;

        public BaseTasksCoordinator(IMessageReaderFactory<M> messageReaderFactory,
            int maxTasksCount, bool isQueueActivationEnabled = false, int maxParallelReading = 2)
        {
            this._log = LogFactory.GetInstance("BaseTasksCoordinator");
            this._tasksCanbeStarted = 0;
            this._stopTokenSource = null;
            this._token = CancellationToken.None;
            this._readerFactory = messageReaderFactory;
            this._maxTasksCount = maxTasksCount;
            this._readersRegister = new ReadersRegister(maxParallelReading, isQueueActivationEnabled);
            this._taskIdSeq = 0;
            this._tasks = new ConcurrentDictionary<int, Task>();
            this._isStarted = 0;
        }

        private class ReadersRegister
        {
            private readonly object _primaryReaderLock = new object();
            private readonly int _maxParallelReading;
            private readonly bool _isQueueActivationEnabled;
            private volatile IMessageReader _primaryReader;
            private readonly IMessageReader[] _activeReaders;
            private CancellationToken _token;
            private int _counter;

            public ReadersRegister(int maxParallelReading, bool isQueueActivationEnabled)
            {
                this._maxParallelReading = maxParallelReading;
                this._isQueueActivationEnabled = isQueueActivationEnabled;
                this._primaryReader = null;
                this._token = CancellationToken.None;
                this._counter = 0;
                this._activeReaders = new IMessageReader[this._maxParallelReading];
            }

            public void Register(IMessageReader reader)
            {
                lock (this._primaryReaderLock)
                {
                    if (this._primaryReader == null)
                    {
                        this._primaryReader = reader;
                    }
                    // counter helps to optimize access to the array
                    if (this._counter < this._maxParallelReading)
                    {
                        var arr = this._activeReaders;
                        for (int i = 0; i < arr.Length; ++i)
                        {
                            if (arr[i] == null)
                            {
                                arr[i] = reader;
                                ++this._counter;
                                break;
                            }
                        }
                    }
                }
            }
      
            public void UnRegister(IMessageReader reader)
            {
                lock (this._primaryReaderLock)
                {
                    if (Object.ReferenceEquals(this._primaryReader, reader))
                    {
                        this._primaryReader = null;
                    }

                    if (this._counter > 0)
                    {
                        var arr = this._activeReaders;
                        for (int i = 0; i < arr.Length; ++i)
                        {
                            if (arr[i] != null && Object.ReferenceEquals(arr[i], reader))
                            {
                                arr[i] = null;
                                --this._counter;
                                break;
                            }
                        }
                    }
                }
            }

            public int MaxParallelReading => _maxParallelReading;

            public bool IsQueueActivationEnabled => _isQueueActivationEnabled;

            public int Counter => _counter;

            public int FreeSlots
            {
                get
                {
                    return  this._maxParallelReading - this._counter;
                }
            }

            public bool IsPrimaryReader(IMessageReader reader)
            {
                lock (this._primaryReaderLock)
                {
                    return this._primaryReader != null && object.ReferenceEquals(this._primaryReader, reader);
                }
            }

            public bool IsSafeToRemove(IMessageReader reader, bool workDone)
            {
                bool res = false;
                lock (this._primaryReaderLock)
                {
                    res = this._token.IsCancellationRequested || this._isQueueActivationEnabled || !this.IsPrimaryReader(reader);
                    if (_maxParallelReading > 1 && res && workDone)
                    {
                        var arr = this._activeReaders;
                        for (int i = 0; i < arr.Length; ++i)
                        {
                            if (arr[i] != null && Object.ReferenceEquals(arr[i], reader))
                            {
                                res = false;
                                break;
                            }
                        }
                    }
                }

                return res;
            }

            public void Start(CancellationToken token) {
                this._token = token;
                var arr = this._activeReaders;
                for (int i = 0; i < arr.Length; ++i)
                {
                    arr[i] = null;
                }
                this._counter = 0;
            }
        }

        public bool Start()
        {
            var oldStarted = Interlocked.CompareExchange(ref this._isStarted, 1, 0);
            if (oldStarted == 1)
                return true;
            this._stopTokenSource = new CancellationTokenSource();
            this._token = this._stopTokenSource.Token;
            this._taskIdSeq = 0;
            this._readersRegister.Start(this._stopTokenSource.Token);
            this._tasksCanbeStarted = this._maxTasksCount;
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
                this._tasksCanbeStarted = 0;
            }
        }

        private void _ExitTask(int id)
        {
            Task res;
            if (this._tasks.TryRemove(id, out res))
            {
                Interlocked.Increment(ref this._tasksCanbeStarted);
            }
        }

        private int _TryDecrementTasksCanbeStarted()
        {
            int beforeChanged = this._tasksCanbeStarted;
            if (beforeChanged > 0 && Interlocked.CompareExchange(ref this._tasksCanbeStarted, beforeChanged - 1, beforeChanged) != beforeChanged)
            {
                var spinner = new SpinWait();
                do
                {
                    spinner.SpinOnce();
                    beforeChanged = this._tasksCanbeStarted;
                } while (beforeChanged > 0 && Interlocked.CompareExchange(ref this._tasksCanbeStarted, beforeChanged - 1, beforeChanged) != beforeChanged);
            }
            return beforeChanged;
        }

        private void _TryStartNewTask()
        {
            bool result = false;
            bool semaphoreOK = false;
            int taskId = -1;
            
            try
            {
                int beforeChanged = this._TryDecrementTasksCanbeStarted();
                if (beforeChanged > 0)
                {
                    semaphoreOK = true;
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
                        Interlocked.Increment(ref this._tasksCanbeStarted);
                        Task res;
                        if (result)
                        {
                            this._tasks.TryRemove(taskId, out res);
                        }
                        throw;
                    }

                    var token = this._stopTokenSource.Token;
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
      
        private async Task<int> JobRunner(CancellationToken token, int taskId)
        {
            try
            {
                token.ThrowIfCancellationRequested();
                IMessageReader mr = this.GetMessageReader(taskId);
                this._readersRegister.Register(mr);
                MessageReaderResult readerResult = new MessageReaderResult() { IsRemoved = false, IsWorkDone = false };
                try
                {
                    while (!readerResult.IsRemoved && !token.IsCancellationRequested)
                    {
                        readerResult = await mr.ProcessMessage(token).ConfigureAwait(false);
                    }
                    token.ThrowIfCancellationRequested();
                }
                finally
                {
                    this._readersRegister.UnRegister(mr);
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

        #region  ITaskCoordinatorAdvanced<M>
        void ITaskCoordinatorAdvanced<M>.StartNewTask()
        {
            this._TryStartNewTask();
        }

        bool ITaskCoordinatorAdvanced<M>.IsSafeToRemoveReader(IMessageReader reader, bool workDone)
        {
            bool res = this._readersRegister.IsSafeToRemove(reader, workDone);
            return res  || this._tasksCanbeStarted < 0;
        }

        bool ITaskCoordinatorAdvanced<M>.IsPrimaryReader(IMessageReader reader)
        {
            return this._readersRegister.IsPrimaryReader(reader);
        }

        bool ITaskCoordinatorAdvanced<M>.OnBeforeDoWork(IMessageReader reader)
        {
            //пока обрабатывается сообщение
            //очередь не прослушивается этим потоком
            //пробуем передать эту роль другому свободному потоку
            this._readersRegister.UnRegister(reader);
            this._token.ThrowIfCancellationRequested();
            this._TryStartNewTask();
            return true;
        }

        void ITaskCoordinatorAdvanced<M>.OnAfterDoWork(IMessageReader reader)
        {
            this._readersRegister.Register(reader);
        }
        #endregion

        #region IQueueActivator
        bool IQueueActivator.ActivateQueue()
        {
            if (!this._readersRegister.IsQueueActivationEnabled)
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
                return this._readersRegister.IsQueueActivationEnabled;
            }
        }
        #endregion

        public int MaxTasksCount
        {
            get {
                return this._maxTasksCount;
            }
            set
            {
                if (value < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(MaxTasksCount));
                }

                int diff = value - this._maxTasksCount;
                this._maxTasksCount = value;
                // It can be negative temporarily (before the excess of the tasks stop) 
                int canBeStarted = Interlocked.Add(ref this._tasksCanbeStarted, diff);
                if (canBeStarted > 0 && this.TasksCount == 0)
                {
                    this._TryStartNewTask();
                }
            }
        }

        public int FreeReadersAvailable
        {
            get
            {
                return this._tasksCanbeStarted;
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

        public bool IsPaused
        {
            get { return this._isPaused; }
            set { this._isPaused = value; }
        }
    }
}
