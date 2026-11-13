using Nefarius.PowerConnect.Cli.Mcp;

namespace Nefarius.PowerConnect.Cli.Tests;

public sealed class SwitchCallGateTests
{
    [Fact]
    public async Task SerializesOneAliasAndOverlapsDifferentAliases()
    {
        SwitchCallGate gate = new SwitchCallGate();
        TaskCompletionSource firstEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource secondEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        Task<int> core = gate.RunAsync("core", async _ =>
        {
            firstEntered.TrySetResult();
            await release.Task;
            return 1;
        }, CancellationToken.None);
        Task<int> edge = gate.RunAsync("edge", async _ =>
        {
            secondEntered.TrySetResult();
            await release.Task;
            return 1;
        }, CancellationToken.None);
        Task<int>? queued = null;
        try
        {
            Task entered = Task.WhenAll(firstEntered.Task, secondEntered.Task);
            Assert.True(await Task.WhenAny(entered, Task.Delay(TimeSpan.FromSeconds(5))) == entered, "core and edge did not overlap.");

            queued = gate.RunAsync("CORE", _ => Task.FromResult(2), CancellationToken.None);
            await Task.Delay(50);
            Assert.False(queued.IsCompleted);
        }
        finally
        {
            release.TrySetResult();
        }

        Assert.Equal(new[] { 1, 1, 2 }, await Task.WhenAll(core, edge, queued!).WaitAsync(TimeSpan.FromSeconds(5)));
    }
}
