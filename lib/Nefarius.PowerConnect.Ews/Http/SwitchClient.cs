using System.Net;
using System.Text.RegularExpressions;

using Nefarius.PowerConnect.Ews.Ews;

using Serilog;

namespace Nefarius.PowerConnect.Ews.Http;

/// <summary>
///     HTTP session against a PowerConnect 28xx GoAhead web UI.
/// </summary>
public sealed class SwitchClient : IAsyncDisposable
{
    private static readonly Regex PasswordField = new(
        @"(password(?:\$[A-Za-z0-9?]*)?=)[^&]*",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly HttpClient _http;
    private readonly SwitchConnection _connection;
    private readonly ILogger _logger;
    private int _captureIndex;

    private SwitchClient(HttpClient http, SwitchConnection connection, ILogger logger)
    {
        _http = http;
        _connection = connection;
        _logger = logger;
    }

    /// <summary>
    ///     Logs in and returns a session. The password is not written to logs or capture files.
    /// </summary>
    public static async Task<SwitchClient> ConnectAsync(
        SwitchConnection connection,
        ILogger? logger = null,
        HttpMessageHandler? handler = null,
        CancellationToken cancellationToken = default)
    {
        ILogger log = logger ?? Log.Logger;
        CookieContainer cookies = new CookieContainer();
        HttpMessageHandler pipeline = handler ?? new HttpClientHandler
        {
            CookieContainer = cookies,
            UseCookies = true,
            AllowAutoRedirect = true
        };

        HttpClient http = new HttpClient(pipeline, disposeHandler: handler == null)
        {
            BaseAddress = connection.BaseUri,
            Timeout = connection.Timeout
        };

        SwitchClient client = new SwitchClient(http, connection, log);
        await client.LoginAsync(cancellationToken).ConfigureAwait(false);
        return client;
    }

    /// <summary>
    ///     Downloads a page body.
    /// </summary>
    public async Task<string> GetAsync(string path, CancellationToken cancellationToken = default)
    {
        string relative = Normalize(path);
        _logger.Debug("GET {Path}", relative);
        using HttpResponseMessage response = await _http.GetAsync(relative, cancellationToken).ConfigureAwait(false);
        string body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        await CaptureAsync("GET", relative, null, (int)response.StatusCode, body, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new SwitchException($"GET {relative} returned {(int)response.StatusCode}.");
        }

        FormPage.EnsureNoDocumentError(body);
        EnsureAuthenticated(body);
        return body;
    }

    /// <summary>
    ///     Downloads and parses a configuration page.
    /// </summary>
    public async Task<FormPage> GetFormAsync(string path, CancellationToken cancellationToken = default)
    {
        string body = await GetAsync(path, cancellationToken).ConfigureAwait(false);
        FormPage page = FormPage.Parse(body);
        page.RequestPath = Normalize(path);
        string? error = page.ErrorMessage();
        if (error != null)
        {
            throw new SwitchException(error);
        }

        return page;
    }

    /// <summary>
    ///     Posts form fields and returns the response body.
    /// </summary>
    public async Task<string> PostAsync(
        string path,
        IReadOnlyList<KeyValuePair<string, string>> fields,
        CancellationToken cancellationToken = default)
    {
        string relative = Normalize(path);
        _logger.Debug("POST {Path} ({Count} fields)", relative, fields.Count);
        using FormUrlEncodedContent content = new FormUrlEncodedContent(fields);
        using HttpResponseMessage response = await _http.PostAsync(relative, content, cancellationToken).ConfigureAwait(false);
        string body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        string requestText = string.Join("&", fields.Select(pair => pair.Key + "=" + pair.Value));
        await CaptureAsync("POST", relative, requestText, (int)response.StatusCode, body, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new SwitchException($"POST {relative} returned {(int)response.StatusCode}.");
        }

        FormPage.EnsureNoDocumentError(body);
        FormPage page = FormPage.Parse(body);
        string? error = page.ErrorMessage();
        if (error != null)
        {
            throw new SwitchException(error);
        }

        return body;
    }

    /// <summary>
    ///     Replays a parsed form with the supplied mutations.
    /// </summary>
    public Task<string> PostFormAsync(
        FormPage page,
        IReadOnlyDictionary<string, string>? mutations,
        CancellationToken cancellationToken = default,
        IReadOnlyCollection<string>? omit = null)
    {
        string path = string.IsNullOrWhiteSpace(page.Action) ? page.RequestPath : page.Action;
        return PostAsync(path, page.BuildSubmission(mutations, omit), cancellationToken);
    }

    /// <summary>
    ///     Ends the session.
    /// </summary>
    public async Task LogoutAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await GetAsync("/config/log_out.htm", cancellationToken).ConfigureAwait(false);
        }
        catch (SwitchException exception)
        {
            _logger.Debug(exception, "Logout request failed");
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await LogoutAsync().ConfigureAwait(false);
        _http.Dispose();
    }

    private async Task LoginAsync(CancellationToken cancellationToken)
    {
        KeyValuePair<string, string>[] fields =
        {
            new KeyValuePair<string, string>("userName$query", _connection.User),
            new KeyValuePair<string, string>("password$query", _connection.Password),
            new KeyValuePair<string, string>("restoreUrl", ""),
            new KeyValuePair<string, string>("errorCollector", "")
        };

        using FormUrlEncodedContent content = new FormUrlEncodedContent(fields);
        using HttpResponseMessage response = await _http.PostAsync("/config/log_off_page.htm", content, cancellationToken).ConfigureAwait(false);
        string body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        await CaptureAsync(
            "POST",
            "/config/log_off_page.htm",
            "userName$query=" + _connection.User + "&password$query=***",
            (int)response.StatusCode,
            body,
            cancellationToken).ConfigureAwait(false);

        FormPage.EnsureNoDocumentError(body);
        Uri? finalUri = response.RequestMessage?.RequestUri;
        bool stillOnLogin = finalUri != null &&
                            finalUri.AbsolutePath.Contains("log_off_page", StringComparison.OrdinalIgnoreCase);
        if (!response.IsSuccessStatusCode || stillOnLogin || body.Contains("userName$query", StringComparison.Ordinal) ||
            body.Contains("Access was denied", StringComparison.OrdinalIgnoreCase))
        {
            throw new SwitchException("Login failed. The switch rejected the session, or another user is already logged in.");
        }
    }

    private async Task CaptureAsync(
        string method,
        string path,
        string? requestBody,
        int status,
        string responseBody,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_connection.CaptureDirectory))
        {
            return;
        }

        Directory.CreateDirectory(_connection.CaptureDirectory);
        int index = Interlocked.Increment(ref _captureIndex);
        string safePath = path.Replace('/', '_').Replace('?', '_').Replace(':', '_');
        if (safePath.Length > 80)
        {
            safePath = safePath[..80];
        }

        string file = Path.Combine(_connection.CaptureDirectory, $"{index:000}-{method}-{safePath}.txt");
        string redactedRequest = requestBody == null ? "" : PasswordField.Replace(requestBody, "$1***");
        string redactedResponse = PasswordField.Replace(responseBody, "$1***");
        string text = method + " " + path + " -> " + status.ToString(System.Globalization.CultureInfo.InvariantCulture) +
                      Environment.NewLine + redactedRequest + Environment.NewLine + "-----" + Environment.NewLine +
                      redactedResponse;
        await File.WriteAllTextAsync(file, text, cancellationToken).ConfigureAwait(false);
    }

    private static void EnsureAuthenticated(string body)
    {
        if (body.Contains("Access was denied", StringComparison.OrdinalIgnoreCase) ||
            body.Contains("name=\"userName$query\"", StringComparison.OrdinalIgnoreCase) ||
            body.Contains("name=userName$query", StringComparison.OrdinalIgnoreCase))
        {
            throw new SwitchException("The switch returned the login page. Another session may still be open.");
        }
    }

    private static string Normalize(string path)
    {
        if (path.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            Uri uri = new Uri(path);
            return uri.PathAndQuery;
        }

        return path.StartsWith('/') ? path : "/config/" + path;
    }
}
