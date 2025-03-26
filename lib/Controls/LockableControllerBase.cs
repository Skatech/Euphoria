using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace Skatech.Components.Presentation;

abstract class LockableControllerBase : ControllerBase {
    const string DefaultLockBackground = "#88000000", ErrorLockBackground = "#88880000";
    
    public string? LockMessage { get; private set; }
    public string LockBackground { get; private set; } = DefaultLockBackground;
    public bool LockAnimated => LockMessage?.EndsWith("...") ?? false;

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
                OnPropertyChanged(nameof(LockAnimated));
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

    protected Task LockWithMessage(string message, int milliseconds = 2000, string background = DefaultLockBackground) {
        return LockUntilComplete(Task.Delay(milliseconds), message, background);
    }

    protected void LockWithErrorMessage(string message, int milliseconds = 2000) {
        LockWithMessage(message, milliseconds, ErrorLockBackground);
    }
}