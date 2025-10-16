using NordpoolApi.Services;
using Xunit;

namespace NordpoolApi.Tests;

public class SchedulerTests
{
    [Fact]
    public async Task RunOnce_WithTimeSpan_ExecutesActionAfterDelay()
    {
        // Arrange
        var scheduler = new Scheduler();
        var executed = false;
        var delay = TimeSpan.FromMilliseconds(100);

        // Act
        using var scheduledTask = scheduler.RunOnce(delay, async () =>
        {
            executed = true;
            await Task.CompletedTask;
        });

        // Wait for the action to execute
        await Task.Delay(delay + TimeSpan.FromMilliseconds(50));

        // Assert
        Assert.True(executed);
    }

    [Fact]
    public async Task RunOnce_WithTimeSpan_CanBeCancelled()
    {
        // Arrange
        var scheduler = new Scheduler();
        var executed = false;
        var delay = TimeSpan.FromMilliseconds(200);

        // Act
        var scheduledTask = scheduler.RunOnce(delay, async () =>
        {
            executed = true;
            await Task.CompletedTask;
        });

        // Cancel immediately
        scheduledTask.Dispose();

        // Wait to ensure action doesn't execute
        await Task.Delay(delay + TimeSpan.FromMilliseconds(50));

        // Assert
        Assert.False(executed);
    }

    [Fact]
    public async Task RunOnce_WithDateTimeOffset_ExecutesActionAtSpecifiedTime()
    {
        // Arrange
        var scheduler = new Scheduler();
        var executed = false;
        var delay = TimeSpan.FromMilliseconds(100);
        var time = DateTimeOffset.Now.Add(delay);

        // Act
        using var scheduledTask = scheduler.RunOnce(time, async () =>
        {
            executed = true;
            await Task.CompletedTask;
        });

        // Wait for the action to execute
        await Task.Delay(delay + TimeSpan.FromMilliseconds(50));

        // Assert
        Assert.True(executed);
    }

    [Fact]
    public async Task RunOnce_WithPastDateTimeOffset_ExecutesImmediately()
    {
        // Arrange
        var scheduler = new Scheduler();
        var executed = false;
        var pastTime = DateTimeOffset.Now.AddMilliseconds(-100);

        // Act
        using var scheduledTask = scheduler.RunOnce(pastTime, async () =>
        {
            executed = true;
            await Task.CompletedTask;
        });

        // Wait a bit to allow the action to execute
        await Task.Delay(TimeSpan.FromMilliseconds(50));

        // Assert
        Assert.True(executed);
    }

    [Fact]
    public async Task RunOnce_WithDateTimeOffset_CanBeCancelled()
    {
        // Arrange
        var scheduler = new Scheduler();
        var executed = false;
        var time = DateTimeOffset.Now.AddMilliseconds(200);

        // Act
        var scheduledTask = scheduler.RunOnce(time, async () =>
        {
            executed = true;
            await Task.CompletedTask;
        });

        // Cancel immediately
        scheduledTask.Dispose();

        // Wait to ensure action doesn't execute
        await Task.Delay(TimeSpan.FromMilliseconds(250));

        // Assert
        Assert.False(executed);
    }

    [Fact]
    public async Task RunEvery_ExecutesActionMultipleTimes()
    {
        // Arrange
        var scheduler = new Scheduler();
        var executionCount = 0;
        var startTime = DateTimeOffset.Now.AddMilliseconds(50);
        var interval = TimeSpan.FromMilliseconds(100);

        // Act
        using var scheduledTask = scheduler.RunEvery(startTime, interval, async () =>
        {
            executionCount++;
            await Task.CompletedTask;
        });

        // Wait for at least 3 executions
        await Task.Delay(TimeSpan.FromMilliseconds(400));

        // Assert
        Assert.True(executionCount >= 3, $"Expected at least 3 executions, but got {executionCount}");
    }

    [Fact]
    public async Task RunEvery_CanBeCancelled()
    {
        // Arrange
        var scheduler = new Scheduler();
        var executionCount = 0;
        var startTime = DateTimeOffset.Now.AddMilliseconds(50);
        var interval = TimeSpan.FromMilliseconds(100);

        // Act
        var scheduledTask = scheduler.RunEvery(startTime, interval, async () =>
        {
            executionCount++;
            await Task.CompletedTask;
        });

        // Wait for a couple of executions
        await Task.Delay(TimeSpan.FromMilliseconds(250));
        var countBeforeCancel = executionCount;

        // Cancel the task
        scheduledTask.Dispose();

        // Wait to ensure no more executions happen
        await Task.Delay(TimeSpan.FromMilliseconds(200));

        // Assert
        Assert.True(executionCount >= 1, "Should have executed at least once");
        Assert.Equal(countBeforeCancel, executionCount);
    }

    [Fact]
    public async Task RunEvery_WithPastStartTime_StartsImmediately()
    {
        // Arrange
        var scheduler = new Scheduler();
        var executionCount = 0;
        var pastTime = DateTimeOffset.Now.AddMilliseconds(-100);
        var interval = TimeSpan.FromMilliseconds(100);

        // Act
        using var scheduledTask = scheduler.RunEvery(pastTime, interval, async () =>
        {
            executionCount++;
            await Task.CompletedTask;
        });

        // Wait for at least 2 executions
        await Task.Delay(TimeSpan.FromMilliseconds(250));

        // Assert
        Assert.True(executionCount >= 2, $"Expected at least 2 executions, but got {executionCount}");
    }

    [Fact]
    public async Task MultipleScheduledTasks_CanRunConcurrently()
    {
        // Arrange
        var scheduler = new Scheduler();
        var task1Executed = false;
        var task2Executed = false;
        var delay = TimeSpan.FromMilliseconds(100);

        // Act
        using var scheduledTask1 = scheduler.RunOnce(delay, async () =>
        {
            task1Executed = true;
            await Task.CompletedTask;
        });

        using var scheduledTask2 = scheduler.RunOnce(delay, async () =>
        {
            task2Executed = true;
            await Task.CompletedTask;
        });

        // Wait for both actions to execute
        await Task.Delay(delay + TimeSpan.FromMilliseconds(50));

        // Assert
        Assert.True(task1Executed);
        Assert.True(task2Executed);
    }

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        // Arrange
        var scheduler = new Scheduler();
        var scheduledTask = scheduler.RunOnce(TimeSpan.FromMilliseconds(1000), async () =>
        {
            await Task.CompletedTask;
        });

        // Act & Assert - should not throw
        scheduledTask.Dispose();
        scheduledTask.Dispose();
        scheduledTask.Dispose();
    }

    [Fact]
    public async Task RunOnce_ActionThrowsException_DoesNotCrashScheduler()
    {
        // Arrange
        var scheduler = new Scheduler();
        var executed = false;
        var delay = TimeSpan.FromMilliseconds(100);

        // Act
        using var scheduledTask = scheduler.RunOnce(delay, async () =>
        {
            executed = true;
            await Task.CompletedTask;
            throw new InvalidOperationException("Test exception");
        });

        // Wait for the action to execute
        await Task.Delay(delay + TimeSpan.FromMilliseconds(50));

        // Assert - action executed despite throwing
        Assert.True(executed);
    }
}
