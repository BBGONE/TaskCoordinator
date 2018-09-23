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
        internal static ILog Log = Shared.Log.GetInstance("BaseTasksCoordinator");

        private readonly object _lock = new object();
        private readonly object _semaphoreLock = new object();
        private CancellationTokenSource _stopSource;
        private CancellationToken _cancellation;
        private readonly bool _isQueueActivationEnabled;
        private readonly bool _isEnableParallelReading;
        private readonly int _maxReadersCount;
        private volatile int _taskIdSeq;
        private volatile IMessageReader _primaryReader;
        private volatile int  _isStarted;
        private volatile bool _isPaused;
        private int _semaphore;
        private readonly IMessageDispatcher<M> _dispatcher;
        private readonly ConcurrentDictionary<int, Task> _tasks;
        protected readonly IMessageReaderFactory<M> _readerFactory;

        public BaseTasksCoordinator(IMessageDispatcher<M> messageDispatcher, IMessageReaderFactory<M> messageReaderFactory,
            int maxReadersCount, bool isEnableParallelReading = false, bool isQueueActivationEnabled = false)
        {
            this._semaphore = 0;
            this._stopSource = new CancellationTokenSource();
            this._cancellation = this._stopSource.Token;
            this._dispatcher = messageDispatcher;
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
            if (this._maxReadersCount == 0)
                return false;

            var oldStarted = Interlocked.CompareExchange(ref this._isStarted, 1, 0);
            if (oldStarted == 1)
                return true;
            this._taskIdSeq = 0;
            this._primaryReader = null;

            lock (this._semaphoreLock)
            {
                this._semaphore = this.MaxReadersCount;
            }
            this._StartNewTask();
            return true;
        }

        public async Task Stop()
        {
            var oldStarted = Interlocked.CompareExchange(ref this._isStarted, 0, 1);
            if (oldStarted == 0)
                return;

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
                Log.Error(ex);
            }
            finally
            {
                this._tasks.Clear();
                this._semaphore = 0;
                var oldSource = this._stopSource;
                // set new source and token
                this._stopSource = new CancellationTokenSource();
                this._cancellation = this._stopSource.Token;
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

        private void _StartNewTask()
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
                    Task<int> task = Task.Run(async () => await JobRunner(cancellation, taskId), cancellation);
                    this._tasks.TryUpdate(taskId, task, dummy);
                    task.ContinueWith((antecedent, state) => {
                        this._ExitTask((int)state);
                        if (antecedent.IsFaulted)
                        {
                            var err = antecedent.Exception;
                            err.Flatten().Handle((ex) => {
                                Log.Error(ex);
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
                    Log.Error(ex);
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

        protected IMessageReader GetMessageReader(int taskId)
        {
            return this._readerFactory.CreateReader(taskId, this);
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
                        readerResult = await mr.ProcessMessage().ConfigureAwait(false);
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
                Log.Error(ex);
            }
            finally
            {
                this._ExitTask(taskId);
            }
  
            return taskId;
        }

        void ITaskCoordinatorAdvanced<M>.StartNewTask()
        {
            this._StartNewTask();
        }


        bool ITaskCoordinatorAdvanced<M>.IsSafeToRemoveReader(IMessageReader reader)
        {
            lock (this._lock)
            {
                return  this._stopSource.IsCancellationRequested || this._isQueueActivationEnabled || !(this as ITaskCoordinatorAdvanced<M>).IsPrimaryReader(reader);
            }
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
            this._cancellation.ThrowIfCancellationRequested();
            this._StartNewTask();
            return true;
        }

        async Task<MessageProcessingResult> ITaskCoordinatorAdvanced<M>.OnDoWork(M message, object state, int taskId)
        {
            // Console.WriteLine(string.Format("DO WORK {0}", taskId));
            // Console.WriteLine(string.Format("Task: {0} Thread: {1}", taskId, Thread.CurrentThread.ManagedThreadId));
            WorkContext context = new WorkContext(taskId, state, this._cancellation, this);
            var res = await this._dispatcher.DispatchMessage(message, context).ConfigureAwait(false);
            return res;
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
            this._StartNewTask();
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
                return this._cancellation;
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
