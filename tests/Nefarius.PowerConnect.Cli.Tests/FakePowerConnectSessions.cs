using Nefarius.PowerConnect.Cli.Mcp;
using Nefarius.PowerConnect.Ews.Changes;
using Nefarius.PowerConnect.Ews.Ews;
using Nefarius.PowerConnect.Ews.Ports;
using Nefarius.PowerConnect.Ews.Vlan;

namespace Nefarius.PowerConnect.Cli.Tests;

internal sealed record RecordedCall(string Method, object?[] Arguments);

internal sealed class FakePowerConnectSession : IPowerConnectSession
{
    public List<RecordedCall> Calls { get; } = new List<RecordedCall>();

    public Exception? Error { get; set; }

    public Task<IReadOnlyList<VlanSummary>> ListVlansAsync(CancellationToken cancellationToken)
    {
        Record("ListVlans");
        return Task.FromResult<IReadOnlyList<VlanSummary>>(Array.Empty<VlanSummary>());
    }

    public Task<VlanDetails> GetVlanAsync(int vlanId, CancellationToken cancellationToken)
    {
        Record("GetVlan", vlanId);
        return Task.FromResult(new VlanDetails());
    }

    public Task<ChangePlan> PlanCreateVlanAsync(int vlanId, string name, CancellationToken cancellationToken)
    {
        Record("PlanCreateVlan", vlanId, name);
        return Task.FromResult(OneStep("Create VLAN"));
    }

    public Task<ChangePlan> PlanDeleteVlanAsync(int vlanId, CancellationToken cancellationToken)
    {
        Record("PlanDeleteVlan", vlanId);
        return Task.FromResult(OneStep("Delete VLAN"));
    }

    public Task<ChangePlan> PlanSetVlanAsync(
        int vlanId,
        IReadOnlyList<PortName> tagged,
        IReadOnlyList<PortName> untagged,
        IReadOnlyList<PortName> forbidden,
        IReadOnlyList<PortName> none,
        CancellationToken cancellationToken)
    {
        Record("PlanSetVlan", vlanId, tagged, untagged, forbidden, none);
        return Task.FromResult(OneStep("Set VLAN"));
    }

    public Task<IReadOnlyList<VlanPortSettings>> ListVlanPortsAsync(CancellationToken cancellationToken)
    {
        Record("ListVlanPorts");
        return Task.FromResult<IReadOnlyList<VlanPortSettings>>(Array.Empty<VlanPortSettings>());
    }

    public Task<VlanPortSettings> GetVlanPortAsync(PortName port, CancellationToken cancellationToken)
    {
        Record("GetVlanPort", port);
        return Task.FromResult(new VlanPortSettings());
    }

    public Task<ChangePlan> PlanSetVlanPortAsync(PortName port, int? pvid, string? frameType, bool? ingressFiltering, CancellationToken cancellationToken)
    {
        Record("PlanSetVlanPort", port, pvid, frameType, ingressFiltering);
        return Task.FromResult(OneStep("Set VLAN port " + port.Name));
    }

    public Task<ChangePlan> PlanAccessVlanAsync(IReadOnlyList<PortName> ports, int vlanId, CancellationToken cancellationToken)
    {
        Record("PlanAccessVlan", ports, vlanId);
        return Task.FromResult(OneStep("Access VLAN"));
    }

    public Task<IReadOnlyList<PortConfiguration>> ListPortsAsync(CancellationToken cancellationToken)
    {
        Record("ListPorts");
        return Task.FromResult<IReadOnlyList<PortConfiguration>>(Array.Empty<PortConfiguration>());
    }

    public Task<PortConfiguration> GetPortAsync(PortName port, CancellationToken cancellationToken)
    {
        Record("GetPort", port);
        return Task.FromResult(new PortConfiguration());
    }

    public Task<ChangePlan> PlanSetPortAsync(PortName port, PortUpdate update, CancellationToken cancellationToken)
    {
        Record("PlanSetPort", port, update);
        return Task.FromResult(OneStep("Configure port " + port.Name));
    }

    public Task<IReadOnlyDictionary<string, string>> GetAssetAsync(CancellationToken cancellationToken)
    {
        Record("GetAsset");
        return Task.FromResult<IReadOnlyDictionary<string, string>>(new Dictionary<string, string>());
    }

    public Task<IReadOnlyDictionary<string, string>> GetVersionsAsync(CancellationToken cancellationToken)
    {
        Record("GetVersions");
        return Task.FromResult<IReadOnlyDictionary<string, string>>(new Dictionary<string, string>());
    }

    public Task<ChangePlan> PlanAssetTagAsync(string tag, CancellationToken cancellationToken)
    {
        Record("PlanAssetTag", tag);
        return Task.FromResult(OneStep("Set asset tag"));
    }

    public Task<string> GetSecureModeAsync(CancellationToken cancellationToken)
    {
        Record("GetSecureMode");
        return Task.FromResult("managed");
    }

    public Task<ChangePlan> PlanSecureModeAsync(string mode, CancellationToken cancellationToken)
    {
        Record("PlanSecureMode", mode);
        return Task.FromResult(OneStep("Set secure mode"));
    }

    public Task<IReadOnlyDictionary<string, string>> GetJumboAsync(CancellationToken cancellationToken)
    {
        Record("GetJumbo");
        return Task.FromResult<IReadOnlyDictionary<string, string>>(new Dictionary<string, string>());
    }

    public Task<ChangePlan> PlanJumboAsync(bool enabled, CancellationToken cancellationToken)
    {
        Record("PlanJumbo", enabled);
        return Task.FromResult(OneStep("Set jumbo"));
    }

    public Task<ChangePlan> PlanResetAsync(CancellationToken cancellationToken)
    {
        Record("PlanReset");
        return Task.FromResult(OneStep("Reset"));
    }

    public Task<IReadOnlyDictionary<string, string>> GetRawAsync(string page, CancellationToken cancellationToken)
    {
        Record("GetRaw", page);
        return Task.FromResult<IReadOnlyDictionary<string, string>>(new Dictionary<string, string>());
    }

    public Task<ChangePlan> PlanRawSetAsync(string page, IReadOnlyDictionary<string, string> mutations, CancellationToken cancellationToken)
    {
        Record("PlanRawSet", page, mutations);
        return Task.FromResult(OneStep("Raw set"));
    }

    private void Record(string method, params object?[] arguments)
    {
        Calls.Add(new RecordedCall(method, arguments));
        if (Error != null)
        {
            throw Error;
        }
    }

    private static ChangePlan OneStep(string title)
    {
        ChangePlan plan = new ChangePlan();
        plan.Add(new ChangeStep(title, destructive: false, Array.Empty<FieldDiff>(), _ => Task.CompletedTask, null));
        return plan;
    }
}

internal sealed class FakePowerConnectSessions : IPowerConnectSessions
{
    public FakePowerConnectSession Session { get; } = new FakePowerConnectSession();

    public List<string> Aliases { get; } = new List<string>();

    public IReadOnlyList<SwitchTargetInfo> Targets { get; set; } = Array.Empty<SwitchTargetInfo>();

    public IReadOnlyList<SwitchTargetInfo> List()
    {
        return Targets;
    }

    public Task<T> ExecuteAsync<T>(string alias, Func<IPowerConnectSession, CancellationToken, Task<T>> action, CancellationToken cancellationToken)
    {
        if (!alias.Equals("core", StringComparison.OrdinalIgnoreCase))
        {
            throw new SwitchTargetException("Unknown switch '" + alias + "'. Configured aliases: core.");
        }

        Aliases.Add(alias);
        return action(Session, cancellationToken);
    }
}

internal sealed class RecordingRunner : IMcpChangeRunner
{
    public ChangePlan? Plan { get; private set; }

    public bool Apply { get; private set; }

    public bool IUnderstand { get; private set; }

    public bool RequireAcknowledgement { get; private set; }

    public Task<McpChangeResult> RunAsync(
        ChangePlan plan,
        bool apply,
        bool iUnderstand,
        bool requireAcknowledgement,
        CancellationToken cancellationToken)
    {
        Plan = plan;
        Apply = apply;
        IUnderstand = iUnderstand;
        RequireAcknowledgement = requireAcknowledgement;
        return Task.FromResult(new McpChangeResult
        {
            Status = "recorded",
            Applied = false,
            Destructive = requireAcknowledgement,
            AcknowledgementRequired = requireAcknowledgement,
            Message = "recorded",
            Steps = Array.Empty<McpChangeStep>()
        });
    }
}
