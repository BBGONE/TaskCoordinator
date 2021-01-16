using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Common.Threading
{
    public static class AsyncHelper
    {
        /// <summary>
        /// Upcasts the result to the base class
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <typeparam name="TResult"></typeparam>
        /// <param name="task"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static async Task<TResult> AsTask<T, TResult>(this Task<T> task)
            where T : TResult
            where TResult : class
        {
            return await task;
        }

        public static Task ForEachAsync<T>(this IEnumerable<T> source, int maxDegreeOfParallelism, Func<T, Task> body)
        {
            return Task.WhenAll(from partition in Partitioner.Create(source).GetPartitions(maxDegreeOfParallelism)
                                select Task.Run(async () =>
                                {
                                    using (partition)
                                        while (partition.MoveNext())
                                            await body(partition.Current);
                                }));
        }

        
        /// <summary>
        /// Configures to run continuation on a custom scheduler
        /// </summary>
        /// <param name="task"></param>
        /// <param name="scheduler"></param>
        /// <returns></returns>
        /// <example>
        /// public static async Task Foo() {
        ///   Console.WriteLine("Foo Started");
        ///   await SomeAsyncTask().ConfigureScheduler(scheduler);
        ///   Console.WriteLine("Foo Finished");
        /// }
        /// </example>
        public static SchedulerAwaitable ConfigureScheduler(this Task task, TaskScheduler scheduler)
        {
            return new SchedulerAwaitable(task, scheduler);
        }
 
        public static SchedulerAwaitable<TResult> ConfigureScheduler<TResult>(this Task<TResult> task, TaskScheduler scheduler)
        {
            return new SchedulerAwaitable<TResult>(task, scheduler);
        }

        public static LongRunningAwaitable ConfigureLongRunning(this Task task)
        {
            return new LongRunningAwaitable(task);
        }

        public struct SchedulerAwaitable
        {
            CustomTaskAwaiter awaitable;

            public SchedulerAwaitable(Task task, TaskScheduler scheduler)
            {
                awaitable = new CustomTaskAwaiter(task, scheduler);
            }

            public CustomTaskAwaiter GetAwaiter() { return awaitable; }

            public readonly struct CustomTaskAwaiter : INotifyCompletion
            {
                readonly Task task;
                readonly TaskScheduler scheduler;
                readonly TaskScheduler currentScheduler;

                public CustomTaskAwaiter(Task task, TaskScheduler scheduler)
                {
                    this.task = task;
                    this.scheduler = scheduler;
                    this.currentScheduler = TaskScheduler.Current;
                }

                private bool IsTheSameScheduler
                {
                    get
                    {
                        return Object.Equals(this.scheduler, this.currentScheduler);
                    }
                }
                public void OnCompleted(Action continuation)
                {
                    Task.Factory.StartNew(continuation, CancellationToken.None, TaskCreationOptions.PreferFairness, scheduler);
                }

                public bool IsCompleted { 
                    get {
                        return IsTheSameScheduler ? task.IsCompleted : false;
                    } 
                }

                public void GetResult() {
                    task.GetAwaiter().GetResult();
                }
            }
        }

        public struct SchedulerAwaitable<T>
        {
            CustomTaskAwaiter<T> awaitable;

            public SchedulerAwaitable(Task<T> task, TaskScheduler scheduler)
            {
                awaitable = new CustomTaskAwaiter<T>(task, scheduler);
            }

            public CustomTaskAwaiter<T> GetAwaiter() { return awaitable; }

            public readonly struct CustomTaskAwaiter<TResult> : INotifyCompletion
            {
                readonly Task<TResult> task;
                readonly TaskScheduler scheduler;
                readonly TaskScheduler currentScheduler;

                public CustomTaskAwaiter(Task<TResult> task, TaskScheduler scheduler)
                {
                    this.task = task;
                    this.scheduler = scheduler;
                    this.currentScheduler = TaskScheduler.Current;
                }

                private bool IsTheSameScheduler
                {
                    get
                    {
                        return Object.Equals(this.scheduler, this.currentScheduler);
                    }
                }

                public void OnCompleted(Action continuation)
                {
                    // Action action = () => { Console.WriteLine($"before continuation .... {Thread.CurrentThread.ManagedThreadId}"); continuation(); Console.WriteLine($"after continuation .... {Thread.CurrentThread.ManagedThreadId}"); };
                    Task.Factory.StartNew(continuation, CancellationToken.None, TaskCreationOptions.PreferFairness, scheduler);
                }

                public bool IsCompleted
                {
                    get
                    {
                        return IsTheSameScheduler ? task.IsCompleted : false;
                    }
                }

                public TResult GetResult() { return task.GetAwaiter().GetResult(); } 
            }
        }

        public struct LongRunningAwaitable
        {
            CustomTaskAwaiter awaitable;

            public LongRunningAwaitable(Task task)
            {
                awaitable = new CustomTaskAwaiter(task);
            }

            public CustomTaskAwaiter GetAwaiter() { return awaitable; }

            public readonly struct CustomTaskAwaiter : INotifyCompletion
            {
                readonly Task task;

                public CustomTaskAwaiter(Task task)
                {
                    this.task = task;
                }

                public void OnCompleted(Action continuation)
                {
                    Task.Factory.StartNew(continuation, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
                }

                public bool IsCompleted
                {
                    get
                    {
                        return  Thread.CurrentThread.IsThreadPoolThread? false: task.IsCompleted;
                    }
                }

                public void GetResult()
                {
                    //propagates exceptions
                    task.GetAwaiter().GetResult();
                }
            }
        }
    }
}
