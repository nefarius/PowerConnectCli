using System.Text.Json;

using Nefarius.PowerConnect.Cli.Mcp;
using Nefarius.PowerConnect.Ews.Changes;

namespace Nefarius.PowerConnect.Cli.Tests;

public sealed class McpChangeRunnerTests
{
    private const string Secret = "super-secret-password";

    [Fact]
    public async Task DryRunReturnsTheRedactedPlanWithoutPosting()
    {
        int applied = 0;
        ChangePlan plan = Plan(destructive: true, () => applied++, (field: "userPassword$query", before: Secret, after: "next-secret"));

        McpChangeResult result = await new McpChangeRunner().RunAsync(plan, apply: false, iUnderstand: false, requireAcknowledgement: false, CancellationToken.None);

        Assert.Equal(0, applied);
        Assert.Equal("planned", result.Status);
        Assert.False(result.Applied);
        Assert.True(result.AcknowledgementRequired);
        McpFieldChange change = Assert.Single(Assert.Single(result.Steps).Changes);
        Assert.Equal("***", change.Before);
        Assert.Equal("***", change.After);
        Assert.DoesNotContain(Secret, JsonSerializer.Serialize(result));
        Assert.DoesNotContain("next-secret", JsonSerializer.Serialize(result));
    }

    [Fact]
    public async Task ApplyPostsOnlyAfterAcknowledgementWhenThePlanIsDestructive()
    {
        int applied = 0;
        McpChangeRunner runner = new McpChangeRunner();
        ChangePlan plan = Plan(destructive: true, () => applied++);

        McpChangeResult refused = await runner.RunAsync(plan, apply: true, iUnderstand: false, requireAcknowledgement: false, CancellationToken.None);
        Assert.Equal("refused", refused.Status);
        Assert.False(refused.Applied);
        Assert.Equal(0, applied);

        McpChangeResult posted = await runner.RunAsync(plan, apply: true, iUnderstand: true, requireAcknowledgement: false, CancellationToken.None);
        Assert.Equal("applied", posted.Status);
        Assert.True(posted.Applied);
        Assert.Equal(1, applied);
    }

    [Fact]
    public async Task RawAcknowledgementIsRequiredEvenWhenTheStepIsNotMarkedDestructive()
    {
        int applied = 0;
        ChangePlan plan = Plan(destructive: false, () => applied++);
        McpChangeRunner runner = new McpChangeRunner();

        McpChangeResult refused = await runner.RunAsync(plan, apply: true, iUnderstand: false, requireAcknowledgement: true, CancellationToken.None);
        Assert.Equal("refused", refused.Status);
        Assert.Equal(0, applied);

        McpChangeResult posted = await runner.RunAsync(plan, apply: true, iUnderstand: true, requireAcknowledgement: true, CancellationToken.None);
        Assert.Equal("applied", posted.Status);
        Assert.Equal(1, applied);
    }

    [Fact]
    public async Task EmptyPlanDoesNotPost()
    {
        McpChangeResult result = await new McpChangeRunner().RunAsync(new ChangePlan(), apply: true, iUnderstand: true, requireAcknowledgement: false, CancellationToken.None);

        Assert.Equal("unchanged", result.Status);
        Assert.False(result.Applied);
        Assert.Empty(result.Steps);
    }

    private static ChangePlan Plan(bool destructive, Action apply, params (string field, string before, string after)[] diffs)
    {
        ChangePlan plan = new ChangePlan();
        plan.Add(new ChangeStep(
            "Change",
            destructive,
            diffs.Select(diff => new FieldDiff(diff.field, diff.before, diff.after)).ToList(),
            _ =>
            {
                apply();
                return Task.CompletedTask;
            },
            null));
        return plan;
    }
}
