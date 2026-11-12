using Nefarius.PowerConnect.Ews.Changes;
using Nefarius.PowerConnect.Ews.Ews;
using Nefarius.PowerConnect.Ews.Ports;
using Nefarius.PowerConnect.Ews.Vlan;

using ModelContextProtocol;

namespace Nefarius.PowerConnect.Cli.Mcp;

/// <summary>Maps MCP arguments onto switch sessions and the shared change runner.</summary>
internal sealed class SwitchToolService
{
    private readonly IPowerConnectSessions _sessions;
    private readonly IMcpChangeRunner _runner;

    public SwitchToolService(IPowerConnectSessions sessions, IMcpChangeRunner runner)
    {
        _sessions = sessions;
        _runner = runner;
    }

    public IReadOnlyList<SwitchTargetInfo> ListSwitches()
    {
        return _sessions.List();
    }

    public Task<IReadOnlyList<VlanSummary>> ListVlansAsync(string alias, CancellationToken cancellationToken)
    {
        return Guard(() => Use(alias, (session, token) => session.ListVlansAsync(token), cancellationToken), cancellationToken);
    }

    public Task<VlanDetails> ShowVlanAsync(string alias, int id, CancellationToken cancellationToken)
    {
        return Guard(() =>
        {
            RequireVlan(id, allowDefault: true);
            return Use(alias, (session, token) => session.GetVlanAsync(id, token), cancellationToken);
        }, cancellationToken);
    }

    public Task<McpChangeResult> CreateVlanAsync(string alias, int id, string name, bool apply, CancellationToken cancellationToken)
    {
        return Guard(async () =>
        {
            RequireVlan(id, allowDefault: false);
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new FormatException("VLAN name is required.");
            }

            ChangePlan plan = await Use(alias, (session, token) => session.PlanCreateVlanAsync(id, name.Trim(), token), cancellationToken).ConfigureAwait(false);
            return await _runner.RunAsync(plan, apply, iUnderstand: false, requireAcknowledgement: false, cancellationToken).ConfigureAwait(false);
        }, cancellationToken);
    }

    public Task<McpChangeResult> DeleteVlanAsync(string alias, int id, bool apply, CancellationToken cancellationToken)
    {
        return Guard(async () =>
        {
            RequireVlan(id, allowDefault: false);
            ChangePlan plan = await Use(alias, (session, token) => session.PlanDeleteVlanAsync(id, token), cancellationToken).ConfigureAwait(false);
            return await _runner.RunAsync(plan, apply, iUnderstand: false, requireAcknowledgement: false, cancellationToken).ConfigureAwait(false);
        }, cancellationToken);
    }

    public Task<McpChangeResult> SetVlanAsync(
        string alias,
        int id,
        string? tagged,
        string? untagged,
        string? forbidden,
        string? none,
        bool apply,
        CancellationToken cancellationToken)
    {
        return Guard(async () =>
        {
            RequireVlan(id, allowDefault: true);
            IReadOnlyList<PortName> taggedPorts = McpValues.OptionalPorts(tagged);
            IReadOnlyList<PortName> untaggedPorts = McpValues.OptionalPorts(untagged);
            IReadOnlyList<PortName> forbiddenPorts = McpValues.OptionalPorts(forbidden);
            IReadOnlyList<PortName> removedPorts = McpValues.OptionalPorts(none);
            if (taggedPorts.Count + untaggedPorts.Count + forbiddenPorts.Count + removedPorts.Count == 0)
            {
                throw new FormatException("Name at least one port in tagged, untagged, forbidden, or none.");
            }

            ChangePlan plan = await Use(
                alias,
                (session, token) => session.PlanSetVlanAsync(id, taggedPorts, untaggedPorts, forbiddenPorts, removedPorts, token),
                cancellationToken).ConfigureAwait(false);
            return await _runner.RunAsync(plan, apply, iUnderstand: false, requireAcknowledgement: false, cancellationToken).ConfigureAwait(false);
        }, cancellationToken);
    }

    public Task<IReadOnlyList<VlanPortSettings>> ListVlanPortsAsync(string alias, CancellationToken cancellationToken)
    {
        return Guard(() => Use(alias, (session, token) => session.ListVlanPortsAsync(token), cancellationToken), cancellationToken);
    }

    public Task<IReadOnlyList<VlanPortSettings>> ShowVlanPortsAsync(string alias, string ports, CancellationToken cancellationToken)
    {
        return Guard(() =>
        {
            IReadOnlyList<PortName> selection = McpValues.RequiredPorts(ports);
            return Use(alias, async (session, token) =>
            {
                List<VlanPortSettings> result = new List<VlanPortSettings>();
                foreach (PortName port in selection)
                {
                    result.Add(await session.GetVlanPortAsync(port, token).ConfigureAwait(false));
                }

                return (IReadOnlyList<VlanPortSettings>)result;
            }, cancellationToken);
        }, cancellationToken);
    }

    public Task<McpChangeResult> SetVlanPortsAsync(
        string alias,
        string ports,
        int? pvid,
        string? frameType,
        string? ingressFiltering,
        bool apply,
        CancellationToken cancellationToken)
    {
        return Guard(async () =>
        {
            IReadOnlyList<PortName> selection = McpValues.RequiredPorts(ports);
            bool? filtering = ingressFiltering == null ? null : McpValues.OnOff(ingressFiltering);
            string? frames = frameType == null ? null : McpValues.FrameType(frameType);
            if (pvid == null && frames == null && filtering == null)
            {
                throw new FormatException("Set pvid, frameType, or ingressFiltering.");
            }

            if (pvid is < 1 or > 4094)
            {
                throw new FormatException("PVID must be between 1 and 4094.");
            }

            ChangePlan plan = await Use(alias, async (session, token) =>
            {
                ChangePlan combined = new ChangePlan();
                foreach (PortName port in selection)
                {
                    combined.AddRange(await session.PlanSetVlanPortAsync(port, pvid, frames, filtering, token).ConfigureAwait(false));
                }

                return combined;
            }, cancellationToken).ConfigureAwait(false);
            return await _runner.RunAsync(plan, apply, iUnderstand: false, requireAcknowledgement: false, cancellationToken).ConfigureAwait(false);
        }, cancellationToken);
    }

    public Task<McpChangeResult> AccessVlanAsync(string alias, string ports, int vlan, bool apply, CancellationToken cancellationToken)
    {
        return Guard(async () =>
        {
            IReadOnlyList<PortName> selection = McpValues.RequiredPorts(ports);
            RequireVlan(vlan, allowDefault: true);
            ChangePlan plan = await Use(alias, (session, token) => session.PlanAccessVlanAsync(selection, vlan, token), cancellationToken).ConfigureAwait(false);
            return await _runner.RunAsync(plan, apply, iUnderstand: false, requireAcknowledgement: false, cancellationToken).ConfigureAwait(false);
        }, cancellationToken);
    }

    public Task<IReadOnlyList<PortConfiguration>> ListPortsAsync(string alias, CancellationToken cancellationToken)
    {
        return Guard(() => Use(alias, (session, token) => session.ListPortsAsync(token), cancellationToken), cancellationToken);
    }

    public Task<IReadOnlyList<PortConfiguration>> ShowPortsAsync(string alias, string ports, CancellationToken cancellationToken)
    {
        return Guard(() =>
        {
            IReadOnlyList<PortName> selection = McpValues.RequiredPorts(ports);
            return Use(alias, async (session, token) =>
            {
                List<PortConfiguration> result = new List<PortConfiguration>();
                foreach (PortName port in selection)
                {
                    result.Add(await session.GetPortAsync(port, token).ConfigureAwait(false));
                }

                return (IReadOnlyList<PortConfiguration>)result;
            }, cancellationToken);
        }, cancellationToken);
    }

    public Task<McpChangeResult> SetPortsAsync(
        string alias,
        string ports,
        string? admin,
        string? description,
        int? speed,
        string? duplex,
        string? autoNegotiation,
        string? flowControl,
        string? mdix,
        string? backPressure,
        bool apply,
        CancellationToken cancellationToken)
    {
        return Guard(async () =>
        {
            IReadOnlyList<PortName> selection = McpValues.RequiredPorts(ports);
            PortUpdate update = new PortUpdate
            {
                AdminUp = admin == null ? null : McpValues.AdminUp(admin),
                Description = description,
                SpeedMegabits = McpValues.Speed(speed),
                Duplex = duplex == null ? null : McpValues.Duplex(duplex),
                AutoNegotiation = autoNegotiation == null ? null : McpValues.OnOff(autoNegotiation),
                FlowControl = flowControl == null ? null : McpValues.FlowControl(flowControl),
                Mdix = mdix == null ? null : McpValues.Mdix(mdix),
                BackPressure = backPressure == null ? null : McpValues.OnOff(backPressure)
            };
            if (update.AdminUp == null &&
                update.Description == null &&
                update.SpeedMegabits == null &&
                update.Duplex == null &&
                update.AutoNegotiation == null &&
                update.FlowControl == null &&
                update.Mdix == null &&
                update.BackPressure == null)
            {
                throw new FormatException("Set at least one port field.");
            }

            ChangePlan plan = await Use(alias, async (session, token) =>
            {
                ChangePlan combined = new ChangePlan();
                foreach (PortName port in selection)
                {
                    combined.AddRange(await session.PlanSetPortAsync(port, update, token).ConfigureAwait(false));
                }

                return combined;
            }, cancellationToken).ConfigureAwait(false);
            return await _runner.RunAsync(plan, apply, iUnderstand: false, requireAcknowledgement: false, cancellationToken).ConfigureAwait(false);
        }, cancellationToken);
    }

    public Task<IReadOnlyDictionary<string, string>> AssetAsync(string alias, CancellationToken cancellationToken)
    {
        return Guard(() => Use(alias, (session, token) => session.GetAssetAsync(token), cancellationToken), cancellationToken);
    }

    public Task<IReadOnlyDictionary<string, string>> VersionsAsync(string alias, CancellationToken cancellationToken)
    {
        return Guard(() => Use(alias, (session, token) => session.GetVersionsAsync(token), cancellationToken), cancellationToken);
    }

    public Task<McpChangeResult> SetAssetTagAsync(string alias, string tag, bool apply, CancellationToken cancellationToken)
    {
        return Guard(async () =>
        {
            if (tag == null)
            {
                throw new FormatException("Asset tag is required.");
            }

            ChangePlan plan = await Use(alias, (session, token) => session.PlanAssetTagAsync(tag, token), cancellationToken).ConfigureAwait(false);
            return await _runner.RunAsync(plan, apply, iUnderstand: false, requireAcknowledgement: false, cancellationToken).ConfigureAwait(false);
        }, cancellationToken);
    }

    public Task<SecureModeInfo> SecureModeAsync(string alias, CancellationToken cancellationToken)
    {
        return Guard(async () =>
        {
            string mode = await Use(alias, (session, token) => session.GetSecureModeAsync(token), cancellationToken).ConfigureAwait(false);
            return new SecureModeInfo { Mode = mode };
        }, cancellationToken);
    }

    public Task<McpChangeResult> SetSecureModeAsync(string alias, string mode, bool apply, bool iUnderstand, CancellationToken cancellationToken)
    {
        return Guard(async () =>
        {
            string normalized = McpValues.SecureMode(mode);
            ChangePlan plan = await Use(alias, (session, token) => session.PlanSecureModeAsync(normalized, token), cancellationToken).ConfigureAwait(false);
            return await _runner.RunAsync(plan, apply, iUnderstand, requireAcknowledgement: normalized == "unmanaged", cancellationToken).ConfigureAwait(false);
        }, cancellationToken);
    }

    public Task<IReadOnlyDictionary<string, string>> JumboAsync(string alias, CancellationToken cancellationToken)
    {
        return Guard(() => Use(alias, (session, token) => session.GetJumboAsync(token), cancellationToken), cancellationToken);
    }

    public Task<McpChangeResult> SetJumboAsync(string alias, string state, bool apply, CancellationToken cancellationToken)
    {
        return Guard(async () =>
        {
            bool enabled = McpValues.OnOff(state);
            ChangePlan plan = await Use(alias, (session, token) => session.PlanJumboAsync(enabled, token), cancellationToken).ConfigureAwait(false);
            return await _runner.RunAsync(plan, apply, iUnderstand: false, requireAcknowledgement: false, cancellationToken).ConfigureAwait(false);
        }, cancellationToken);
    }

    public Task<McpChangeResult> ResetAsync(string alias, bool apply, bool iUnderstand, CancellationToken cancellationToken)
    {
        return Guard(async () =>
        {
            ChangePlan plan = await Use(alias, (session, token) => session.PlanResetAsync(token), cancellationToken).ConfigureAwait(false);
            return await _runner.RunAsync(plan, apply, iUnderstand, requireAcknowledgement: true, cancellationToken).ConfigureAwait(false);
        }, cancellationToken);
    }

    public Task<IReadOnlyDictionary<string, string>> RawGetAsync(string alias, string page, CancellationToken cancellationToken)
    {
        return Guard(() =>
        {
            string path = RequiredPage(page);
            return Use(alias, (session, token) => session.GetRawAsync(path, token), cancellationToken);
        }, cancellationToken);
    }

    public Task<McpChangeResult> RawSetAsync(
        string alias,
        string page,
        IReadOnlyDictionary<string, string>? fields,
        bool apply,
        bool iUnderstand,
        CancellationToken cancellationToken)
    {
        return Guard(async () =>
        {
            string path = RequiredPage(page);
            if (fields == null || fields.Count == 0)
            {
                throw new FormatException("At least one field is required.");
            }

            Dictionary<string, string> mutations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, string> field in fields)
            {
                if (string.IsNullOrWhiteSpace(field.Key))
                {
                    throw new FormatException("Field names must not be empty.");
                }

                mutations[field.Key] = field.Value ?? "";
            }

            ChangePlan plan = await Use(alias, (session, token) => session.PlanRawSetAsync(path, mutations, token), cancellationToken).ConfigureAwait(false);
            return await _runner.RunAsync(plan, apply, iUnderstand, requireAcknowledgement: true, cancellationToken).ConfigureAwait(false);
        }, cancellationToken);
    }

    private Task<T> Use<T>(string alias, Func<IPowerConnectSession, CancellationToken, Task<T>> action, CancellationToken cancellationToken)
    {
        return _sessions.ExecuteAsync(NormalizeAlias(alias), action, cancellationToken);
    }

    private static async Task<T> Guard<T>(Func<Task<T>> action, CancellationToken cancellationToken)
    {
        try
        {
            return await action().ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (McpException)
        {
            throw;
        }
        catch (Exception exception) when (McpErrors.TryDescribe(exception, out string message))
        {
            throw new McpException(message);
        }
    }

    private static string NormalizeAlias(string alias)
    {
        if (string.IsNullOrWhiteSpace(alias))
        {
            throw new FormatException("A switch alias is required.");
        }

        return alias.Trim();
    }

    private static void RequireVlan(int id, bool allowDefault)
    {
        int minimum = allowDefault ? 1 : 2;
        if (id < minimum || id > 4094)
        {
            throw new FormatException(allowDefault
                ? "VLAN id must be between 1 and 4094."
                : "VLAN id must be between 2 and 4094.");
        }
    }

    private static string RequiredPage(string page)
    {
        if (string.IsNullOrWhiteSpace(page))
        {
            throw new FormatException("A page name is required.");
        }

        return page.Trim();
    }
}
