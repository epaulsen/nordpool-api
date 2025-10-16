namespace NordpoolApi.Services;

public class Scheduler : IScheduler
{
    public IDisposable RunOnce(DateTimeOffset time, Func<Task> action)
    {
        var delay = time - DateTimeOffset.Now;
        if (delay < TimeSpan.Zero)
        {
            delay = TimeSpan.Zero;
        }
        
        return RunOnce(delay, action);
    }

    public IDisposable RunOnce(TimeSpan delay, Func<Task> action)
    {
        var cts = new CancellationTokenSource();
        var task = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(delay, cts.Token);
                if (!cts.Token.IsCancellationRequested)
                {
                    await action();
                }
            }
            catch (OperationCanceledException)
            {
                // Task was cancelled, this is expected
            }
        }, cts.Token);

        return new ScheduledTask(cts, task);
    }

    public IDisposable RunEvery(DateTimeOffset startTime, TimeSpan interval, Func<Task> action)
    {
        var cts = new CancellationTokenSource();
        var task = Task.Run(async () =>
        {
            try
            {
                // Wait until start time
                var delay = startTime - DateTimeOffset.Now;
                if (delay > TimeSpan.Zero)
                {
                    await Task.Delay(delay, cts.Token);
                }

                // Execute action repeatedly
                while (!cts.Token.IsCancellationRequested)
                {
                    await action();
                    await Task.Delay(interval, cts.Token);
                }
            }
            catch (OperationCanceledException)
            {
                // Task was cancelled, this is expected
            }
        }, cts.Token);

        return new ScheduledTask(cts, task);
    }

    private class ScheduledTask : IDisposable
    {
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly Task _task;
        private bool _disposed;

        public ScheduledTask(CancellationTokenSource cancellationTokenSource, Task task)
        {
            _cancellationTokenSource = cancellationTokenSource;
            _task = task;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            
            // Cancel the scheduled task
            _cancellationTokenSource.Cancel();
            
            // Wait for the task to complete (with a timeout to avoid blocking indefinitely)
            try
            {
                _task.Wait(TimeSpan.FromSeconds(5));
            }
            catch (AggregateException)
            {
                // Task was cancelled or threw an exception, which is expected
            }

            _cancellationTokenSource.Dispose();
        }
    }
}
