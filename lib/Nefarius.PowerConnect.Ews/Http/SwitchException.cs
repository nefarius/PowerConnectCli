namespace Nefarius.PowerConnect.Ews.Http;

/// <summary>
///     Failure talking to a PowerConnect web UI, including rejected logins and Document Error pages.
/// </summary>
public sealed class SwitchException : Exception
{
    /// <summary>
    ///     Creates an exception with a message.
    /// </summary>
    public SwitchException(string message)
        : base(message)
    {
    }

    /// <summary>
    ///     Creates an exception with a message and an inner exception.
    /// </summary>
    public SwitchException(string message, Exception inner)
        : base(message, inner)
    {
    }
}
