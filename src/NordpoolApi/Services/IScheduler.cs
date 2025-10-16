namespace NordpoolApi.Services;

public interface IScheduler
{
    /// <summary>
    /// Runs specified code once, at the given timestamp
    /// </summary>
    /// <param name="time">The timestamp when the action should run</param>
    /// <param name="action">The asynchronous action to execute</param>
    /// <returns>An IDisposable that can be used to cancel the scheduled task</returns>
    IDisposable RunOnce(DateTimeOffset time, Func<Task> action);

    /// <summary>
    /// Runs specified code once, after a specified delay
    /// </summary>
    /// <param name="delay">The delay before running the action</param>
    /// <param name="action">The asynchronous action to execute</param>
    /// <returns>An IDisposable that can be used to cancel the scheduled task</returns>
    IDisposable RunOnce(TimeSpan delay, Func<Task> action);

    /// <summary>
    /// Runs first time at given timestamp, then every interval afterwards
    /// </summary>
    /// <param name="startTime">The timestamp when the action should first run</param>
    /// <param name="interval">The interval between subsequent executions</param>
    /// <param name="action">The asynchronous action to execute</param>
    /// <returns>An IDisposable that can be used to cancel the scheduled task</returns>
    IDisposable RunEvery(DateTimeOffset startTime, TimeSpan interval, Func<Task> action);
}
