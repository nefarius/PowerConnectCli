namespace Nefarius.PowerConnect.Cli.Mcp;

/// <summary>
///     Structured result of a planned or applied write. Field values that look like secrets are already redacted.
/// </summary>
public sealed class McpChangeResult
{
    public required string Status { get; init; }

    public bool Applied { get; init; }

    public bool Destructive { get; init; }

    public bool AcknowledgementRequired { get; init; }

    public required string Message { get; init; }

    public required IReadOnlyList<McpChangeStep> Steps { get; init; }
}

/// <summary>One step in an <see cref="McpChangeResult"/>.</summary>
public sealed class McpChangeStep
{
    public required string Title { get; init; }

    public bool Destructive { get; init; }

    public required IReadOnlyList<McpFieldChange> Changes { get; init; }
}

/// <summary>One redacted field difference.</summary>
public sealed class McpFieldChange
{
    public required string Field { get; init; }

    public required string Before { get; init; }

    public required string After { get; init; }
}
