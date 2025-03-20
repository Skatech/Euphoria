using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace Skatech.Components.Presentation;

abstract class LockableControllerBase : ControllerBase {
    public string? LockMessage { get; private set; }
    public string LockBackground { get; private set; } = DefaultLockBackground;

    const string DefaultLockBackground = "#77000000";
    ConcurrentQueue<(Task, string, string)> _queue = new();
    protected TTask LockUntilComplete<TTask>(TTask task, string message, string background = DefaultLockBackground) where TTask : Task {
        void UpdateQueue(Task completedTask) {
            // Debug.WriteLine($"#{Thread.CurrentThread.ManagedThreadId}  Q{_queue.Count}  {(completedTask.IsCompleted ? "END" : "RUN")}  \"{LockMessage ?? "NULL"}\"");
            string? message = null, background = LockBackground;
            while (_queue.TryPeek(out (Task Task, string Message, string Background) rec)) {
                if (rec.Task.IsCompleted is false) {
                    background = rec.Background;
                    message = rec.Message;
                    break;
                }
                else _queue.TryDequeue(out rec);
            }
            if (Object.ReferenceEquals(background, LockBackground) is false) {
                LockBackground = background;
                OnPropertyChanged(nameof(LockBackground));
            }
            if (Object.ReferenceEquals(message, LockMessage) is false) {
                LockMessage = message;
                OnPropertyChanged(nameof(LockMessage));
            }
        }
        if (task.IsCompleted is false) {
            _queue.Enqueue(new(task, message, background));
            task.ContinueWith(UpdateQueue);
            UpdateQueue(task);
        }
        return task;
    }

    // void SetColor(string? color = default) {
    //     ColorTranslator.FromHtml(color ?? "#77000000");
    // }

    // TaskCompletionSource? _tcs;
    // protected bool TryLockWindow(string? message = default) {
    //     if (_tcs is null || _tcs.Task.IsCompleted) {
    //         if (message is not null) {
    //             _tcs = new TaskCompletionSource();
    //             LockUntilCompleteX(_tcs.Task, message);
    //             return true;
    //         }
    //     }
    //     else if (message is null) {
    //         _tcs.SetResult();
    //         return true;
    //     }
    //     return false;
    // }

    // protected async ValueTask<TResult> LockUntilComplete<TResult>(Task<TResult> task, string message) {
    //     LockUntilCompleteX(task, message);
    //     await task;
    //     return task.Result;
    // }

    // string? _lockmsg;
    // public string? LockMessage => _lockmsg;

    // protected bool TryLockWindow(string? message = default) {
    //     var lockmsg = _lockmsg;
    //     if (lockmsg is null || message is null) {
    //         if (Object.ReferenceEquals(lockmsg, Interlocked.CompareExchange(ref _lockmsg, message, lockmsg))) {
    //             OnPropertyChanged(nameof(LockMessage));
    //             return true;
    //         }
    //         else Debug.WriteLine("FAILED TO LOCK WINDOW: " + message ?? "NULL");
    //     }
    //     return false;
    // }

    // public string? LockMessage { get; private set; }

    // protected bool TryLockWindow(string? message = default) {
    //     if (LockMessage is null || message is null) {
    //         LockMessage = message;
    //         OnPropertyChanged(nameof(LockMessage));
    //         return true;
    //     }
    //     return false;
    // }

    // protected async ValueTask<TResult> LockUntilComplete<TResult>(Task<TResult> task, string message) {
    //     while(true) {
    //         if (task.IsCompleted) {
    //             Debug.WriteLine("Completed before lock: " + message);
    //             return task.Result;
    //         }
    //         if (TryLockWindow(message)) {
    //             Debug.WriteLine("Locked window on task: " + message);
    //             break;
    //         }
    //         // await Task.Yield();
    //         await Task.Delay(25);//.ConfigureAwait(false);
    //     }

    //     // #if (DEBUG)
    //     // await Task.Delay(1000);
    //     // #endif
    //     await task;
    //     TryLockWindow();
    //     Debug.WriteLine("Unlocked window on task complete: " + message);
    //     return task.Result;
    // }
}