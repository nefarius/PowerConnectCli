using System.Globalization;

using Nefarius.PowerConnect.Ews.Changes;

namespace Nefarius.PowerConnect.Cli.Mcp;

/// <summary>Prints nothing and posts a plan only when the caller explicitly applies it.</summary>
internal interface IMcpChangeRunner
{
    Task<McpChangeResult> RunAsync(
        ChangePlan plan,
        bool apply,
        bool iUnderstand,
        bool requireAcknowledgement,
        CancellationToken cancellationToken);
}

/// <summary>Turns a <see cref="ChangePlan"/> into a redacted MCP result and enforces apply guards.</summary>
internal sealed class McpChangeRunner : IMcpChangeRunner
{
    public async Task<McpChangeResult> RunAsync(
        ChangePlan plan,
        bool apply,
        bool iUnderstand,
        bool requireAcknowledgement,
        CancellationToken cancellationToken)
    {
        bool acknowledgementRequired = requireAcknowledgement || plan.HasDestructive;
        List<McpChangeStep> steps = plan.Steps.Select(step => new McpChangeStep
        {
            Title = step.Title,
            Destructive = step.Destructive,
            Changes = step.Diffs
                .Where(diff => diff.Changed)
                .Select(diff => new McpFieldChange
                {
                    Field = diff.Field,
                    Before = diff.Before,
                    After = diff.After
                })
                .ToList()
        }).ToList();

        if (plan.Steps.Count == 0)
        {
            return Result("unchanged", applied: false, acknowledgementRequired, "Nothing to change.", steps);
        }

        if (apply && acknowledgementRequired && !iUnderstand)
        {
            return Result(
                "refused",
                applied: false,
                acknowledgementRequired,
                "Refusing to apply a destructive change without i_understand.",
                steps);
        }

        ChangeExecutor executor = new ChangeExecutor();
        int status = await executor.ExecuteAsync(
            plan,
            new ChangeExecutionOptions
            {
                DryRun = !apply,
                Yes = apply,
                IUnderstand = iUnderstand,
                ConfirmAsync = null,
                WriteLine = _ => { }
            },
            cancellationToken).ConfigureAwait(false);

        if (!apply)
        {
            string message = acknowledgementRequired
                ? "Dry run; nothing was written. Applying this change requires i_understand."
                : "Dry run; nothing was written.";
            return Result("planned", applied: false, acknowledgementRequired, message, steps);
        }

        if (status == 2)
        {
            return Result("refused", applied: false, acknowledgementRequired, "Refusing to apply a destructive change without i_understand.", steps);
        }

        return Result(
            "applied",
            applied: true,
            acknowledgementRequired,
            "Applied " + plan.Steps.Count.ToString(CultureInfo.InvariantCulture) + " step(s).",
            steps);
    }

    private static McpChangeResult Result(
        string status,
        bool applied,
        bool acknowledgementRequired,
        string message,
        IReadOnlyList<McpChangeStep> steps)
    {
        return new McpChangeResult
        {
            Status = status,
            Applied = applied,
            Destructive = acknowledgementRequired,
            AcknowledgementRequired = acknowledgementRequired,
            Message = message,
            Steps = steps
        };
    }
}
