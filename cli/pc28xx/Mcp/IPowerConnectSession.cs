using Nefarius.PowerConnect.Ews.Changes;
using Nefarius.PowerConnect.Ews.Ews;
using Nefarius.PowerConnect.Ews.Ports;
using Nefarius.PowerConnect.Ews.Vlan;

namespace Nefarius.PowerConnect.Cli.Mcp;

/// <summary>Switch operations the MCP tools can perform. Tests substitute a fake.</summary>
internal interface IPowerConnectSession
{
    Task<IReadOnlyList<VlanSummary>> ListVlansAsync(CancellationToken cancellationToken);

    Task<VlanDetails> GetVlanAsync(int vlanId, CancellationToken cancellationToken);

    Task<ChangePlan> PlanCreateVlanAsync(int vlanId, string name, CancellationToken cancellationToken);

    Task<ChangePlan> PlanDeleteVlanAsync(int vlanId, CancellationToken cancellationToken);

    Task<ChangePlan> PlanSetVlanAsync(
        int vlanId,
        IReadOnlyList<PortName> tagged,
        IReadOnlyList<PortName> untagged,
        IReadOnlyList<PortName> forbidden,
        IReadOnlyList<PortName> none,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<VlanPortSettings>> ListVlanPortsAsync(CancellationToken cancellationToken);

    Task<VlanPortSettings> GetVlanPortAsync(PortName port, CancellationToken cancellationToken);

    Task<ChangePlan> PlanSetVlanPortAsync(PortName port, int? pvid, string? frameType, bool? ingressFiltering, CancellationToken cancellationToken);

    Task<ChangePlan> PlanAccessVlanAsync(IReadOnlyList<PortName> ports, int vlanId, CancellationToken cancellationToken);

    Task<IReadOnlyList<PortConfiguration>> ListPortsAsync(CancellationToken cancellationToken);

    Task<PortConfiguration> GetPortAsync(PortName port, CancellationToken cancellationToken);

    Task<ChangePlan> PlanSetPortAsync(PortName port, PortUpdate update, CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<string, string>> GetAssetAsync(CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<string, string>> GetVersionsAsync(CancellationToken cancellationToken);

    Task<ChangePlan> PlanAssetTagAsync(string tag, CancellationToken cancellationToken);

    Task<string> GetSecureModeAsync(CancellationToken cancellationToken);

    Task<ChangePlan> PlanSecureModeAsync(string mode, CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<string, string>> GetJumboAsync(CancellationToken cancellationToken);

    Task<ChangePlan> PlanJumboAsync(bool enabled, CancellationToken cancellationToken);

    Task<ChangePlan> PlanResetAsync(CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<string, string>> GetRawAsync(string page, CancellationToken cancellationToken);

    Task<ChangePlan> PlanRawSetAsync(string page, IReadOnlyDictionary<string, string> mutations, CancellationToken cancellationToken);
}

/// <summary>Opens a switch session for one alias.</summary>
internal interface IPowerConnectSessions
{
    IReadOnlyList<SwitchTargetInfo> List();

    Task<T> ExecuteAsync<T>(string alias, Func<IPowerConnectSession, CancellationToken, Task<T>> action, CancellationToken cancellationToken);
}
