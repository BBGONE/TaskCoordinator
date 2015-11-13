using Shared;
using Shared.Errors;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Linq;
using Services.Shared;
using System.Collections.Generic;

namespace TasksCoordinator
{
    /// <summary>
    /// используется для регулирования количества слушающих очередь потоков
    /// в случае необходимости освобождает из спячки один поток
    /// </summary>
    public abstract class BaseTasksCoordinator<M, D>: IQueueActivator
        where D : IMessageDispatcher<M>
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
        private readonly D _messageDispatcher;
        private readonly ConcurrentDictionary<int, Task> _tasks;
        private CancellationTokenSource _stopServiceSource;
        private bool _isStarted;

        public BaseTasksCoordinator(D messageDispatcher, int maxReadersCount, bool isQueueActivationEnabled = false, bool isEnableParallelReading = false)
        {
            this.SyncRoot = new object();
            this._stopServiceSource = null;
            this._messageDispatcher = messageDispatcher;
            this._maxReadersCount = maxReadersCount;
            this._isQueueActivationEnabled = isQueueActivationEnabled;
            this._isEnableParallelReading = isEnableParallelReading;
            this._taskIdSeq = 0;
            this._readersCount = 0;
            this._workingCount = 0;
            this._primaryReader = null;
            this._tasks = new ConcurrentDictionary<int, Task>();
            this._isStarted = false;
        }

        protected abstract IMessageProducer<M> CreateNewMessageProducer();

        protected virtual IMessageReader<M> CreateNewMessageReader(int taskId)
        {
            return new MessageReader<M, D>(taskId, this);
        }

        public void Start()
        {
            lock (this.SyncRoot)
            {
                if (this._isStarted)
                    return;
                this._isStarted = true;
                this._stopServiceSource = new CancellationTokenSource();
                this._taskIdSeq = 0;
                this._readersCount = 0;
                this._workingCount = 0;
                this._primaryReader = null;
                if (this.TasksCount > 0 || this._maxReadersCount == 0)
                    return;
                this.StartNewTask();
            }
        }

        public void Stop()
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
                Thread.Sleep(1000);
                var tasks = this._tasks.ToArray().Select(p => p.Value).ToArray();
                if (tasks.Length > 0)
                    Task.WaitAll(tasks, 30000);
            }
            catch (AggregateException ex)
            {
                ex.Flatten().Handle((err) => {
                    if (err is OperationCanceledException)
                        return true;
                    _log.Error(ex);
                    return true;
                });
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

        private void StartNewTask() {
            CancellationToken token = this.Cancellation;
            Interlocked.CompareExchange(ref this._taskIdSeq, 0, MAX_TASK_NUM);
            int taskId = Interlocked.Increment(ref this._taskIdSeq);
            Task<Task<int>> task = this.CreateNewTask(token, taskId);
            this.OnTaskStart(taskId, task);
        }

        protected IMessageReader<M> GetMessageReader(int taskId)
        {
            return this.CreateNewMessageReader(taskId);
        }

        public IMessageProducer<M> GetMessageProducer()
        {
            return this.CreateNewMessageProducer();
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
                    this.AddReader(mr, false);
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
                        this.RemoveReader(mr, false);
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

        /// <summary>
        /// снимает с учета слушателя
        /// он может впасть в спячку (ждет события), либо начать обработку сообщения
        /// </summary>
        /// <param name="reader"></param>
        /// <param name="isStartedWorking"></param>
        public void RemoveReader(IMessageReader<M> reader, bool isStartedWorking)
        {
            lock (this.SyncRoot)
            {
                int prevCount = this._readersCount;
                int newCount = prevCount - 1;
                int prevWorkingCount = this._workingCount;
                var prevReader = this._primaryReader;

                if (newCount < 0)
                    throw new PPSException(string.Format("ReadersCount must not be equal to {0}", newCount));

                if (Object.ReferenceEquals(this._primaryReader, reader))
                {
                    this._primaryReader = null;
                }

                if (newCount == 0 && this._primaryReader != null)
                    throw new InvalidOperationException("PrimaryReader must not be NULL when no free readers is left");

                if (isStartedWorking)
                    this._workingCount += 1;
                this._readersCount = newCount;

                try
                {
                    if (newCount == 0 && !this.Cancellation.IsCancellationRequested)
                    {
                        int freeCount = this.AvailableCount;
                        int canCreateCount = this.AvailableToCreateCount;
                        if (freeCount == 0 && canCreateCount > 0)
                        {
                            this.StartNewTask();
                        }
                    }
                }
                catch (Exception)
                {
                    this._workingCount = prevWorkingCount;
                    this._readersCount = prevCount;
                    this._primaryReader = prevReader;
                    throw;
                }
            }
        }

        /// <summary>
        /// добавляет для учета освободившегося слушателя
        /// он мог освободиться от спячки, либо после обработки сообщения
        /// </summary>
        /// <param name="reader"></param>
        /// <param name="isEndedWorking"></param>
        public void AddReader(IMessageReader<M> reader, bool isEndedWorking)
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

      
        internal bool IsSafeToRemoveReader(IMessageReader<M> reader)
        {
            lock (this.SyncRoot)
            {
                return this.Cancellation.IsCancellationRequested || this._isQueueActivationEnabled || !this.IsPrimaryReader(reader);
            }
        }

        public bool IsPrimaryReader(IMessageReader<M> reader)
        {
            lock (this.SyncRoot)
            {
                return this._primaryReader != null && object.ReferenceEquals(this._primaryReader, reader);
            }
        }

        #region IQueueActivator
        bool IQueueActivator.ActivateQueue()
        {
            if (!this._isQueueActivationEnabled)
                return false;
            lock (this.SyncRoot)
            {
                if (this.Cancellation.IsCancellationRequested)
                    return false;
                if (this.TasksCount > 0 || this._maxReadersCount == 0)
                    return false;
                try
                {
                    this.StartNewTask();
                }
                catch(Exception ex)
                {
                    _log.Error(ex);
                    return false;
                }
            }
            return true;
        }
        #endregion

        /*
        private bool Lock() {
            bool res= false;
            Monitor.Enter(this.SyncRoot, ref res);
            return res;
        }
        private void UnLock() {
            Monitor.Exit(this.SyncRoot);
        }
        */

        public int MaxReadersCount
        {
            get { return this._maxReadersCount; }
        }

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
        /// сколько сечас задач создано
        /// </summary>
        public int TasksCount
        {
            get
            {
                return this._tasks.Count;
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

        public D MessageDispatcher
        {
            get
            {
                return _messageDispatcher;
            }
        }
    }
}
