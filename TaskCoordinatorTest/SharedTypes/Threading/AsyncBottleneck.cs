using System;
using System.Threading;
using System.Threading.Tasks;

namespace Shared.Threading
{
    public class AsyncBottleneck
    {
        readonly SemaphoreSlim _semaphore;

        public AsyncBottleneck(int maxParallelOperationsToAllow) => _semaphore = new SemaphoreSlim(maxParallelOperationsToAllow);

        public async Task<IDisposable> Enter(CancellationToken cancellationToken) 
        {
            await _semaphore.WaitAsync(cancellationToken);

            return new Releaser(_semaphore);
        }

        class Releaser : IDisposable
        {
            readonly SemaphoreSlim _semaphore;

            public Releaser(SemaphoreSlim semaphore) => _semaphore = semaphore;

            public void Dispose() => _semaphore.Release();
        }
    }
}