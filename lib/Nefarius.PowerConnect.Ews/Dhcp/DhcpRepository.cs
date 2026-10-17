using Nefarius.PowerConnect.Ews.Changes;
using Nefarius.PowerConnect.Ews.Common;
using Nefarius.PowerConnect.Ews.Ews;
using Nefarius.PowerConnect.Ews.Http;

namespace Nefarius.PowerConnect.Ews.Dhcp;

/// <summary>Create, update, or delete one static DHCP host.</summary>
public enum DhcpStaticAction
{
    /// <summary>Post the <c>$add</c> fields with create-and-go status.</summary>
    Add,

    /// <summary>Post the selected row's <c>$query</c> fields with active status.</summary>
    Update,

    /// <summary>Post the selected row's <c>$query</c> fields with destroy status.</summary>
    Delete
}

/// <summary>On-box DHCP server.</summary>
public sealed class DhcpRepository
{
    private readonly SwitchClient _client;

    /// <summary>Creates a repository bound to a session.</summary>
    public DhcpRepository(SwitchClient client)
    {
        _client = client;
    }

    /// <summary>Reads server enable and ICMP probe settings.</summary>
    public async Task<IReadOnlyDictionary<string, string>> GetPropertiesAsync(CancellationToken cancellationToken = default)
    {
        FormPage page = await _client.GetFormAsync("/config/DHCP_properties.htm", cancellationToken).ConfigureAwait(false);
        return new Dictionary<string, string>
        {
            ["enable"] = page.ValueOf("rlDhcpSrvEnable") ?? "",
            ["probe"] = page.ValueOf("rlDhcpSrvProbeEnable") ?? "",
            ["probe-retries"] = page.ValueOf("rlDhcpSrvProbeRetries") ?? "",
            ["probe-timeout"] = page.ValueOf("rlDhcpSrvProbeTimeout") ?? ""
        };
    }

    /// <summary>Plans property changes.</summary>
    public async Task<ChangePlan> PlanPropertiesAsync(
        bool? enable,
        bool? probe,
        int? retries,
        int? timeout,
        CancellationToken cancellationToken = default)
    {
        FormPage page = await _client.GetFormAsync("/config/DHCP_properties.htm", cancellationToken).ConfigureAwait(false);
        Dictionary<string, string> mutations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (enable.HasValue)
        {
            mutations["rlDhcpSrvEnable"] = MibValues.Enable(enable.Value);
        }

        if (probe.HasValue)
        {
            mutations["rlDhcpSrvProbeEnable"] = MibValues.Enable(probe.Value);
        }

        if (retries.HasValue)
        {
            mutations["rlDhcpSrvProbeRetries"] = MibValues.Number(retries.Value);
        }

        if (timeout.HasValue)
        {
            mutations["rlDhcpSrvProbeTimeout"] = MibValues.Number(timeout.Value);
        }

        ChangePlan plan = new ChangePlan();
        plan.Add(FormChange.Post(_client, "Set DHCP server properties", page, mutations, verifyPath: "/config/DHCP_properties.htm"));
        return plan;
    }

    /// <summary>Reads the network pool page.</summary>
    public Task<FormPage> PoolAsync(CancellationToken cancellationToken = default)
    {
        return _client.GetFormAsync("/config/Network_Pool.htm", cancellationToken);
    }

    /// <summary>Plans pool address range changes.</summary>
    public async Task<ChangePlan> PlanPoolAsync(
        string? start,
        string? end,
        string? mask,
        CancellationToken cancellationToken = default)
    {
        FormPage page = await PoolAsync(cancellationToken).ConfigureAwait(false);
        Dictionary<string, string> mutations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (start != null)
        {
            mutations["rlDhcpSrvDynamicIpAddrFrom$query"] = start;
        }

        if (end != null)
        {
            mutations["rlDhcpSrvDynamicIpAddrTo$query"] = end;
        }

        if (mask != null)
        {
            mutations["rlDhcpSrvDynamicIpNetMask$query"] = mask;
        }

        ChangePlan plan = new ChangePlan();
        plan.Add(FormChange.Post(_client, "Set DHCP pool", page, mutations));
        return plan;
    }

    /// <summary>Lists excluded ranges (lease time 0).</summary>
    public async Task<IReadOnlyList<IReadOnlyDictionary<string, string>>> ListExcludedAsync(CancellationToken cancellationToken = default)
    {
        FormPage page = await ExcludedPageAsync(cancellationToken).ConfigureAwait(false);
        return page.Rows().Select(row => (IReadOnlyDictionary<string, string>)new Dictionary<string, string>
        {
            ["from"] = row.Value("rlDhcpSrvDynamicIpAddrFrom") ?? "",
            ["to"] = row.Value("rlDhcpSrvDynamicIpAddrTo") ?? "",
            ["pool"] = row.Value("rlDhcpSrvDynamicPoolName") ?? ""
        }).Where(row => row["from"].Length > 0).ToList();
    }

    /// <summary>Plans an excluded-range add or delete.</summary>
    public async Task<ChangePlan> PlanExcludedAsync(
        string from,
        string to,
        bool delete,
        CancellationToken cancellationToken = default)
    {
        FormPage page = await ExcludedPageAsync(cancellationToken).ConfigureAwait(false);
        Dictionary<string, string> mutations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["rlDhcpSrvDynamicIpAddrFrom$add"] = from,
            ["rlDhcpSrvDynamicIpAddrTo$add"] = to,
            ["rlDhcpSrvDynamicLeaseTime$add"] = "0",
            ["rlDhcpSrvDynamicRowStatus$add"] = delete ? "6" : "4",
            ["rlDhcpSrvDynamicPoolName$add"] = "pool"
        };
        if (delete)
        {
            FormRow? row = page.Rows().FirstOrDefault(candidate =>
                candidate.Value("rlDhcpSrvDynamicIpAddrFrom") == from &&
                candidate.Value("rlDhcpSrvDynamicIpAddrTo") == to);
            if (row != null)
            {
                string index = row.Index.ToString(System.Globalization.CultureInfo.InvariantCulture);
                mutations["rlDhcpSrvDynamicRowStatus$repeat11?" + index] = "6";
            }
        }

        ChangePlan plan = new ChangePlan();
        plan.Add(FormChange.Post(_client, (delete ? "Delete" : "Add") + " DHCP exclusion " + from + "-" + to, page, mutations));
        return plan;
    }

    /// <summary>Reads static hosts.</summary>
    public Task<FormPage> StaticHostsAsync(CancellationToken cancellationToken = default)
    {
        return _client.GetFormAsync(
            "/config/DHCPSrv_Static_Host.htm?[DHCPIPVT]Filter:rlDhcpSrvIpAddrMechanism=1",
            cancellationToken);
    }

    /// <summary>Plans a static host create, update, or delete.</summary>
    public async Task<ChangePlan> PlanStaticAsync(
        string address,
        string mac,
        string? name,
        DhcpStaticAction action,
        CancellationToken cancellationToken = default)
    {
        bool existing = action != DhcpStaticAction.Add;
        string path = existing
            ? "/config/DHCPSrv_Static_Host.htm?[DHCPIPVT]Filter:rlDhcpSrvIpAddrMechanism=1^Query:rlDhcpSrvIpAddrIpAddr=" + Uri.EscapeDataString(address)
            : "/config/DHCPSrv_Static_Host.htm?[DHCPIPVT]Filter:rlDhcpSrvIpAddrMechanism=1";
        FormPage page = await _client.GetFormAsync(path, cancellationToken).ConfigureAwait(false);
        string suffix = existing ? "$query" : "$add";
        string status = action switch
        {
            DhcpStaticAction.Delete => "6",
            DhcpStaticAction.Update => "1",
            _ => "4"
        };
        Dictionary<string, string> mutations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["rlDhcpSrvIpAddrIpAddr" + suffix] = address,
            ["rlDhcpSrvIpAddrRowStatus" + suffix] = status
        };
        if (action == DhcpStaticAction.Add || mac.Length > 0)
        {
            mutations["rlDhcpSrvIpAddrIdentifier" + suffix] = mac;
        }

        if (name != null)
        {
            mutations["rlDhcpSrvIpAddrClnHostName" + suffix] = name;
        }

        string verb = action switch
        {
            DhcpStaticAction.Delete => "Delete",
            DhcpStaticAction.Update => "Update",
            _ => "Add"
        };
        ChangePlan plan = new ChangePlan();
        plan.Add(FormChange.Post(_client, verb + " DHCP static host " + address, page, mutations));
        return plan;
    }

    /// <summary>Reads address bindings.</summary>
    public Task<FormPage> BindingsAsync(CancellationToken cancellationToken = default)
    {
        return _client.GetFormAsync("/config/DHCPSrv_AddrBind.htm", cancellationToken);
    }

    private Task<FormPage> ExcludedPageAsync(CancellationToken cancellationToken)
    {
        return _client.GetFormAsync(
            "/config/ExcludedAddresses.htm?[DHCPexcludeVT]Filter:rlDhcpSrvDynamicLeaseTime=0",
            cancellationToken);
    }
}
