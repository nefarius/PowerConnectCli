using Nefarius.PowerConnect.Ews.Changes;
using Nefarius.PowerConnect.Ews.Http;

namespace Nefarius.PowerConnect.Ews.Tests;

public sealed class ChangePlanTests
{
    [Fact]
    public async Task DryRunDoesNotApply()
    {
        bool applied = false;
        ChangePlan plan = new ChangePlan();
        plan.Add(new ChangeStep(
            "touch nothing",
            false,
            new[] { new FieldDiff("ifAlias$query", "", "lab") },
            _ =>
            {
                applied = true;
                return Task.CompletedTask;
            },
            null));

        List<string> lines = new List<string>();
        int code = await new ChangeExecutor().ExecuteAsync(plan, new ChangeExecutionOptions
        {
            DryRun = true,
            WriteLine = lines.Add
        });

        Assert.Equal(0, code);
        Assert.False(applied);
        Assert.Contains(lines, line => line.Contains("ifAlias$query", StringComparison.Ordinal));
    }

    [Fact]
    public async Task DestructiveChangeRequiresAcknowledgement()
    {
        ChangePlan plan = new ChangePlan();
        plan.Add(new ChangeStep("reset", true, Array.Empty<FieldDiff>(), _ => Task.CompletedTask, null));

        int code = await new ChangeExecutor().ExecuteAsync(plan, new ChangeExecutionOptions
        {
            Yes = true,
            WriteLine = _ => { }
        });

        Assert.Equal(2, code);
    }

    [Fact]
    public void PasswordDiffsAreRedacted()
    {
        FieldDiff diff = new FieldDiff("rlAAALocalUserPassword$query", "secret", "next");

        Assert.Equal("***", diff.Before);
        Assert.Equal("***", diff.After);
        Assert.True(diff.Changed);
    }

    [Theory]
    [InlineData("rlRadiusServerInetKey$add")]
    [InlineData("rlRadiusServerInetKey$query")]
    public void RadiusKeyDiffsAreRedacted(string field)
    {
        FieldDiff diff = new FieldDiff(field, "", "secret");

        Assert.Equal("", diff.Before);
        Assert.Equal("***", diff.After);
        Assert.True(diff.Changed);
    }

    [Fact]
    public async Task ApplyWritesProgressWithoutActivityHook()
    {
        ChangePlan plan = new ChangePlan();
        plan.Add(new ChangeStep("rename", false, Array.Empty<FieldDiff>(), _ => Task.CompletedTask, _ => Task.FromResult<string?>(null)));
        List<string> lines = new List<string>();

        int code = await new ChangeExecutor().ExecuteAsync(plan, new ChangeExecutionOptions
        {
            Yes = true,
            WriteLine = lines.Add
        });

        Assert.Equal(0, code);
        Assert.Contains("Applying: rename", lines);
        Assert.Contains(lines, line => line.StartsWith("Applied ", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ActivityHookWrapsApplyAndVerify()
    {
        bool applied = false;
        bool verified = false;
        List<string> statuses = new List<string>();
        ChangePlan plan = new ChangePlan();
        plan.Add(new ChangeStep(
            "rename",
            false,
            Array.Empty<FieldDiff>(),
            _ =>
            {
                applied = true;
                return Task.CompletedTask;
            },
            _ =>
            {
                verified = true;
                return Task.FromResult<string?>(null);
            }));
        List<string> lines = new List<string>();

        int code = await new ChangeExecutor().ExecuteAsync(plan, new ChangeExecutionOptions
        {
            Yes = true,
            WriteLine = lines.Add,
            ActivityAsync = async (status, operation, token) =>
            {
                statuses.Add(status);
                await operation(token);
            }
        });

        Assert.Equal(0, code);
        Assert.True(applied);
        Assert.True(verified);
        Assert.Equal(new[] { "Applying: rename", "Verifying: rename" }, statuses);
        Assert.DoesNotContain(lines, line => line.StartsWith("Applying:", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.StartsWith("Applied ", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ActivityHookIsNotUsedWhenDeclined()
    {
        bool called = false;
        ChangePlan plan = new ChangePlan();
        plan.Add(new ChangeStep("rename", false, Array.Empty<FieldDiff>(), _ => Task.CompletedTask, null));

        int code = await new ChangeExecutor().ExecuteAsync(plan, new ChangeExecutionOptions
        {
            ConfirmAsync = _ => Task.FromResult(false),
            WriteLine = _ => { },
            ActivityAsync = (_, _, _) =>
            {
                called = true;
                return Task.CompletedTask;
            }
        });

        Assert.Equal(2, code);
        Assert.False(called);
    }

    [Fact]
    public async Task ActivityHookSurfacesReadBackFailure()
    {
        ChangePlan plan = new ChangePlan();
        plan.Add(new ChangeStep(
            "rename",
            false,
            Array.Empty<FieldDiff>(),
            _ => Task.CompletedTask,
            _ => Task.FromResult<string?>("mismatch")));

        SwitchException exception = await Assert.ThrowsAsync<SwitchException>(() => new ChangeExecutor().ExecuteAsync(plan, new ChangeExecutionOptions
        {
            Yes = true,
            WriteLine = _ => { },
            ActivityAsync = (_, operation, token) => operation(token)
        }));

        Assert.Contains("mismatch", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ActivityHookSurfacesApplyFailure()
    {
        ChangePlan plan = new ChangePlan();
        plan.Add(new ChangeStep(
            "rename",
            false,
            Array.Empty<FieldDiff>(),
            _ => throw new InvalidOperationException("post failed"),
            null));

        await Assert.ThrowsAsync<InvalidOperationException>(() => new ChangeExecutor().ExecuteAsync(plan, new ChangeExecutionOptions
        {
            Yes = true,
            WriteLine = _ => { },
            ActivityAsync = async (_, operation, token) => await operation(token)
        }));
    }
}
