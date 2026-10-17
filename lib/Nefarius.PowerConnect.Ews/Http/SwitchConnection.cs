namespace Nefarius.PowerConnect.Ews.Http;

/// <summary>
///     How to reach a PowerConnect 28xx web UI.
/// </summary>
public sealed class SwitchConnection
{
    /// <summary>
    ///     Host or IP. The device speaks plain HTTP only.
    /// </summary>
    public string Host { get; init; } = "192.168.2.17";

    /// <summary>
    ///     Web UI user name.
    /// </summary>
    public string User { get; init; } = "admin";

    /// <summary>
    ///     Web UI password. Sent in cleartext because the device has no HTTPS.
    /// </summary>
    public string Password { get; init; } = "";

    /// <summary>
    ///     Per-request timeout.
    /// </summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    ///     Optional directory that receives redacted request and response captures.
    /// </summary>
    public string? CaptureDirectory { get; init; }

    /// <summary>
    ///     Base URI, always HTTP.
    /// </summary>
    public Uri BaseUri
    {
        get
        {
            string host = Host.Trim();
            if (host.Contains("://", StringComparison.Ordinal))
            {
                return new Uri(host.TrimEnd('/') + "/");
            }

            return new Uri("http://" + host.TrimEnd('/') + "/");
        }
    }
}
