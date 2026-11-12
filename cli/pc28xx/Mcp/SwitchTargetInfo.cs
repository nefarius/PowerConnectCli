namespace Nefarius.PowerConnect.Cli.Mcp;

/// <summary>
///     Non-secret description of a configured switch. Tool results use this instead of the target record.
/// </summary>
public sealed class SwitchTargetInfo
{
    public required string Alias { get; init; }

    public required string Host { get; init; }

    public required string User { get; init; }

    public int TimeoutSeconds { get; init; }

    public int LoginRetries { get; init; }

    public double LoginRetryDelaySeconds { get; init; }
}
