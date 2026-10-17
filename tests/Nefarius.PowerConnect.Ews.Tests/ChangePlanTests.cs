using Nefarius.PowerConnect.Ews.Changes;

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
}
