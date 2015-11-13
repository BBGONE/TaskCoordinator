using System;
using System.Threading;
using System.Threading.Tasks;

namespace Shared.Errors
{
    public class ConnectionErrorHandler
    {
        private DateTime? _lastTime;
        private int _counter;

        public ConnectionErrorHandler() {
            this._counter = 0;
        }

        private const int MAX_COUNT = 30;
        private const int MAX_AGE_SEC = 3 * 60;
        public async Task Handle(ILog _log, Exception ex, CancellationToken cancelation)
        {
            lock(this)
            {
                DateTime now = DateTime.Now;
                TimeSpan age = TimeSpan.FromDays(1);
                if (_lastTime.HasValue)
                {
                    age = now - _lastTime.Value;
                }
               
                if (_counter < 5)
                {
                    _log.Error(new Exception("Can not establish a connection to the Database", ex));
                }
                else if (_counter % MAX_COUNT == 0)
                {
                    _log.Critical(new Exception(string.Format("NO connection to the Database after {0} tries", this._counter), ex));
                }
                if (age.TotalSeconds > MAX_AGE_SEC)
                    this._counter = 0;
                else
                    this._counter += 1;
                this._lastTime = now;
            }
            int delay = (_counter % MAX_COUNT) * 1000;
            await Task.Delay(delay, cancelation);
        }
    
    }
}
