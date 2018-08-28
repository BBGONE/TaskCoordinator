using Shared;
using System;
using System.Threading;
using System.Threading.Tasks;
using TasksCoordinator.Test.Interface;

namespace TasksCoordinator.Test
{
    public class CallbackProxy : ICallback
    {
        private static ILog _log = Log.GetInstance("CallbackProxy");
        private ICallback _callback;
        private volatile int _runningCount;
        
        public CallbackProxy(ICallback callback)
        {
            this._runningCount = 0;
            this._callback = callback;
        }

        public int RunningCount { get => _runningCount; }

        void ICallback.TaskCompleted(Message message, string error)
        {
            Interlocked.Increment(ref _runningCount);
            var task = Task.Factory.StartNew(() => {
                try
                {
                    this._callback.TaskCompleted(message, error);
                }
                catch (Exception ex)
                {
                    if (!(ex is OperationCanceledException))
                    {
                        _log.Error(ex);
                    }
                }
                finally
                {
                    Interlocked.Decrement(ref _runningCount);
                }
            });
        }
    }
}
