using System.Threading.Tasks;

public static class TaskExtensions {
    public static void DoNotAwait(this Task task) { }
    public static void DoNotAwait(this ValueTask task) { }
    public static void DoNotAwait<TResult>(this Task<TResult> task) { }
    public static void DoNotAwait<TResult>(this ValueTask<TResult> task) { }
}