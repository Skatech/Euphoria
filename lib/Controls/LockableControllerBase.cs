using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Linq;

using Skatech.IO;

namespace Skatech.Components.Presentation;

abstract class LockableControllerBase : ControllerBase {
    public const string DefaultLockBackground = "#88000000", InfoLockBackground = "#88000044", ErrorLockBackground = "#88880000";
    
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
            // Debug.WriteLine($"Task UNCOMPLETED: '{message}'");
        }
        // else Debug.WriteLine($"Task completed: '{message}'");
        return task;
    }

    protected Task LockWithMessage(string message, int milliseconds = 2000, string background = DefaultLockBackground) {
        return LockUntilComplete(Task.Delay(milliseconds), message, background);
    }

    protected Task LockWithInfoMessage(string message, int milliseconds = 2000) {
        return LockWithMessage(message, milliseconds, InfoLockBackground);
    }

    protected Task LockWithErrorMessage(string message, int milliseconds = 2000) {
        return LockWithMessage(message, milliseconds, ErrorLockBackground);
    }

    readonly static ConcurrentDictionary<string, (string, string)> _drvcache = new(StringComparer.OrdinalIgnoreCase);
    ///<summary>Use zero or negative attempts value as infinity</summary>
    public async ValueTask<bool> LockedDriveCheck(string path, int attempts = 1) {
        var root = _drvcache.Keys.FirstOrDefault(s => path.StartsWith(s,
            StringComparison.OrdinalIgnoreCase)) ?? DriveChecker.Default.GetPathRoot(path);
        
        if (!_drvcache.TryGetValue(root, out (string WaitMessage, string FailMessage) rec))
            _drvcache.TryAdd(root, rec = ($"Awaiting resource {root}...", $"Missing resource {root}"));

        for (int n = 0; attempts < 1 || attempts > n; ++n) {
            if (await LockUntilComplete(DriveChecker.Default.Check(root), rec.WaitMessage))
                return true;

            await LockWithErrorMessage(rec.FailMessage, attempts < 1 || attempts > n + 1
                ? 1000 + (int)DriveChecker.Default.CacheTimeout.TotalMilliseconds : 2000);
        }
        return false;
    }
}