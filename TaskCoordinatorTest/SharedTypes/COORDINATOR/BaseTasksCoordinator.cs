using Shared;
using Shared.Errors;
using Shared.Services;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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
    public abstract class BaseTasksCoordinator<M>: ITaskCoordinatorAdvanced<M>, IQueueActivator
    {
        private static readonly int MAX_TASK_NUM = 100000000;
        internal static ILog _log = Log.GetInstance("TasksCoordinator");

        private readonly object SyncRoot;
        private readonly int _maxReadersCount;
        private int _readersCount;
        private int _workingCount;
        private int _taskIdSeq;
        private readonly bool _isQueueActivationEnabled;
        private readonly bool _isEnableParallelReading;
        private IMessageReader<M> _primaryReader;
        private readonly IMessageDispatcher<M> _messageDispatcher;
        private readonly ConcurrentDictionary<int, Task> _tasks;
        private CancellationTokenSource _stopServiceSource;
        private bool _isStarted;
        private volatile bool _isPaused;
        protected readonly IMessageReaderFactory<M> _messagerReaderFactory;
        protected  readonly IMessageProducer<M> _messageProducer;

        public BaseTasksCoordinator(IMessageDispatcher<M> messageDispatcher, IMessageProducer<M> messageProducer, 
            IMessageReaderFactory<M> messageReaderFactory,
            int maxReadersCount, bool isEnableParallelReading = false)
        {
            this.SyncRoot = new object();
            this._stopServiceSource = null;
            this._messageDispatcher = messageDispatcher;
            this._messageProducer = messageProducer;
            this._messagerReaderFactory = messageReaderFactory;
            this._maxReadersCount = maxReadersCount;
            this._isQueueActivationEnabled = this._messageProducer.IsQueueActivationEnabled;
            this._isEnableParallelReading = isEnableParallelReading;
            this._taskIdSeq = 0;
            this._readersCount = 0;
            this._workingCount = 0;
            this._primaryReader = null;
            this._tasks = new ConcurrentDictionary<int, Task>();
            this._isStarted = false;
        }

        public void Start()
        {
            lock (this.SyncRoot)
            {
                if (this._isStarted)
                    return;
                this._isStarted = true;
                this._stopServiceSource = new CancellationTokenSource();
                this._messageProducer.Cancellation = this._stopServiceSource.Token;
                this._taskIdSeq = 0;
                this._readersCount = 0;
                this._workingCount = 0;
                this._primaryReader = null;
                if (this.TasksCount > 0 || this._maxReadersCount == 0)
                    return;
                if (!this.StartNewTask())
                    throw new Exception("Can not start initial task to process messages");
            }
        }

        public async Task Stop()
        {
            lock (this.SyncRoot)
            {
                if (!this._isStarted)
                    return;
            }
            try
            {
                lock (this.SyncRoot)
                {
                   this._stopServiceSource.Cancel();
                }
                this.IsPaused = false;
                await Task.Delay(1000).ConfigureAwait(false);
                var tasks = this._tasks.ToArray().Select(p => p.Value).ToArray();
                if (tasks.Length > 0)
                {
                    Task[] taskarr = new Task[tasks.Length + 1];
                    tasks.CopyTo(taskarr, 0);
                    taskarr[tasks.Length] = Task.Delay(30000);
                    await Task.WhenAll(taskarr).ConfigureAwait(false);
                }
            }
            catch(OperationCanceledException) { 
               //NOOP
            }
            catch (Exception ex)
            {
                _log.Error(ex);
            }
            finally
            {
                try 
                {
                    this._isStarted = false;
                    if (this._stopServiceSource != null)
                    {
                        this._stopServiceSource.Dispose();
                        this._stopServiceSource = null;
                    }
                }
                catch (Exception ex)
                {
                    _log.Error(ex);
                }
                _tasks.Clear();
            }
        }

        private bool StartNewTask() {
            try
            {
                CancellationToken token = this.Cancellation;
                Interlocked.CompareExchange(ref this._taskIdSeq, 0, MAX_TASK_NUM);
                int taskId = Interlocked.Increment(ref this._taskIdSeq);
                Task<Task<int>> task = this.CreateNewTask(token, taskId);
                this.OnTaskStart(taskId, task);
                return true;
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                _log.Error(ex);
            }
            return false;
        }

        protected IMessageReader<M> GetMessageReader(int taskId)
        {
           return this._messagerReaderFactory.CreateReader(taskId, this._messageProducer, this);
        }

        private Task<Task<int>> CreateNewTask(CancellationToken token, int taskId)
        {
            var res = new Task<Task<int>>(()=> JobRunner(token, taskId), token);
            return res;
        }

        private async Task<int> JobRunner(CancellationToken token, int taskId)
        {
            bool isReaderAdded = false;
            try
            {
                token.ThrowIfCancellationRequested();
                IMessageReader<M> mr = null;
                lock (this.SyncRoot)
                {
                    mr = this.GetMessageReader(taskId);
                    (this as ITaskCoordinatorAdvanced<M>).AddReader(mr, false);
                    isReaderAdded = true;
                }
                try
                {
                    bool doLoop = true;
                    //Цикл обработки сообщений
                    while (doLoop && !token.IsCancellationRequested)
                    {
                        doLoop = await mr.ProcessMessage().ConfigureAwait(false);
                    } // while
                    token.ThrowIfCancellationRequested();
                }
                finally
                {
                    if (isReaderAdded)
                        (this as ITaskCoordinatorAdvanced<M>).RemoveReader(mr, false);
                }
            }
            catch (OperationCanceledException)
            {
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
                this.OnTaskExit(taskId);
            }
            return taskId;
        }

        private void OnTaskStart(int taskId, Task<Task<int>> task)
        {
            if (this._tasks.TryAdd(taskId, task))
            {
                try
                {
                    var badStartAsync = task.ContinueWith((antecedent) =>
                    {
                        lock (this.SyncRoot)
                        {
                            this.OnTaskExit(taskId);
                        }
                    }, TaskContinuationOptions.NotOnRanToCompletion);
                    var goodStartAsync = task.ContinueWith((antecedent) =>
                    {
                        this._tasks.TryUpdate(taskId, antecedent.Result, task);
                    }, TaskContinuationOptions.OnlyOnRanToCompletion);

                    if (!task.IsCanceled)
                    {
                        task.Start();
                    }
                    else
                    {
                        Task tmp;
                        this._tasks.TryRemove(taskId, out tmp);
                    }
                }
                catch (Exception)
                {
                    Task tmp;
                    this._tasks.TryRemove(taskId, out tmp);
                    throw;
                }
            }
        }

        private void OnTaskExit(int taskId)
        {
            lock (this.SyncRoot)
            {
                Task res;
                this._tasks.TryRemove(taskId, out res);
            }
        }

        bool ITaskCoordinatorAdvanced<M>.IsSafeToRemoveReader(IMessageReader<M> reader)
        {
            lock (this.SyncRoot)
            {
                return this._stopServiceSource.IsCancellationRequested || this._isQueueActivationEnabled || !(this as ITaskCoordinatorAdvanced<M>).IsPrimaryReader(reader);
            }
        }

        /// <summary>
        /// снимает с учета слушателя
        /// он может впасть в спячку (ждет события), либо начать обработку сообщения
        /// </summary>
        /// <param name="reader"></param>
        /// <param name="isStartedWorking"></param>
        void ITaskCoordinatorAdvanced<M>.RemoveReader(IMessageReader<M> reader, bool isStartedWorking)
        {
            lock (this.SyncRoot)
            {
                int prevCount = this._readersCount;
                int newCount = prevCount - 1;

                if (newCount < 0)
                    throw new PPSException(string.Format("ReadersCount must not be equal to {0}", newCount));

                if (Object.ReferenceEquals(this._primaryReader, reader))
                {
                    this._primaryReader = null;
                }

                if (newCount == 0 && this._primaryReader != null)
                    throw new InvalidOperationException("The PrimaryReader must be NULL when no free readers is left");

                if (isStartedWorking)
                    this._workingCount += 1;
                this._readersCount = newCount;

                if (newCount == 0 && !this._stopServiceSource.IsCancellationRequested)
                {
                    int freeCount = this.AvailableCount;
                    int canCreateCount = this.AvailableToCreateCount;
                    if (freeCount == 0 && canCreateCount > 0)
                    {
                        this.StartNewTask();
                    }
                }
            }
        }

        /// <summary>
        /// добавляет для учета освободившегося слушателя
        /// он мог освободиться от спячки, либо после обработки сообщения
        /// </summary>
        /// <param name="reader"></param>
        /// <param name="isEndedWorking"></param>
        void ITaskCoordinatorAdvanced<M>.AddReader(IMessageReader<M> reader, bool isEndedWorking)
        {
            lock (this.SyncRoot)
            {
                int prevCount = this._readersCount;
                int newCount = prevCount + 1;

                if (prevCount == 0 && this._primaryReader != null)
                    throw new InvalidOperationException("PrimaryReader must be NULL when no free readers is left");

                if (newCount > this._maxReadersCount)
                    throw new InvalidOperationException(string.Format("ReadersCount must not be equal to {0}", newCount));

                this._readersCount = newCount;

                if (isEndedWorking)
                    this._workingCount -= 1;

                if (this._primaryReader == null)
                {
                    this._primaryReader = reader;
                }
            }
        }
      
        bool ITaskCoordinatorAdvanced<M>.IsPrimaryReader(IMessageReader<M> reader)
        {
            lock (this.SyncRoot)
            {
                return this._primaryReader != null && object.ReferenceEquals(this._primaryReader, reader);
            }
        }

        Task<MessageProcessingResult> IMessageDispatcher<M>.DispatchMessages(IEnumerable<M> messages, WorkContext context, Action<M> onProcessStart)
        {
            return this._messageDispatcher.DispatchMessages(messages, context, onProcessStart);
        }

        #region IQueueActivator
        bool IQueueActivator.ActivateQueue()
        {
            if (!this._isQueueActivationEnabled)
                return false;
            lock (this.SyncRoot)
            {
                if (this._stopServiceSource.IsCancellationRequested)
                    return false;
                if (this.TasksCount > 0 || this._maxReadersCount == 0)
                    return false;
                return this.StartNewTask();
            }
        }
        #endregion

        /// <summary>
        /// сколько сейчас потоков может выполнять работу, если понадобится
        /// </summary>
        private int AvailableCount
        {
            get
            {
                return this.TasksCount - this._workingCount;
            }
        }

        /// <summary>
        /// сколько еще потоков можно создать
        /// </summary>
        private int AvailableToCreateCount
        {
            get
            {
                return this._maxReadersCount - this.TasksCount;
            }
        }

        /// <summary>
        /// если есть потоки читающие сообщения
        /// то один из них должен быть главным
        /// т.е. использовать WaitFor(RECEIVE( , а не просто RECEIVE(.
        /// </summary>
        public IMessageReader<M> PrimaryReader
        {
            get { return _primaryReader; }
            private set { _primaryReader = value; }
        }

        public int MaxReadersCount
        {
            get { return this._maxReadersCount; }
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
                return this._stopServiceSource.Token;
            }
        }

        public bool IsQueueActivationEnabled
        {
            get
            {
                return _isQueueActivationEnabled;
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
