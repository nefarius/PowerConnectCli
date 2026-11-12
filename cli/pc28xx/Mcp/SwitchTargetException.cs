namespace Nefarius.PowerConnect.Cli.Mcp;

/// <summary>
///     A named switch target could not be loaded or selected. The message never includes a password.
/// </summary>
internal sealed class SwitchTargetException : Exception
{
    public SwitchTargetException(string message)
        : base(message)
    {
    }
}
