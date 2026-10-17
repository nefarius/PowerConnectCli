using Nefarius.PowerConnect.Ews.Ews;
using Nefarius.PowerConnect.Ews.Http;

namespace Nefarius.PowerConnect.Ews.Changes;

/// <summary>
///     One field before and after a planned write. Password-like names are redacted for display.
/// </summary>
public sealed class FieldDiff
{
    /// <summary>Creates a diff.</summary>
    public FieldDiff(string field, string before, string after)
    {
        Field = field;
        Changed = !string.Equals(before, after, StringComparison.Ordinal);
        Before = Redact(field, before);
        After = Redact(field, after);
    }

    /// <summary>Form field name.</summary>
    public string Field { get; }

    /// <summary>Value currently on the device.</summary>
    public string Before { get; }

    /// <summary>Value that would be posted.</summary>
    public string After { get; }

    /// <summary>True when the raw values differ. Display values may both be redacted.</summary>
    public bool Changed { get; }

    private static string Redact(string field, string value)
    {
        if (field.Contains("password", StringComparison.OrdinalIgnoreCase) ||
            field.Contains("keystring", StringComparison.OrdinalIgnoreCase) ||
            field.Contains("CommunityString", StringComparison.OrdinalIgnoreCase))
        {
            return value.Length == 0 ? "" : "***";
        }

        return value;
    }
}

/// <summary>
///     A single confirmed write: diff, apply, then read back.
/// </summary>
public sealed class ChangeStep
{
    /// <summary>Creates a step.</summary>
    public ChangeStep(
        string title,
        bool destructive,
        IReadOnlyList<FieldDiff> diffs,
        Func<CancellationToken, Task> applyAsync,
        Func<CancellationToken, Task<string?>>? verifyAsync)
    {
        Title = title;
        Destructive = destructive;
        Diffs = diffs;
        ApplyAsync = applyAsync;
        VerifyAsync = verifyAsync;
    }

    /// <summary>Short description shown in the plan.</summary>
    public string Title { get; }

    /// <summary>Requires <c>--i-understand</c> in addition to confirmation.</summary>
    public bool Destructive { get; }

    /// <summary>Field changes, already redacted.</summary>
    public IReadOnlyList<FieldDiff> Diffs { get; }

    /// <summary>Posts the change.</summary>
    public Func<CancellationToken, Task> ApplyAsync { get; }

    /// <summary>Re-reads the device. Returns an error string, or null when the read-back matches.</summary>
    public Func<CancellationToken, Task<string?>>? VerifyAsync { get; }
}

/// <summary>
///     Ordered writes that are shown and applied only after confirmation.
/// </summary>
public sealed class ChangePlan
{
    private readonly List<ChangeStep> _steps = new List<ChangeStep>();

    /// <summary>Steps in apply order.</summary>
    public IReadOnlyList<ChangeStep> Steps => _steps;

    /// <summary>True when any step is destructive.</summary>
    public bool HasDestructive => _steps.Exists(step => step.Destructive);

    /// <summary>Adds a step.</summary>
    public void Add(ChangeStep step)
    {
        _steps.Add(step);
    }
}

/// <summary>
///     Builds a <see cref="ChangeStep"/> that replays a form.
/// </summary>
public static class FormChange
{
    /// <summary>
    ///     Plans a POST of <paramref name="page"/> with <paramref name="mutations"/>.
    /// </summary>
    public static ChangeStep Post(
        SwitchClient client,
        string title,
        FormPage page,
        IReadOnlyDictionary<string, string> mutations,
        bool destructive = false,
        string? verifyPath = null,
        Func<FormPage, string?>? verify = null,
        IReadOnlyCollection<string>? omit = null)
    {
        List<FieldDiff> diffs = new List<FieldDiff>();
        foreach (KeyValuePair<string, string> pair in mutations)
        {
            string before = page.ValueOf(pair.Key) ?? "";
            diffs.Add(new FieldDiff(pair.Key, before, pair.Value));
        }

        return new ChangeStep(
            title,
            destructive,
            diffs,
            cancellationToken => client.PostFormAsync(page, mutations, cancellationToken, omit),
            verifyPath == null
                ? null
                : async cancellationToken =>
                {
                    FormPage after = await client.GetFormAsync(verifyPath, cancellationToken).ConfigureAwait(false);
                    return verify?.Invoke(after);
                });
    }
}
