using Shared;
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
    public class BaseTasksCoordinator : ITaskCoordinatorAdvanced, IQueueActivator
    {
        private const long MAX_TASK_NUM = long.MaxValue;
        private const int STOP_TIMEOUT = 30000;
        private readonly ILog _log;
        private CancellationTokenSource _stopTokenSource;
        private CancellationToken _token;
        private long _taskIdSeq;
        private volatile int _maxTasksCount;
        private volatile int  _isStarted;
        private volatile bool _isPaused;
        private volatile int _tasksCanBeStarted;
        private readonly ConcurrentDictionary<long, Task> _tasks;
        private readonly IMessageReaderFactory _readerFactory;
        private volatile IMessageReader _primaryReader;

        public BaseTasksCoordinator(IMessageReaderFactory messageReaderFactory,
            int maxTasksCount, bool isQueueActivationEnabled = false)
        {
            this._log = LogFactory.GetInstance("BaseTasksCoordinator");
            this._tasksCanBeStarted = 0;
            this._stopTokenSource = null;
            this._token = CancellationToken.None;
            this._readerFactory = messageReaderFactory;
            this._maxTasksCount = maxTasksCount;
            this.IsQueueActivationEnabled = isQueueActivationEnabled;
            this._taskIdSeq = 0;
            this._tasks = new ConcurrentDictionary<long, Task>();
            this._isStarted = 0;
        }

        public bool Start()
        {
            var oldStarted = Interlocked.CompareExchange(ref this._isStarted, 1, 0);
            if (oldStarted == 1)
                return true;
            this._stopTokenSource = new CancellationTokenSource();
            this._token = this._stopTokenSource.Token;
            this._taskIdSeq = 0;
            this._tasksCanBeStarted = this._maxTasksCount;
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
                this._tasksCanBeStarted = 0;
            }
        }

        private void _ExitTask(long id)
        {
            Task res;
            if (this._tasks.TryRemove(id, out res))
            {
                Interlocked.Increment(ref this._tasksCanBeStarted);
            }
        }

        private int _TryDecrementTasksCanbeStarted()
        {
            int beforeChanged = this._tasksCanBeStarted;
            if (beforeChanged > 0 && Interlocked.CompareExchange(ref this._tasksCanBeStarted, beforeChanged - 1, beforeChanged) != beforeChanged)
            {
                var spinner = new SpinWait();
                do
                {
                    spinner.SpinOnce();
                    beforeChanged = this._tasksCanBeStarted;
                } while (beforeChanged > 0 && Interlocked.CompareExchange(ref this._tasksCanBeStarted, beforeChanged - 1, beforeChanged) != beforeChanged);
            }
            return beforeChanged;
        }

        private void _TryStartNewTask()
        {
            bool result = false;
            bool semaphoreOK = false;
            long taskId = -1;
            
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
                        Interlocked.Increment(ref this._tasksCanBeStarted);
                        Task res;
                        if (result)
                        {
                            this._tasks.TryRemove(taskId, out res);
                        }
                        throw;
                    }

                    var token = this._stopTokenSource.Token;
                    Task<long> task = Task.Run(() => JobRunner(token, taskId), token);
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
      
        private async Task<long> JobRunner(CancellationToken token, long taskId)
        {
            try
            {
                token.ThrowIfCancellationRequested();
                IMessageReader reader = this.GetMessageReader(taskId);
                Interlocked.CompareExchange(ref this._primaryReader, reader, null);
                try
                {
                    MessageReaderResult readerResult = new MessageReaderResult() { IsRemoved = false, IsWorkDone = false };
                    while (!readerResult.IsRemoved && !token.IsCancellationRequested)
                    {
                        readerResult = await reader.ProcessMessage(token).ConfigureAwait(false);
                    }
                }
                finally
                {
                    Interlocked.CompareExchange(ref this._primaryReader, null, reader);
                }
                token.ThrowIfCancellationRequested();
            }
            catch (OperationCanceledException)
            {
                // NOOP
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

        protected IMessageReader GetMessageReader(long taskId)
        {
            return this._readerFactory.CreateReader(taskId, this);
        }

        #region  ITaskCoordinatorAdvanced
        void ITaskCoordinatorAdvanced.StartNewTask()
        {
            this._TryStartNewTask();
        }

        bool ITaskCoordinatorAdvanced.IsSafeToRemoveReader(IMessageReader reader, bool workDone)
        {
            bool canRemove = false;
            bool isPrimary = (object)reader == this._primaryReader;
            if (this._token.IsCancellationRequested)
                return true;
            canRemove = this.IsQueueActivationEnabled || !isPrimary;
            return canRemove || this._tasksCanBeStarted < 0;
        }

        bool ITaskCoordinatorAdvanced.IsPrimaryReader(IMessageReader reader)
        {
            return this._primaryReader == (object)reader;
        }

        void ITaskCoordinatorAdvanced.OnBeforeDoWork(IMessageReader reader)
        {
            Interlocked.CompareExchange(ref this._primaryReader, null, reader);
            this._token.ThrowIfCancellationRequested();
            this._TryStartNewTask();
        }

        void ITaskCoordinatorAdvanced.OnAfterDoWork(IMessageReader reader)
        {
            Interlocked.CompareExchange(ref this._primaryReader, reader, null);
        }
        #endregion

        #region IQueueActivator
        bool IQueueActivator.ActivateQueue()
        {
            if (!this.IsQueueActivationEnabled)
                return false;
            if (this._isStarted == 0)
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
            get;
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
                int canBeStarted = Interlocked.Add(ref this._tasksCanBeStarted, diff);
                if (this.TasksCount == 0)
                {
                    this._TryStartNewTask();
                }
            }
        }

        public int FreeReadersAvailable
        {
            get
            {
                return this._tasksCanBeStarted;
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
