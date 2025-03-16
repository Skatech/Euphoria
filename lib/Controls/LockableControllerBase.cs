using System.Threading.Tasks;

namespace Skatech.Components.Presentation;

abstract class LockableControllerBase : ControllerBase {
    public string? LockMessage { get; private set; }

    protected bool TryLockWindow(string? message = default) {
        if (LockMessage is null || message is null) {
            LockMessage = message;
            OnPropertyChanged(nameof(LockMessage));
            return true;
        }
        return false;
    }

    protected async ValueTask<TResult> LockUntilComplete<TResult>(Task<TResult> task, string message) {
        while(true) {
            if (task.IsCompleted) {
                return task.Result;
            }
            if (TryLockWindow(message)) {
                break;
            }
            await Task.Yield();
        }

        #if (DEBUG)
        await Task.Delay(3000);
        #endif
        await task;
        LockMessage = null;
        OnPropertyChanged(nameof(LockMessage));
        return task.Result;
    }   
}