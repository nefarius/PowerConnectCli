using System.Net;

using Nefarius.PowerConnect.Ews.Http;

using Serilog;

namespace Nefarius.PowerConnect.Ews.Tests;

public sealed class SwitchClientTests
{
    private sealed class StubHandler(Func<int, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            HttpResponseMessage response = respond(Calls++);
            response.RequestMessage = response.StatusCode == HttpStatusCode.OK && response.Headers.Contains("X-Home")
                ? new HttpRequestMessage(HttpMethod.Get, new Uri(request.RequestUri!, "/home.htm"))
                : request;
            return Task.FromResult(response);
        }
    }

    private static HttpResponseMessage Ok(string body = "<html>home</html>")
    {
        HttpResponseMessage response = new(HttpStatusCode.OK) { Content = new StringContent(body) };
        response.Headers.Add("X-Home", "1");
        return response;
    }

    private static HttpResponseMessage Denied()
    {
        return Ok("<html>Access was denied</html>");
    }

    private static SwitchConnection Connection(int retries)
    {
        return new SwitchConnection { LoginRetries = retries, LoginRetryDelay = TimeSpan.Zero };
    }

    private static Task<SwitchClient> Connect(SwitchConnection c, StubHandler h, CancellationToken ct = default)
    {
        return SwitchClient.ConnectAsync(c, new LoggerConfiguration().CreateLogger(), h, ct);
    }

    [Fact]
    public async Task Succeeds_after_retries()
    {
        StubHandler handler = new(n => n < 2 ? Denied() : Ok());
        await using SwitchClient client = await Connect(Connection(3), handler);
        Assert.Equal(3, handler.Calls);
    }

    [Fact]
    public async Task Exhausts_retries()
    {
        StubHandler handler = new(_ => Denied());
        SwitchException ex = await Assert.ThrowsAsync<SwitchException>(() => Connect(Connection(2), handler));
        Assert.StartsWith("Login failed", ex.Message);
        Assert.Equal(3, handler.Calls);
    }

    [Fact]
    public async Task Zero_retries_disables_retry()
    {
        StubHandler handler = new(_ => Denied());
        await Assert.ThrowsAsync<SwitchException>(() => Connect(Connection(0), handler));
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task Retries_transport_errors()
    {
        StubHandler handler = new(n => n == 0 ? throw new HttpRequestException("boom") : Ok());
        await using SwitchClient client = await Connect(Connection(1), handler);
        Assert.Equal(2, handler.Calls);
    }

    [Fact]
    public async Task Does_not_retry_document_errors()
    {
        StubHandler handler = new(_ => Ok("<html><title>Document Error</title>Document Error</html>"));
        await Assert.ThrowsAsync<SwitchException>(() => Connect(Connection(3), handler));
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task Cancellation_during_backoff_propagates()
    {
        using CancellationTokenSource cts = new();
        StubHandler handler = new(_ => Denied());
        SwitchConnection c = new() { LoginRetries = 3, LoginRetryDelay = TimeSpan.FromMinutes(5) };
        Task<SwitchClient> task = Connect(c, handler, cts.Token);
        cts.CancelAfter(100);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
        Assert.Equal(1, handler.Calls);
    }
}
