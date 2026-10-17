using Nefarius.PowerConnect.Ews.Changes;
using Nefarius.PowerConnect.Ews.Common;
using Nefarius.PowerConnect.Ews.Ews;
using Nefarius.PowerConnect.Ews.Http;

namespace Nefarius.PowerConnect.Ews.Snmp;

/// <summary>SNMP globals, communities, and trap recipients.</summary>
public sealed class SnmpRepository
{
    private readonly SwitchClient _client;

    /// <summary>Creates a repository bound to a session.</summary>
    public SnmpRepository(SwitchClient client)
    {
        _client = client;
    }

    /// <summary>Reads authentication traps and alarms.</summary>
    public async Task<IReadOnlyDictionary<string, string>> GetGlobalAsync(CancellationToken cancellationToken = default)
    {
        FormPage page = await _client.GetFormAsync("/config/snmp_global.htm", cancellationToken).ConfigureAwait(false);
        return new Dictionary<string, string>
        {
            ["alarms"] = MibValues.IsEnabled(page.ValueOf("rndAlarmEnabling$scalar")) ? "on" : "off",
            ["auth-traps"] = MibValues.IsEnabled(page.ValueOf("snmpEnableAuthenTraps$scalar")) ? "on" : "off"
        };
    }

    /// <summary>Plans global SNMP flags.</summary>
    public async Task<ChangePlan> PlanGlobalAsync(bool? authTraps, bool? alarms, CancellationToken cancellationToken = default)
    {
        FormPage page = await _client.GetFormAsync("/config/snmp_global.htm", cancellationToken).ConfigureAwait(false);
        Dictionary<string, string> mutations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (alarms.HasValue)
        {
            mutations["rndAlarmEnabling$scalar"] = MibValues.Enable(alarms.Value);
        }

        if (authTraps.HasValue)
        {
            mutations["snmpEnableAuthenTraps$scalar"] = MibValues.Enable(authTraps.Value);
        }

        ChangePlan plan = new ChangePlan();
        plan.Add(FormChange.Post(_client, "Set SNMP globals", page, mutations, verifyPath: "/config/snmp_global.htm"));
        return plan;
    }

    /// <summary>Lists communities. The string is included because the operator asked to read configuration; logs redact it.</summary>
    public async Task<IReadOnlyList<IReadOnlyDictionary<string, string>>> ListCommunitiesAsync(CancellationToken cancellationToken = default)
    {
        FormPage page = await _client.GetFormAsync(CommunityUrl(), cancellationToken).ConfigureAwait(false);
        return page.Rows()
            .Select(row => (IReadOnlyDictionary<string, string>)new Dictionary<string, string>
            {
                ["community"] = row.Value("rndCommunityString") ?? "",
                ["station"] = row.Value("rndCommunityMngStationAddr") ?? "",
                ["access"] = AccessText(row.Value("rndCommunityAccess")),
                ["owner"] = row.Value("rndCommunityOwner") ?? ""
            })
            .Where(row => row["community"].Length > 0 || row["station"].Length > 0)
            .ToList();
    }

    /// <summary>Plans a community add or delete. Access is ro, rw, or su.</summary>
    public async Task<ChangePlan> PlanCommunityAsync(
        string community,
        string? access,
        string? station,
        bool delete,
        CancellationToken cancellationToken = default)
    {
        FormPage page = await _client.GetFormAsync(CommunityUrl(), cancellationToken).ConfigureAwait(false);
        Dictionary<string, string> mutations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["rndCommunityString$query"] = community,
            ["rndCommunityMngStationAddr$query"] = station ?? "0.0.0.0",
            ["rndCommunityStatus$query"] = delete ? "6" : "4",
            ["rndCommunityOwner$query"] = "manager"
        };
        if (access != null)
        {
            mutations["rndCommunityAccess$query"] = access.Trim().ToLowerInvariant() switch
            {
                "ro" or "read-only" => "1",
                "rw" or "read-write" => "2",
                "su" or "super" => "3",
                _ => throw new FormatException("Access must be ro, rw, or su.")
            };
        }

        ChangePlan plan = new ChangePlan();
        plan.Add(FormChange.Post(
            _client,
            (delete ? "Delete" : "Add") + " SNMP community",
            page,
            mutations));
        return plan;
    }

    /// <summary>Lists trap recipients.</summary>
    public async Task<FormPage> RecipientsPageAsync(CancellationToken cancellationToken = default)
    {
        return await _client.GetFormAsync(
            "/config/snmp_note.htm?[ComVT]Filter:(rndCommunityOwner='monitor'||rndCommunityOwner='monitor_manager')",
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Plans a trap recipient add or delete.</summary>
    public async Task<ChangePlan> PlanRecipientAsync(
        string community,
        string station,
        string? version,
        bool delete,
        CancellationToken cancellationToken = default)
    {
        FormPage page = await RecipientsPageAsync(cancellationToken).ConfigureAwait(false);
        Dictionary<string, string> mutations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["rndCommunityString$query"] = community,
            ["rndCommunityMngStationAddr$query"] = station,
            ["rndCommunityStatus$query"] = delete ? "6" : "4",
            ["rndCommunityOwner$query"] = "monitor"
        };
        if (version != null)
        {
            mutations["rndCommunityTrapsEnable$query"] = version;
        }

        ChangePlan plan = new ChangePlan();
        plan.Add(FormChange.Post(_client, (delete ? "Delete" : "Add") + " SNMP trap recipient " + station, page, mutations));
        return plan;
    }

    private static string CommunityUrl()
    {
        return "/config/s_v3.htm?[ComVT]Filter:(rndCommunityOwner='manager'||rndCommunityOwner='monitor_manager')";
    }

    private static string AccessText(string? value)
    {
        return value switch
        {
            "1" => "ro",
            "2" => "rw",
            "3" => "su",
            _ => value ?? ""
        };
    }
}
