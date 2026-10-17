namespace Nefarius.PowerConnect.Ews.Changes;

/// <summary>
///     Options for applying a <see cref="ChangePlan"/>.
/// </summary>
public sealed class ChangeExecutionOptions
{
    /// <summary>Skip the confirmation prompt.</summary>
    public bool Yes { get; init; }

    /// <summary>Print the plan and do not post.</summary>
    public bool DryRun { get; init; }

    /// <summary>Required when the plan contains a destructive step.</summary>
    public bool IUnderstand { get; init; }

    /// <summary>
    ///     Asks the operator to confirm. Return true to apply.
    ///     When null, a non-interactive run without <see cref="Yes"/> is rejected.
    /// </summary>
    public Func<ChangePlan, Task<bool>>? ConfirmAsync { get; init; }

    /// <summary>Receives human-readable plan lines.</summary>
    public Action<string>? WriteLine { get; init; }
}

/// <summary>
///     Prints a plan, enforces confirmation, applies each step, and runs read-back checks.
/// </summary>
public sealed class ChangeExecutor
{
    /// <summary>
    ///     Runs the plan. Returns 0 on success, 2 when the operator declined or a guard refused the write.
    /// </summary>
    public async Task<int> ExecuteAsync(
        ChangePlan plan,
        ChangeExecutionOptions options,
        CancellationToken cancellationToken = default)
    {
        Action<string> write = options.WriteLine ?? Console.WriteLine;
        if (plan.Steps.Count == 0)
        {
            write("Nothing to change.");
            return 0;
        }

        write(plan.HasDestructive ? "Destructive change plan:" : "Change plan:");
        foreach (ChangeStep step in plan.Steps)
        {
            write("- " + step.Title + (step.Destructive ? " [destructive]" : ""));
            foreach (FieldDiff diff in step.Diffs.Where(diff => diff.Changed))
            {
                write("    " + diff.Field + ": '" + diff.Before + "' -> '" + diff.After + "'");
            }
        }

        if (options.DryRun)
        {
            write("Dry run; nothing was written.");
            return 0;
        }

        if (plan.HasDestructive && !options.IUnderstand)
        {
            write("Refusing a destructive change without --i-understand.");
            return 2;
        }

        if (!options.Yes)
        {
            if (options.ConfirmAsync == null)
            {
                write("Refusing to write without --yes or an interactive confirmation.");
                return 2;
            }

            bool accepted = await options.ConfirmAsync(plan).ConfigureAwait(false);
            if (!accepted)
            {
                write("Aborted; nothing was written.");
                return 2;
            }
        }

        foreach (ChangeStep step in plan.Steps)
        {
            write("Applying: " + step.Title);
            await step.ApplyAsync(cancellationToken).ConfigureAwait(false);
            if (step.VerifyAsync != null)
            {
                string? error = await step.VerifyAsync(cancellationToken).ConfigureAwait(false);
                if (error != null)
                {
                    throw new Nefarius.PowerConnect.Ews.Http.SwitchException("Read-back failed for '" + step.Title + "': " + error);
                }
            }
        }

        write("Applied " + plan.Steps.Count.ToString(System.Globalization.CultureInfo.InvariantCulture) + " step(s).");
        return 0;
    }
}
