﻿using TSM.Common.Errors;
using TSM.Common.Threading;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace TSM.TasksCoordinator
{
    public class BaseTasksCoordinator : ITaskCoordinatorAdvanced
    {
        private readonly Task NOOP = Task.CompletedTask;
        private const long MAX_TASK_NUM = long.MaxValue;
        private const int STOP_TIMEOUT = 30000;
        private CancellationTokenSource _stopTokenSource;
        private long _taskIdSeq;
        private volatile int _maxTasksCount;
        private volatile int  _isStarted;
        private volatile bool _isPaused;
        private volatile int _tasksCanBeStarted;
        private CancellationToken _cancellationToken;
        private readonly ConcurrentDictionary<long, Task> _tasks;
        private readonly IMessageReaderFactory _readerFactory;
        private volatile IMessageReader _primaryReader;
        private readonly Bottleneck _readBottleNeck;
        private readonly TaskScheduler _taskScheduler = null;
        private readonly TaskFactory _taskFactory;

        public BaseTasksCoordinator(IMessageReaderFactory messageReaderFactory, ILoggerFactory loggerFactory,
            int maxTasksCount, int maxReadParallelism = 4, TaskScheduler taskScheduler = null)
        {
            this.Logger = loggerFactory.CreateLogger<BaseTasksCoordinator>();
            // the current PrimaryReader does not use BottleNeck hence: maxReadParallelism - 1
            int throttleCount = Math.Max(maxReadParallelism - 1, 1);
            this._tasksCanBeStarted = 0;
            this._stopTokenSource = null;
            this._cancellationToken = CancellationToken.None;
            this._readerFactory = messageReaderFactory;
            this._maxTasksCount = maxTasksCount;
            this._taskIdSeq = 0;
            this._tasks = new ConcurrentDictionary<long, Task>();
            this._isStarted = 0;
            this._readBottleNeck = new Bottleneck(throttleCount);
            this._taskScheduler = taskScheduler ?? TaskScheduler.Default;
            this._taskFactory = new TaskFactory(CancellationToken.None, TaskCreationOptions.DenyChildAttach, TaskContinuationOptions.None, this._taskScheduler);
        }

        public bool Start()
        {
            var oldStarted = Interlocked.CompareExchange(ref this._isStarted, 1, 0);
            if (oldStarted == 1)
                return true;
            this._stopTokenSource = new CancellationTokenSource();
            this._cancellationToken = this._stopTokenSource.Token;
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
                await Task.Yield();
                var tasks = this._tasks.Select(p => p.Value).ToArray();
                if (tasks.Length > 0)
                {
                    await Task.WhenAny(Task.WhenAll(tasks), Task.Delay(STOP_TIMEOUT));
                }
            }
            catch (OperationCanceledException)
            {
                //NOOP
            }
            catch (Exception ex)
            {
                Logger.LogError(ErrorHelper.GetFullMessage(ex));
            }
            finally
            {
                this._tasks.Clear();
                this._tasksCanBeStarted = 0;
            }
        }

        private void _ExitTask(long id)
        {
            if (this._tasks.TryRemove(id, out var _))
            {
                Interlocked.Increment(ref this._tasksCanBeStarted);
            }
        }

        private bool _TryDecrementTasksCanBeStarted()
        {
            int beforeChanged;
            do
            {
                beforeChanged = this._tasksCanBeStarted;
            } while (beforeChanged > 0 && Interlocked.CompareExchange(ref this._tasksCanBeStarted, beforeChanged - 1, beforeChanged) != beforeChanged);
            return beforeChanged > 0;
        }

        private bool _TryStartNewTask()
        {
            bool semaphoreOK = false;
            long taskId = -1;
            
            try
            {
                semaphoreOK = this._TryDecrementTasksCanBeStarted();

                if (semaphoreOK)
                {
                    try
                    {
                        Interlocked.CompareExchange(ref this._taskIdSeq, 0, MAX_TASK_NUM);
                        taskId = Interlocked.Increment(ref this._taskIdSeq);
                        this._tasks.TryAdd(taskId, NOOP);
                    }
                    catch (Exception)
                    {
                        Interlocked.Increment(ref this._tasksCanBeStarted);
                        this._tasks.TryRemove(taskId, out var _);
                        throw;
                    }

                    var token = this._stopTokenSource.Token;
                    Task<long> task = _taskFactory.StartNew(() => JobRunner(token, taskId), token).Unwrap();
                    this._tasks.TryUpdate(taskId, task, NOOP);
                    task.ContinueWith((antecedent, id) => {
                        this._ExitTask((long)id);
                        if (antecedent.IsFaulted)
                        {
                            var err = antecedent.Exception;
                            err.Flatten().Handle((ex) => {
                                Logger.LogError(ErrorHelper.GetFullMessage(ex));
                                return true;
                            });
                        }
                    }, taskId, TaskContinuationOptions.NotOnRanToCompletion | TaskContinuationOptions.ExecuteSynchronously);
                }

                return semaphoreOK;
            }
            catch (Exception ex)
            {
                this._ExitTask(taskId);
                if (!(ex is OperationCanceledException))
                {
                    Logger.LogError(ErrorHelper.GetFullMessage(ex));
                }
            }

            return false;
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
                 //   MessageReaderResult readerResult = new MessageReaderResult() { IsRemoved = false, IsWorkDone = false };
                    bool loopAgain = false;
                    do
                    {
                        MessageReaderResult readerResult = await reader.TryProcessMessage(token);
                        loopAgain = !readerResult.IsRemoved && !token.IsCancellationRequested;
                        /*
                        // the task is rescheduled to the threadpool which allows other scheduled tasks to be processed
                        // otherwise it could use exclusively the threadpool thread
                        if (loopAgain) await Task.Yield();
                        */
                    } while (loopAgain);
                }
                finally
                {
                    Interlocked.CompareExchange(ref this._primaryReader, null, reader);
                }
            }
            catch (OperationCanceledException)
            {
                // NOOP
            }
            catch (Exception ex)
            {
                Logger.LogError(ErrorHelper.GetFullMessage(ex));
            }
            finally
            {
                this._ExitTask(taskId);
            }
            return taskId;
        }

        protected ILogger Logger { get; }

        protected IMessageReader GetMessageReader(long taskId)
        {
            return this._readerFactory.CreateReader(taskId, this);
        }

        #region  ITaskCoordinatorAdvanced
        bool ITaskCoordinatorAdvanced.StartNewTask()
        {
            return this._TryStartNewTask();
        }

        bool ITaskCoordinatorAdvanced.IsSafeToRemoveReader(IMessageReader reader, bool workDone)
        {
            if (this._tasksCanBeStarted < 0)
                return true;
            if (workDone)
            {
                return false;
            }
            bool isPrimary = (object)reader == this._primaryReader;
            return !isPrimary;
        }

        bool ITaskCoordinatorAdvanced.IsPrimaryReader(IMessageReader reader)
        {
            return this._primaryReader == (object)reader;
        }

        void ITaskCoordinatorAdvanced.OnBeforeDoWork(IMessageReader reader)
        {
            Interlocked.CompareExchange(ref this._primaryReader, null, reader);
            this._cancellationToken.ThrowIfCancellationRequested();
            this._TryStartNewTask();
        }

        void ITaskCoordinatorAdvanced.OnAfterDoWork(IMessageReader reader)
        {
            Interlocked.CompareExchange(ref this._primaryReader, reader, null);
        }


        struct DummyReleaser : IDisposable
        {
            public static IDisposable Instance = new DummyReleaser();

            public void Dispose()
            {
                // NOOP
            }
        }

        async Task<IDisposable> ITaskCoordinatorAdvanced.ReadThrottleAsync(bool isPrimaryReader)
        {
            if (isPrimaryReader)
                return DummyReleaser.Instance;
            return await this._readBottleNeck.EnterAsync(this._stopTokenSource.Token);
        }

        IDisposable ITaskCoordinatorAdvanced.ReadThrottle(bool isPrimaryReader)
        {
            if (isPrimaryReader)
                return DummyReleaser.Instance;
            return this._readBottleNeck.Enter(this._stopTokenSource.Token);
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

        /// <summary>
        /// how many tasks we have running now
        /// </summary>
        public int TasksCount
        {
            get
            {
                return this._tasks.Count;
            }
        }

        public bool IsPaused
        {
            get { return this._isPaused; }
            set { this._isPaused = value; }
        }

        public CancellationToken Token { get => _stopTokenSource == null? CancellationToken.None: _stopTokenSource.Token; }
    }
}
