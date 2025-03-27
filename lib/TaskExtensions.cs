using System.Threading.Tasks;

public static class TaskExtensions {
    public static void DoNotAwait(this Task task) { }
}