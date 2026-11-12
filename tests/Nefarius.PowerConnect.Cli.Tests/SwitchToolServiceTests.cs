using Nefarius.PowerConnect.Cli.Mcp;
using Nefarius.PowerConnect.Ews.Ews;
using Nefarius.PowerConnect.Ews.Ports;

using ModelContextProtocol;

namespace Nefarius.PowerConnect.Cli.Tests;

public sealed class SwitchToolServiceTests
{
    private const string Secret = "super-secret-password";
    private readonly FakePowerConnectSessions _sessions = new FakePowerConnectSessions();
    private readonly RecordingRunner _runner = new RecordingRunner();
    private readonly SwitchToolService _service;

    public SwitchToolServiceTests()
    {
        _service = new SwitchToolService(_sessions, _runner);
    }

    [Fact]
    public async Task MapsPortRangesAndSettingsBeforePlanning()
    {
        await _service.SetPortsAsync(" core ", "g1-g2,g4", admin: "down", description: null, speed: 100, duplex: null, autoNegotiation: "off", flowControl: null, mdix: null, backPressure: null, apply: false, CancellationToken.None);

        Assert.Equal(new[] { "core" }, _sessions.Aliases);
        Assert.Equal(new[] { "g1", "g2", "g4" }, _sessions.Session.Calls.Select(call => ((PortName)call.Arguments[0]!).Name));
        PortUpdate update = (PortUpdate)_sessions.Session.Calls[0].Arguments[1]!;
        Assert.False(update.AdminUp);
        Assert.Equal(100, update.SpeedMegabits);
        Assert.False(update.AutoNegotiation);
        Assert.False(_runner.Apply);
        Assert.False(_runner.RequireAcknowledgement);
        Assert.Equal(3, _runner.Plan!.Steps.Count);
    }

    [Fact]
    public async Task MapsVlanMembershipAndAccessAsOnePlan()
    {
        await _service.SetVlanAsync("core", 20, tagged: "g1-g2", untagged: null, forbidden: null, none: "ch1", apply: true, CancellationToken.None);
        RecordedCall membership = Assert.Single(_sessions.Session.Calls);
        Assert.Equal(new[] { "g1", "g2" }, Names(membership.Arguments[1]));
        Assert.Equal(new[] { "ch1" }, Names(membership.Arguments[4]));
        Assert.True(_runner.Apply);

        _sessions.Session.Calls.Clear();
        await _service.AccessVlanAsync("core", "g5-g6", vlan: 40, apply: false, CancellationToken.None);
        RecordedCall access = _sessions.Session.Calls.Single(call => call.Method == "PlanAccessVlan");
        Assert.Equal(new[] { "g5", "g6" }, Names(access.Arguments[0]));
        Assert.Equal(40, access.Arguments[1]);
    }

    [Fact]
    public async Task RejectsBadArgumentsBeforeContactingTheSwitch()
    {
        McpException exception = await Assert.ThrowsAsync<McpException>(() => _service.SetPortsAsync("core", "g1", admin: "sideways", description: null, speed: null, duplex: null, autoNegotiation: null, flowControl: null, mdix: null, backPressure: null, apply: true, CancellationToken.None));

        Assert.Contains("up or down", exception.Message);
        Assert.Empty(_sessions.Session.Calls);
        Assert.Null(_runner.Plan);
    }

    [Fact]
    public async Task RequiresAcknowledgementForResetUnmanagedModeAndRawWrites()
    {
        await _service.ResetAsync("core", apply: false, iUnderstand: false, CancellationToken.None);
        Assert.True(_runner.RequireAcknowledgement);

        await _service.SetSecureModeAsync("core", "unmanaged", apply: false, iUnderstand: false, CancellationToken.None);
        Assert.True(_runner.RequireAcknowledgement);
        await _service.SetSecureModeAsync("core", "managed", apply: true, iUnderstand: false, CancellationToken.None);
        Assert.False(_runner.RequireAcknowledgement);

        await _service.RawSetAsync("core", "snmp.htm", new Dictionary<string, string> { ["sysName$query"] = "lab" }, apply: false, iUnderstand: false, CancellationToken.None);
        Assert.True(_runner.RequireAcknowledgement);
    }

    [Fact]
    public async Task MapsSwitchFailuresWithoutReturningSecrets()
    {
        _sessions.Session.Error = new TaskCanceledException(Secret);
        McpException timedOut = await Assert.ThrowsAsync<McpException>(() => _service.ListVlansAsync("core", CancellationToken.None));
        Assert.Equal("The switch did not respond in time.", timedOut.Message);
        Assert.DoesNotContain(Secret, timedOut.ToString());

        _sessions.Session.Error = new InvalidOperationException(Secret);
        McpException failed = await Assert.ThrowsAsync<McpException>(() => _service.ListVlansAsync("core", CancellationToken.None));
        Assert.Equal("The switch operation failed.", failed.Message);
        Assert.DoesNotContain(Secret, failed.ToString());

        McpException unknown = await Assert.ThrowsAsync<McpException>(() => _service.ListVlansAsync("lab", CancellationToken.None));
        Assert.Contains("Unknown switch 'lab'", unknown.Message);
        Assert.DoesNotContain(Secret, unknown.Message);
    }

    [Fact]
    public async Task PropagatesCancellationInsteadOfAToolError()
    {
        using CancellationTokenSource source = new CancellationTokenSource();
        source.Cancel();
        _sessions.Session.Error = new OperationCanceledException(source.Token);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _service.ListVlansAsync("core", source.Token));
    }

    [Fact]
    public void RedactsSensitiveRawFields()
    {
        FormPage page = FormPage.Parse("""
            <form>
              <input name="sysName$query" value="lab" />
              <input name="userPassword$query" value="super-secret-password" />
              <input name="rlSnmpCommunityString$query" value="public-community" />
              <input name="type$VT" value="1" />
              <input name="end$endVT" value="" />
            </form>
            """);

        IReadOnlyDictionary<string, string> fields = RawFields.Redact(page);

        Assert.Equal("lab", fields["sysName$query"]);
        Assert.Equal("***", fields["userPassword$query"]);
        Assert.Equal("***", fields["rlSnmpCommunityString$query"]);
        Assert.DoesNotContain(fields, field => field.Value.Contains(Secret, StringComparison.Ordinal) || field.Value.Contains("public-community", StringComparison.Ordinal));
        Assert.DoesNotContain("type$VT", fields.Keys);
        Assert.DoesNotContain("end$endVT", fields.Keys);
    }

    private static IReadOnlyList<string> Names(object? value)
    {
        return ((IReadOnlyList<PortName>)value!).Select(port => port.Name).ToList();
    }
}
