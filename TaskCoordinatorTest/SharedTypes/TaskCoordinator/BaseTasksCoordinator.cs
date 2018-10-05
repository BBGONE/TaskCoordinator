﻿using Shared;
using Shared.Errors;
using Shared.Services;
using System;
using System.Collections.Generic;
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
        private readonly IMessageReaderFactory<M> _readerFactory;
        private readonly ReadersRegister _readersRegister;

        public BaseTasksCoordinator(IMessageReaderFactory<M> messageReaderFactory,
            int maxTasksCount, bool isQueueActivationEnabled = false, int maxParallelReading = 2)
        {
            this._log = LogFactory.GetInstance("BaseTasksCoordinator");
            this._tasksCanBeStarted = 0;
            this._stopTokenSource = null;
            this._token = CancellationToken.None;
            this._readerFactory = messageReaderFactory;
            this._maxTasksCount = maxTasksCount;
            this._readersRegister = new ReadersRegister(maxParallelReading, isQueueActivationEnabled);
            this._taskIdSeq = 0;
            this._tasks = new ConcurrentDictionary<long, Task>();
            this._isStarted = 0;
        }

        private class ReadersRegister
        {
            private readonly object _primaryReaderLock = new object();
            private volatile int _maxParallelReading;
            private volatile IMessageReader _primaryReader;
            private readonly ConcurrentDictionary<long, IMessageReader> _activeReaders;
            private CancellationToken _token;
            private volatile int _counter;

            public ReadersRegister(int maxParallelReading, bool isQueueActivationEnabled)
            {
                if (maxParallelReading < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(maxParallelReading));
                }
                this._maxParallelReading = maxParallelReading;
                this.IsQueueActivationEnabled = isQueueActivationEnabled;
                this._primaryReader = null;
                this._token = CancellationToken.None;
                this._counter = 0;
                this._activeReaders = new ConcurrentDictionary<long, IMessageReader>();
            }

            public void Register(IMessageReader reader)
            {
                var prev = Interlocked.CompareExchange(ref this._primaryReader, reader, null);
                lock (this._primaryReaderLock)
                {
                    // if maxParallelReading < 2 then the current primary reader is the only reader
                    if (this._maxParallelReading < 2)
                    {
                        return;
                    }

                    bool hasFreeSlots = this._counter < this._maxParallelReading;
                    if (hasFreeSlots)
                    {
                        if (this._activeReaders.TryAdd(reader.taskId, reader))
                        {
                            ++this._counter;
                        }
                    }
                    else
                    {
                        // If primary reader was set now
                        if (prev == null)
                        {
                            // if No free  slots available ensure we set primary reader to the set of active readers
                            // even if we must replace one slot with it
                            IMessageReader temp;
                            if (!this._activeReaders.TryGetValue(reader.taskId, out temp))
                            {
                                KeyValuePair<long,IMessageReader> kv = this._activeReaders.FirstOrDefault();
                                // taskId starts with 1
                                if (kv.Key > 0)
                                {
                                    // remove the first found active reader
                                    if (this._activeReaders.TryRemove(kv.Key, out temp))
                                    {
                                        --this._counter;

                                        // and replace it with the primary reader
                                        if (this._activeReaders.TryAdd(reader.taskId, reader))
                                        {
                                            ++this._counter;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
      
            public void UnRegister(IMessageReader reader)
            {
                Interlocked.CompareExchange(ref this._primaryReader, null, reader);
                lock (this._primaryReaderLock)
                {
                    if (this._counter > 0)
                    {
                        IMessageReader temp;
                        if (this._activeReaders.TryRemove(reader.taskId, out temp))
                        {
                            --this._counter;
                        }
                    }
                }
            }

            public bool IsQueueActivationEnabled { get; }

            public int Counter => _counter;

            public int MaxParallelReading
            {
                get { return _maxParallelReading; }
                set
                {
                    if (value < 0)
                    {
                        throw new ArgumentOutOfRangeException(nameof(MaxParallelReading));
                    }

                    lock (this._primaryReaderLock)
                    {
                        _maxParallelReading = value;
                    }
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
                bool canRemove = false;
                lock (this._primaryReaderLock)
                {
                    canRemove = this._token.IsCancellationRequested || this.IsQueueActivationEnabled || !this.IsPrimaryReader(reader);
                    if (canRemove && workDone)
                    {
                        IMessageReader temp;
                        if (this._activeReaders.TryGetValue(reader.taskId, out temp))
                            canRemove = false;
                    }
                }

                return canRemove;
            }

            public void Start(CancellationToken token) {
                this._token = token;
                this._activeReaders.Clear();
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

        protected IMessageReader GetMessageReader(long taskId)
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
            return res  || this._tasksCanBeStarted < 0;
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
            get
            {
                return this._readersRegister.IsQueueActivationEnabled;
            }
        }
        #endregion

        public int MaxParallelReading
        {
            get
            {
                return this._readersRegister.MaxParallelReading;
            }
            set
            {
                this._readersRegister.MaxParallelReading = value;
            }
        }
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
