using System.Text.Json;
using System.Text.Json.Serialization;

using Nefarius.PowerConnect.Ews.Changes;
using Nefarius.PowerConnect.Ews.Ews;
using Nefarius.PowerConnect.Ews.Http;

namespace Nefarius.PowerConnect.Ews.Snapshot;

/// <summary>
///     JSON snapshot of readable configuration fields. Operational counters and password fields are omitted.
/// </summary>
public sealed class ConfigSnapshot
{
    private static readonly string[] Pages =
    {
        "/config/general_asset.htm",
        "/config/general_versions.htm",
        "/config/secure_mode.htm",
        "/config/ipa.htm",
        "/config/sec_db.htm",
        "/config/radius_params.htm",
        "/config/snmp_global.htm",
        "/config/s_v3.htm?[ComVT]Filter:(rndCommunityOwner='manager'||rndCommunityOwner='monitor_manager')",
        "/config/snmp_note.htm?[ComVT]Filter:(rndCommunityOwner='monitor'||rndCommunityOwner='monitor_manager')",
        "/config/DHCP_properties.htm",
        "/config/Network_Pool.htm",
        "/config/ExcludedAddresses.htm?[DHCPexcludeVT]Filter:rlDhcpSrvDynamicLeaseTime=0",
        "/config/tun_general.htm",
        "/config/port_config.htm?[PortConfigTable]Query:ifIndex=1",
        "/config/trunk_set.htm",
        "/config/green_ethernet.htm",
        "/config/port_bprotect.htm?Query:dot1dBasePort=1",
        "/config/port_mirror.htm",
        "/config/sta_bridge.htm",
        "/config/sta_port.htm?Query:rlPhdPortsIfIndex=1",
        "/config/vlan_member.htm",
        "/config/vlan_iterface.htm?[dot1qPortVlanTableVT]Query:dot1dBasePort=1",
        "/config/igmp_params.htm",
        "/config/igmp_snooping.htm",
        "/config/cosqos_global.htm",
        "/config/cosqos_queue.htm",
        "/config/cosqos_to.htm",
        "/config/cosqos_dscp.htm",
        "/config/security_pauth.htm?[securityPortAuthTable]Query:dot1xPaePortNumber=1"
    };

    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>Page path to field name/value.</summary>
    public Dictionary<string, Dictionary<string, string>> PagesFields { get; set; } = new Dictionary<string, Dictionary<string, string>>();

    /// <summary>Reads every catalogued page.</summary>
    public static async Task<ConfigSnapshot> CaptureAsync(SwitchClient client, CancellationToken cancellationToken = default)
    {
        ConfigSnapshot snapshot = new ConfigSnapshot();
        foreach (string path in Pages)
        {
            FormPage page = await client.GetFormAsync(path, cancellationToken).ConfigureAwait(false);
            Dictionary<string, string> fields = new Dictionary<string, string>();
            foreach (FormField field in page.Fields)
            {
                if (!Include(field))
                {
                    continue;
                }

                fields[field.Name] = field.Value;
            }

            snapshot.PagesFields[path] = fields;
        }

        return snapshot;
    }

    /// <summary>Loads a snapshot from JSON.</summary>
    public static async Task<ConfigSnapshot> LoadAsync(string path, CancellationToken cancellationToken = default)
    {
        await using FileStream stream = File.OpenRead(path);
        ConfigSnapshot? snapshot = await JsonSerializer.DeserializeAsync<ConfigSnapshot>(stream, JsonOptions, cancellationToken).ConfigureAwait(false);
        return snapshot ?? new ConfigSnapshot();
    }

    /// <summary>Writes the snapshot as JSON.</summary>
    public async Task SaveAsync(string path, CancellationToken cancellationToken = default)
    {
        await using FileStream stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, this, JsonOptions, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    ///     Compares this snapshot to the live device and plans a raw replay of fields that differ.
    ///     Password fields and destructive pages (reset, secure mode, IP) are not applied.
    /// </summary>
    public async Task<ChangePlan> PlanApplyAsync(SwitchClient client, CancellationToken cancellationToken = default)
    {
        ChangePlan plan = new ChangePlan();
        foreach (KeyValuePair<string, Dictionary<string, string>> pageFields in PagesFields)
        {
            if (IsProtected(pageFields.Key))
            {
                continue;
            }

            FormPage live = await client.GetFormAsync(pageFields.Key, cancellationToken).ConfigureAwait(false);
            Dictionary<string, string> mutations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, string> field in pageFields.Value)
            {
                if (field.Key.Contains("password", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string current = live.ValueOf(field.Key) ?? "";
                if (!string.Equals(current, field.Value, StringComparison.Ordinal))
                {
                    mutations[field.Key] = field.Value;
                }
            }

            if (mutations.Count == 0)
            {
                continue;
            }

            plan.Add(FormChange.Post(client, "Apply " + pageFields.Key, live, mutations));
        }

        return plan;
    }

    private static bool Include(FormField field)
    {
        if (field.Role is FieldRole.Metadata or FieldRole.Marker or FieldRole.Select)
        {
            return false;
        }

        if (field.Disabled)
        {
            return false;
        }

        if (field.Name.Contains("password", StringComparison.OrdinalIgnoreCase) ||
            field.Name.Contains("UpTime", StringComparison.OrdinalIgnoreCase) ||
            field.Name.Contains("Oper", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }

    private static bool IsProtected(string path)
    {
        return path.Contains("switch_reset", StringComparison.OrdinalIgnoreCase) ||
               path.Contains("software_active", StringComparison.OrdinalIgnoreCase) ||
               path.Contains("software_dl", StringComparison.OrdinalIgnoreCase) ||
               path.Contains("secure_mode", StringComparison.OrdinalIgnoreCase) ||
               path.Contains("ipa.htm", StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>Escape hatch that dumps or replays any page.</summary>
public sealed class RawRepository
{
    private readonly SwitchClient _client;

    /// <summary>Creates a repository bound to a session.</summary>
    public RawRepository(SwitchClient client)
    {
        _client = client;
    }

    /// <summary>Downloads and parses a page. <paramref name="page"/> may be a file name or a full <c>/config</c> path.</summary>
    public Task<FormPage> GetAsync(string page, CancellationToken cancellationToken = default)
    {
        string path = page.StartsWith('/') ? page : "/config/" + page;
        return _client.GetFormAsync(path, cancellationToken);
    }

    /// <summary>Plans a raw field replay.</summary>
    public async Task<ChangePlan> PlanSetAsync(
        string page,
        IReadOnlyDictionary<string, string> mutations,
        bool destructive,
        CancellationToken cancellationToken = default)
    {
        FormPage form = await GetAsync(page, cancellationToken).ConfigureAwait(false);
        ChangePlan plan = new ChangePlan();
        plan.Add(FormChange.Post(_client, "Raw set " + page, form, mutations, destructive: destructive));
        return plan;
    }
}
