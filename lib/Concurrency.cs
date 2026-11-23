using System;
using System.Threading;
using System.Threading.Tasks;

namespace Skatech.Concurrency;

static class TaskExtensions {
    public static void DoNotAwait(this Task task) { }
    public static void DoNotAwait(this ValueTask task) { }
    public static void DoNotAwait<TResult>(this Task<TResult> task) { }
    public static void DoNotAwait<TResult>(this ValueTask<TResult> task) { }
}

class Semaphore {
    readonly SemaphoreSlim _sem;

    public Semaphore(int allowedCount) {
        _sem = new SemaphoreSlim(allowedCount);
    }

    public async ValueTask<IDisposable> WaitAsync() {
        await _sem.WaitAsync();
        return new Cookie(_sem);
    }

    struct Cookie: IDisposable {
        SemaphoreSlim _sem;

        public Cookie(SemaphoreSlim sem) {
            _sem = sem;
        }

        public void Dispose() {
            _sem.Release();
        }
    }
}