using System.Globalization;

using Nefarius.PowerConnect.Ews.Changes;
using Nefarius.PowerConnect.Ews.Common;
using Nefarius.PowerConnect.Ews.Ews;
using Nefarius.PowerConnect.Ews.Http;

namespace Nefarius.PowerConnect.Ews.Multicast;

/// <summary>Bridge multicast filtering and IGMP snooping.</summary>
public sealed class MulticastRepository
{
    private readonly SwitchClient _client;

    /// <summary>Creates a repository bound to a session.</summary>
    public MulticastRepository(SwitchClient client)
    {
        _client = client;
    }

    /// <summary>Reads global multicast and IGMP snooping flags.</summary>
    public async Task<IReadOnlyDictionary<string, string>> GetGlobalAsync(CancellationToken cancellationToken = default)
    {
        FormPage page = await _client.GetFormAsync("/config/igmp_params.htm", cancellationToken).ConfigureAwait(false);
        return new Dictionary<string, string>
        {
            ["bridge-filtering"] = MibValues.IsEnabled(page.ValueOf("rlMacMulticastEnable")) ? "on" : "off",
            ["igmp-snooping"] = MibValues.IsEnabled(page.ValueOf("rlIgmpSnoopEnable")) ? "on" : "off"
        };
    }

    /// <summary>Plans global flag changes.</summary>
    public async Task<ChangePlan> PlanGlobalAsync(bool? bridgeFiltering, bool? igmpSnooping, CancellationToken cancellationToken = default)
    {
        FormPage page = await _client.GetFormAsync("/config/igmp_params.htm", cancellationToken).ConfigureAwait(false);
        Dictionary<string, string> mutations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (bridgeFiltering.HasValue)
        {
            mutations["rlMacMulticastEnable"] = MibValues.Enable(bridgeFiltering.Value);
        }

        if (igmpSnooping.HasValue)
        {
            mutations["rlIgmpSnoopEnable"] = MibValues.Enable(igmpSnooping.Value);
        }

        ChangePlan plan = new ChangePlan();
        plan.Add(FormChange.Post(_client, "Set multicast globals", page, mutations, verifyPath: "/config/igmp_params.htm"));
        return plan;
    }

    /// <summary>Lists bridge multicast groups.</summary>
    public async Task<FormPage> GroupsPageAsync(CancellationToken cancellationToken = default)
    {
        return await _client.GetFormAsync("/config/igmp_member_table.htm", cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    ///     Plans adding or deleting a group. Delete sets the remove checkbox path by posting row status where the page has it;
    ///     otherwise it posts <c>RemoveCBox</c>.
    /// </summary>
    public async Task<ChangePlan> PlanGroupAsync(
        int vlanId,
        string mac,
        IReadOnlyList<PortName> ports,
        bool delete,
        CancellationToken cancellationToken = default)
    {
        FormPage page = await GroupsPageAsync(cancellationToken).ConfigureAwait(false);
        Dictionary<string, string> mutations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["OneVlanId"] = MibValues.Number(vlanId),
            ["dot1qTpGroupAddress$select"] = mac
        };
        if (delete)
        {
            mutations["RemoveCBox"] = "on";
            mutations["OneVlanRemove"] = "on";
        }

        if (!delete && ports.Count > 0)
        {
            mutations["dot1qTpGroupEgressPorts$query"] = PortList.Encode(ports);
        }

        ChangePlan plan = new ChangePlan();
        plan.Add(FormChange.Post(
            _client,
            (delete ? "Delete" : "Add") + " multicast group " + mac + " in VLAN " + vlanId.ToString(CultureInfo.InvariantCulture),
            page,
            mutations));
        return plan;
    }

    /// <summary>Reads IGMP snooping for one VLAN.</summary>
    public async Task<IReadOnlyDictionary<string, string>> GetSnoopingAsync(int vlanId, CancellationToken cancellationToken = default)
    {
        FormPage page = await _client.GetFormAsync(SnoopUrl(vlanId), cancellationToken).ConfigureAwait(false);
        return new Dictionary<string, string>
        {
            ["vlan"] = MibValues.Number(vlanId),
            ["enable"] = page.Query("rlIgmpSnoopVlanEnable") ?? "",
            ["auto-learn"] = page.Query("rlIgmpSnoopVlanRouterLearn") ?? "",
            ["querier"] = page.Query("rlIgmpSnoopQuerierAdminEnable") ?? "",
            ["querier-address"] = page.Query("rlIgmpSnoopQuerierAdminAddr") ?? "",
            ["host-timeout"] = page.Query("rlIgmpSnoopVlanHostTimeOut") ?? "",
            ["router-timeout"] = page.Query("rlIgmpSnoopVlanRouterTimeOut") ?? "",
            ["leave-timeout"] = page.Query("rlIgmpSnoopVlanLeaveTimeOut") ?? ""
        };
    }

    /// <summary>Plans IGMP snooping settings for one VLAN.</summary>
    public async Task<ChangePlan> PlanSnoopingAsync(
        int vlanId,
        bool? enable,
        bool? autoLearn,
        int? hostTimeout,
        int? routerTimeout,
        int? leaveTimeout,
        bool? querier,
        string? querierAddress,
        CancellationToken cancellationToken = default)
    {
        FormPage page = await _client.GetFormAsync(SnoopUrl(vlanId), cancellationToken).ConfigureAwait(false);
        Dictionary<string, string> mutations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (enable.HasValue)
        {
            mutations["rlIgmpSnoopVlanEnable$query"] = MibValues.Enable(enable.Value);
        }

        if (autoLearn.HasValue)
        {
            mutations["rlIgmpSnoopVlanRouterLearn$query"] = MibValues.Enable(autoLearn.Value);
        }

        if (hostTimeout.HasValue)
        {
            mutations["rlIgmpSnoopVlanHostTimeOut$query"] = MibValues.Number(hostTimeout.Value);
        }

        if (routerTimeout.HasValue)
        {
            mutations["rlIgmpSnoopVlanRouterTimeOut$query"] = MibValues.Number(routerTimeout.Value);
        }

        if (leaveTimeout.HasValue)
        {
            mutations["rlIgmpSnoopVlanLeaveTimeOut$query"] = MibValues.Number(leaveTimeout.Value);
        }

        if (querier.HasValue)
        {
            mutations["rlIgmpSnoopQuerierAdminEnable$query"] = MibValues.Enable(querier.Value);
        }

        if (querierAddress != null)
        {
            mutations["rlIgmpSnoopQuerierAdminAddr$query"] = querierAddress;
        }

        ChangePlan plan = new ChangePlan();
        plan.Add(FormChange.Post(_client, "Set IGMP snooping on VLAN " + vlanId.ToString(CultureInfo.InvariantCulture), page, mutations, verifyPath: SnoopUrl(vlanId)));
        return plan;
    }

    /// <summary>Reads the forward-all page.</summary>
    public Task<FormPage> ForwardAllAsync(CancellationToken cancellationToken = default)
    {
        return _client.GetFormAsync("/config/igmp_router_table.htm", cancellationToken);
    }

    private static string SnoopUrl(int vlanId)
    {
        return EwsUrl.Config(
            "igmp_snooping.htm",
            new EwsUrl.Selector
            {
                Table = "IgmpSnoopingVT",
                Query = new[] { new KeyValuePair<string, string>("rlIgmpSnoopVlanTag", MibValues.Number(vlanId)) }
            });
    }
}
