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
        await Task.WhenAll(firstEntered.Task, secondEntered.Task);

        Task<int> queued = gate.RunAsync("CORE", _ => Task.FromResult(2), CancellationToken.None);
        await Task.Delay(50);
        Assert.False(queued.IsCompleted);

        release.TrySetResult();
        Assert.Equal(new[] { 1, 1, 2 }, await Task.WhenAll(core, edge, queued));
    }
}
