using Nefarius.PowerConnect.Ews;
using Nefarius.PowerConnect.Ews.Changes;
using Nefarius.PowerConnect.Ews.Ews;
using Nefarius.PowerConnect.Ews.Http;
using Nefarius.PowerConnect.Ews.Ports;
using Nefarius.PowerConnect.Ews.Snapshot;
using Nefarius.PowerConnect.Ews.Vlan;

using Serilog;
using Serilog.Events;

namespace Nefarius.PowerConnect.Cli.Mcp;

/// <summary>Adapts a logged-in <see cref="SwitchDevice"/> to <see cref="IPowerConnectSession"/>.</summary>
internal sealed class DevicePowerConnectSession : IPowerConnectSession
{
    private readonly SwitchDevice _device;

    public DevicePowerConnectSession(SwitchDevice device)
    {
        _device = device;
    }

    public Task<IReadOnlyList<VlanSummary>> ListVlansAsync(CancellationToken cancellationToken)
    {
        return _device.Vlans.ListAsync(cancellationToken);
    }

    public Task<VlanDetails> GetVlanAsync(int vlanId, CancellationToken cancellationToken)
    {
        return _device.Vlans.GetAsync(vlanId, cancellationToken);
    }

    public Task<ChangePlan> PlanCreateVlanAsync(int vlanId, string name, CancellationToken cancellationToken)
    {
        return _device.Vlans.PlanCreateAsync(vlanId, name, cancellationToken);
    }

    public Task<ChangePlan> PlanDeleteVlanAsync(int vlanId, CancellationToken cancellationToken)
    {
        return _device.Vlans.PlanDeleteAsync(vlanId, cancellationToken);
    }

    public Task<ChangePlan> PlanSetVlanAsync(
        int vlanId,
        IReadOnlyList<PortName> tagged,
        IReadOnlyList<PortName> untagged,
        IReadOnlyList<PortName> forbidden,
        IReadOnlyList<PortName> none,
        CancellationToken cancellationToken)
    {
        return _device.Vlans.PlanSetAsync(vlanId, tagged, untagged, forbidden, none, cancellationToken);
    }

    public Task<IReadOnlyList<VlanPortSettings>> ListVlanPortsAsync(CancellationToken cancellationToken)
    {
        return _device.Vlans.ListPortsAsync(cancellationToken);
    }

    public Task<VlanPortSettings> GetVlanPortAsync(PortName port, CancellationToken cancellationToken)
    {
        return _device.Vlans.GetPortAsync(port, cancellationToken);
    }

    public Task<ChangePlan> PlanSetVlanPortAsync(PortName port, int? pvid, string? frameType, bool? ingressFiltering, CancellationToken cancellationToken)
    {
        return _device.Vlans.PlanPortSetAsync(port, pvid, frameType, ingressFiltering, cancellationToken);
    }

    public Task<ChangePlan> PlanAccessVlanAsync(IReadOnlyList<PortName> ports, int vlanId, CancellationToken cancellationToken)
    {
        return _device.Vlans.PlanAccessAsync(ports, vlanId, cancellationToken);
    }

    public Task<IReadOnlyList<PortConfiguration>> ListPortsAsync(CancellationToken cancellationToken)
    {
        return _device.Ports.ListAsync(cancellationToken);
    }

    public Task<PortConfiguration> GetPortAsync(PortName port, CancellationToken cancellationToken)
    {
        return _device.Ports.GetAsync(port, cancellationToken);
    }

    public Task<ChangePlan> PlanSetPortAsync(PortName port, PortUpdate update, CancellationToken cancellationToken)
    {
        return _device.Ports.PlanSetAsync(port, update, cancellationToken);
    }

    public Task<IReadOnlyDictionary<string, string>> GetAssetAsync(CancellationToken cancellationToken)
    {
        return _device.System.GetAssetAsync(cancellationToken);
    }

    public Task<IReadOnlyDictionary<string, string>> GetVersionsAsync(CancellationToken cancellationToken)
    {
        return _device.System.GetVersionsAsync(cancellationToken);
    }

    public Task<ChangePlan> PlanAssetTagAsync(string tag, CancellationToken cancellationToken)
    {
        return _device.System.PlanAssetTagAsync(tag, cancellationToken);
    }

    public Task<string> GetSecureModeAsync(CancellationToken cancellationToken)
    {
        return _device.System.GetSecureModeAsync(cancellationToken);
    }

    public Task<ChangePlan> PlanSecureModeAsync(string mode, CancellationToken cancellationToken)
    {
        return _device.System.PlanSecureModeAsync(mode, cancellationToken);
    }

    public Task<IReadOnlyDictionary<string, string>> GetJumboAsync(CancellationToken cancellationToken)
    {
        return _device.System.GetJumboAsync(cancellationToken);
    }

    public Task<ChangePlan> PlanJumboAsync(bool enabled, CancellationToken cancellationToken)
    {
        return _device.System.PlanJumboAsync(enabled, cancellationToken);
    }

    public Task<ChangePlan> PlanResetAsync(CancellationToken cancellationToken)
    {
        return _device.System.PlanResetAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<string, string>> GetRawAsync(string page, CancellationToken cancellationToken)
    {
        FormPage form = await _device.Raw.GetAsync(page, cancellationToken).ConfigureAwait(false);
        return RawFields.Redact(form);
    }

    public Task<ChangePlan> PlanRawSetAsync(string page, IReadOnlyDictionary<string, string> mutations, CancellationToken cancellationToken)
    {
        return _device.Raw.PlanSetAsync(page, mutations, destructive: true, cancellationToken);
    }
}

/// <summary>Redacts password, RADIUS, and SNMP fields the way the CLI JSON output does.</summary>
internal static class RawFields
{
    public static IReadOnlyDictionary<string, string> Redact(FormPage page)
    {
        Dictionary<string, string> values = new Dictionary<string, string>();
        foreach (FormField field in page.Fields)
        {
            if (field.Role is FieldRole.Metadata or FieldRole.Marker)
            {
                continue;
            }

            values[field.Name] = ConfigSnapshot.IsSensitive(field.Name) ? "***" : field.Value;
        }

        return values;
    }
}

/// <summary>
///     Connects for one tool call, logs out when the call finishes, and keeps each switch single-threaded.
/// </summary>
internal sealed class DevicePowerConnectSessions : IPowerConnectSessions
{
    private readonly SwitchTargetRegistry _registry;
    private readonly SwitchCallGate _gate;

    public DevicePowerConnectSessions(SwitchTargetRegistry registry, SwitchCallGate gate)
    {
        _registry = registry;
        _gate = gate;
    }

    public IReadOnlyList<SwitchTargetInfo> List()
    {
        return _registry.List();
    }

    public Task<T> ExecuteAsync<T>(string alias, Func<IPowerConnectSession, CancellationToken, Task<T>> action, CancellationToken cancellationToken)
    {
        SwitchTarget target = _registry.Get(alias);
        SwitchConnection connection = _registry.CreateConnection(target);
        return _gate.RunAsync(alias, token => RunAsync(target, connection, action, token), cancellationToken);
    }

    private static async Task<T> RunAsync<T>(
        SwitchTarget target,
        SwitchConnection connection,
        Func<IPowerConnectSession, CancellationToken, Task<T>> action,
        CancellationToken cancellationToken)
    {
        Serilog.Core.Logger? logger = target.Verbose
            ? new LoggerConfiguration().MinimumLevel.Debug().WriteTo.Console(standardErrorFromLevel: LogEventLevel.Verbose).CreateLogger()
            : null;
        try
        {
            await using SwitchDevice device = await SwitchDevice.ConnectAsync(connection, logger, cancellationToken).ConfigureAwait(false);
            return await action(new DevicePowerConnectSession(device), cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            logger?.Dispose();
        }
    }
}
