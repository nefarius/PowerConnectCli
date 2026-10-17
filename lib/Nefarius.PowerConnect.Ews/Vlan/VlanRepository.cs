using System.Globalization;

using Nefarius.PowerConnect.Ews.Changes;
using Nefarius.PowerConnect.Ews.Common;
using Nefarius.PowerConnect.Ews.Ews;
using Nefarius.PowerConnect.Ews.Http;

namespace Nefarius.PowerConnect.Ews.Vlan;

/// <summary>VLAN id and name.</summary>
public sealed class VlanSummary
{
    /// <summary>VLAN id.</summary>
    public int Id { get; init; }

    /// <summary>Configured name. VLAN 1 is reported as Default when the page leaves it blank.</summary>
    public string Name { get; init; } = "";
}

/// <summary>Static membership of one VLAN.</summary>
public sealed class VlanDetails
{
    /// <summary>VLAN id.</summary>
    public int Id { get; init; }

    /// <summary>Configured name.</summary>
    public string Name { get; init; } = "";

    /// <summary>Ports in the static egress set, tagged and untagged.</summary>
    public IReadOnlyList<string> Egress { get; init; } = Array.Empty<string>();

    /// <summary>Untagged member ports. These are also in <see cref="Egress"/>.</summary>
    public IReadOnlyList<string> Untagged { get; init; } = Array.Empty<string>();

    /// <summary>Ports forbidden from this VLAN.</summary>
    public IReadOnlyList<string> Forbidden { get; init; } = Array.Empty<string>();

    /// <summary>Row status value.</summary>
    public string RowStatus { get; init; } = "";
}

/// <summary>PVID and ingress settings for one port or LAG.</summary>
public sealed class VlanPortSettings
{
    /// <summary>Port name.</summary>
    public string Port { get; init; } = "";

    /// <summary>Port VLAN id.</summary>
    public int Pvid { get; init; }

    /// <summary><c>admit-all</c> or <c>tagged-only</c>.</summary>
    public string FrameType { get; init; } = "";

    /// <summary>True when ingress filtering is enabled.</summary>
    public bool IngressFiltering { get; init; }
}

/// <summary>
///     VLAN membership and per-port VLAN settings.
/// </summary>
public sealed class VlanRepository
{
    /// <summary>
    ///     Controls <c>formSubmit</c> disables before an edit. The switch rejects the post when those
    ///     client-only fields, especially <c>indexNameRadio</c>, are included. The <c>$add</c> block is for create.
    /// </summary>
    internal static readonly string[] OmittedOnEdit =
    {
        "indexNameRadio",
        "dot1qVlanIndex$select",
        "dot1qVlanStaticName$select",
        "String$select",
        "Default$select",
        "dot1qVlanName",
        "dot1qVlanStatus$query",
        "Unauthorized",
        "RemoveCBox",
        "dot1qVlanIndex$add",
        "String$add",
        "dot1qVlanStaticName$add",
        "Default$add",
        "dot1qVlanStaticEgressPorts$add",
        "dot1qVlanForbiddenEgressPorts$add",
        "dot1qVlanStaticUntaggedPorts$add",
        "dot1qVlanStaticRowStatus$add",
        "VlanMemberStaticVT$endAdd"
    };

    private readonly SwitchClient _client;

    /// <summary>Creates a repository bound to a session.</summary>
    public VlanRepository(SwitchClient client)
    {
        _client = client;
    }

    /// <summary>Lists VLAN ids and names from the membership page.</summary>
    public async Task<IReadOnlyList<VlanSummary>> ListAsync(CancellationToken cancellationToken = default)
    {
        FormPage page = await _client.GetFormAsync("/config/vlan_member.htm", cancellationToken).ConfigureAwait(false);
        Dictionary<int, string> names = new Dictionary<int, string>();
        foreach (FormRow row in page.Rows())
        {
            string? idText = row.Value("dot1qVlanIndex");
            if (!int.TryParse(idText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int id))
            {
                continue;
            }

            names[id] = row.Value("dot1qVlanStaticName") ?? "";
        }

        foreach (SelectOption option in page.Options("dot1qVlanIndex$select"))
        {
            if (int.TryParse(option.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int id) &&
                !names.ContainsKey(id))
            {
                names[id] = id == 1 ? "Default" : "";
            }
        }

        if (!names.ContainsKey(1))
        {
            names[1] = "Default";
        }
        else if (string.IsNullOrWhiteSpace(names[1]))
        {
            names[1] = "Default";
        }

        return names
            .OrderBy(pair => pair.Key)
            .Select(pair => new VlanSummary { Id = pair.Key, Name = pair.Value })
            .ToList();
    }

    /// <summary>Reads static membership for one VLAN.</summary>
    public async Task<VlanDetails> GetAsync(int vlanId, CancellationToken cancellationToken = default)
    {
        FormPage page = await LoadMembershipAsync(vlanId, cancellationToken).ConfigureAwait(false);
        string egress = page.Query("dot1qVlanStaticEgressPorts") ?? "";
        string untagged = page.Query("dot1qVlanStaticUntaggedPorts") ?? "";
        string forbidden = page.Query("dot1qVlanForbiddenEgressPorts") ?? "";
        return new VlanDetails
        {
            Id = vlanId,
            Name = page.Query("dot1qVlanStaticName") ?? "",
            Egress = PortList.Decode(egress).Select(port => port.Name).ToList(),
            Untagged = PortList.Decode(untagged).Select(port => port.Name).ToList(),
            Forbidden = PortList.Decode(forbidden).Select(port => port.Name).ToList(),
            RowStatus = page.Query("dot1qVlanStaticRowStatus") ?? ""
        };
    }

    /// <summary>Plans creation of a VLAN. Row status is createAndGo (4).</summary>
    public async Task<ChangePlan> PlanCreateAsync(int vlanId, string name, CancellationToken cancellationToken = default)
    {
        if (vlanId is < 2 or > 4094)
        {
            throw new SwitchException("VLAN id must be between 2 and 4094.");
        }

        FormPage page = await _client.GetFormAsync("/config/create_new_vlan.htm", cancellationToken).ConfigureAwait(false);
        Dictionary<string, string> mutations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["dot1qVlanIndex$add"] = MibValues.Number(vlanId),
            ["dot1qVlanStaticName$add"] = name,
            ["dot1qVlanStaticRowStatus$add"] = "4"
        };
        ChangePlan plan = new ChangePlan();
        plan.Add(FormChange.Post(
            _client,
            "Create VLAN " + vlanId.ToString(CultureInfo.InvariantCulture) + " (" + name + ")",
            page,
            mutations,
            verifyPath: MembershipUrl(vlanId),
            verify: after =>
            {
                bool created = after.Query("dot1qVlanIndex$queryStatic") == MibValues.Number(vlanId) ||
                               after.Query("dot1qVlanStaticName") == name;
                return created
                    ? null
                    : "VLAN " + vlanId.ToString(CultureInfo.InvariantCulture) + " was not readable after create";
            }));
        return plan;
    }

    /// <summary>Plans deletion. VLAN 1 cannot be deleted.</summary>
    public async Task<ChangePlan> PlanDeleteAsync(int vlanId, CancellationToken cancellationToken = default)
    {
        if (vlanId == 1)
        {
            throw new SwitchException("VLAN 1 cannot be deleted.");
        }

        FormPage page = await LoadMembershipAsync(vlanId, cancellationToken).ConfigureAwait(false);
        Dictionary<string, string> mutations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["dot1qVlanStaticRowStatus$query"] = "6"
        };
        ChangePlan plan = new ChangePlan();
        plan.Add(FormChange.Post(
            _client,
            "Delete VLAN " + vlanId.ToString(CultureInfo.InvariantCulture),
            page,
            mutations,
            omit: OmittedOnEdit));
        return plan;
    }

    /// <summary>
    ///     Plans a membership edit. Untagged ports are also egress. <paramref name="none"/> removes the port
    ///     from egress, untagged, and forbidden.
    /// </summary>
    public async Task<ChangePlan> PlanSetAsync(
        int vlanId,
        IReadOnlyList<PortName> tagged,
        IReadOnlyList<PortName> untagged,
        IReadOnlyList<PortName> forbidden,
        IReadOnlyList<PortName> none,
        CancellationToken cancellationToken = default)
    {
        FormPage page = await LoadMembershipAsync(vlanId, cancellationToken).ConfigureAwait(false);
        (string egress, string untaggedHex, string forbiddenHex) = PortList.UpdateMembership(
            page.Query("dot1qVlanStaticEgressPorts"),
            page.Query("dot1qVlanStaticUntaggedPorts"),
            page.Query("dot1qVlanForbiddenEgressPorts"),
            tagged,
            untagged,
            forbidden,
            none);
        Dictionary<string, string> mutations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["dot1qVlanStaticEgressPorts$query"] = egress,
            ["dot1qVlanStaticUntaggedPorts$query"] = untaggedHex,
            ["dot1qVlanForbiddenEgressPorts$query"] = forbiddenHex,
            ["dot1qVlanStaticRowStatus$query"] = "1"
        };
        ChangePlan plan = new ChangePlan();
        plan.Add(FormChange.Post(
            _client,
            "Set membership of VLAN " + vlanId.ToString(CultureInfo.InvariantCulture),
            page,
            mutations,
            verifyPath: MembershipUrl(vlanId),
            verify: after =>
            {
                if (after.Query("dot1qVlanStaticEgressPorts") != egress)
                {
                    return "Egress bitmap did not stick";
                }

                if (after.Query("dot1qVlanStaticUntaggedPorts") != untaggedHex)
                {
                    return "Untagged bitmap did not stick";
                }

                if (after.Query("dot1qVlanForbiddenEgressPorts") != forbiddenHex)
                {
                    return "Forbidden bitmap did not stick";
                }

                return null;
            },
            omit: OmittedOnEdit));
        return plan;
    }

    /// <summary>Reads PVID, acceptable frame type, and ingress filtering.</summary>
    public async Task<VlanPortSettings> GetPortAsync(PortName port, CancellationToken cancellationToken = default)
    {
        FormPage page = await _client.GetFormAsync(PortUrl(port), cancellationToken).ConfigureAwait(false);
        string frame = page.Query("dot1qPortAcceptableFrameTypes") ?? "1";
        return new VlanPortSettings
        {
            Port = port.Name,
            Pvid = int.TryParse(page.Query("dot1qPvid"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int pvid) ? pvid : 1,
            FrameType = frame == "2" ? "tagged-only" : "admit-all",
            IngressFiltering = MibValues.IsEnabled(page.Query("dot1qPortIngressFiltering"))
        };
    }

    /// <summary>Reads VLAN port settings for every copper port and LAG.</summary>
    public async Task<IReadOnlyList<VlanPortSettings>> ListPortsAsync(CancellationToken cancellationToken = default)
    {
        List<VlanPortSettings> result = new List<VlanPortSettings>();
        for (int index = 1; index <= PortName.PortCount; index++)
        {
            result.Add(await GetPortAsync(PortName.Parse("g" + index.ToString(CultureInfo.InvariantCulture)), cancellationToken).ConfigureAwait(false));
        }

        for (int index = 1; index <= PortName.LagCount; index++)
        {
            result.Add(await GetPortAsync(PortName.Parse("ch" + index.ToString(CultureInfo.InvariantCulture)), cancellationToken).ConfigureAwait(false));
        }

        return result;
    }

    /// <summary>Plans PVID, frame type, and ingress filtering changes for one port or LAG.</summary>
    public async Task<ChangePlan> PlanPortSetAsync(
        PortName port,
        int? pvid,
        string? frameType,
        bool? ingressFiltering,
        CancellationToken cancellationToken = default)
    {
        FormPage page = await _client.GetFormAsync(PortUrl(port), cancellationToken).ConfigureAwait(false);
        Dictionary<string, string> mutations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (pvid.HasValue)
        {
            mutations["dot1qPvid$query"] = MibValues.Number(pvid.Value);
        }

        if (frameType != null)
        {
            string normalized = frameType.Trim().ToLowerInvariant();
            mutations["dot1qPortAcceptableFrameTypes$query"] = normalized switch
            {
                "tagged-only" or "tagged" => "2",
                "admit-all" or "all" => "1",
                _ => throw new FormatException("Frame type must be admit-all or tagged-only.")
            };
        }

        if (ingressFiltering.HasValue)
        {
            mutations["dot1qPortIngressFiltering$query"] = MibValues.Enable(ingressFiltering.Value);
        }

        ChangePlan plan = new ChangePlan();
        plan.Add(FormChange.Post(
            _client,
            "Set VLAN options on " + port.Name,
            page,
            mutations,
            verifyPath: PortUrl(port),
            verify: after =>
            {
                if (pvid.HasValue && after.Query("dot1qPvid") != MibValues.Number(pvid.Value))
                {
                    return "PVID did not stick";
                }

                return null;
            }));
        return plan;
    }

    /// <summary>
    ///     Puts a port into one VLAN as untagged, removes it from every other VLAN, and sets the PVID.
    /// </summary>
    public async Task<ChangePlan> PlanAccessAsync(PortName port, int vlanId, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<VlanSummary> vlans = await ListAsync(cancellationToken).ConfigureAwait(false);
        ChangePlan plan = new ChangePlan();
        ChangePlan add = await PlanSetAsync(
            vlanId,
            Array.Empty<PortName>(),
            new[] { port },
            Array.Empty<PortName>(),
            Array.Empty<PortName>(),
            cancellationToken).ConfigureAwait(false);
        foreach (ChangeStep step in add.Steps)
        {
            plan.Add(step);
        }

        ChangePlan pvid = await PlanPortSetAsync(port, vlanId, "admit-all", true, cancellationToken).ConfigureAwait(false);
        foreach (ChangeStep step in pvid.Steps)
        {
            plan.Add(step);
        }

        foreach (VlanSummary vlan in vlans)
        {
            if (vlan.Id == vlanId)
            {
                continue;
            }

            VlanDetails details = await GetAsync(vlan.Id, cancellationToken).ConfigureAwait(false);
            if (!details.Egress.Contains(port.Name, StringComparer.OrdinalIgnoreCase) &&
                !details.Untagged.Contains(port.Name, StringComparer.OrdinalIgnoreCase) &&
                !details.Forbidden.Contains(port.Name, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            ChangePlan removal = await PlanSetAsync(
                vlan.Id,
                Array.Empty<PortName>(),
                Array.Empty<PortName>(),
                Array.Empty<PortName>(),
                new[] { port },
                cancellationToken).ConfigureAwait(false);
            foreach (ChangeStep step in removal.Steps)
            {
                plan.Add(step);
            }
        }

        return plan;
    }

    private Task<FormPage> LoadMembershipAsync(int vlanId, CancellationToken cancellationToken)
    {
        return _client.GetFormAsync(MembershipUrl(vlanId), cancellationToken);
    }

    private static string MembershipUrl(int vlanId)
    {
        string id = MibValues.Number(vlanId);
        return EwsUrl.Config(
            "vlan_member.htm",
            new EwsUrl.Selector
            {
                Table = "VlanMemberStaticVT",
                Query = new[] { new KeyValuePair<string, string>("dot1qVlanIndex", id) }
            },
            new EwsUrl.Selector
            {
                Table = "nameVT",
                Filter = "dot1qVlanStaticName!=''"
            },
            new EwsUrl.Selector
            {
                Table = "VlanMemberCurrentVT",
                Query = new[]
                {
                    new KeyValuePair<string, string>("dot1qVlanTimeMark", "0"),
                    new KeyValuePair<string, string>("dot1qVlanIndex", id)
                }
            });
    }

    private static string PortUrl(PortName port)
    {
        string page = port.IsLag ? "vlan_trunk.htm" : "vlan_iterface.htm";
        string table = port.IsLag ? "" : "dot1qPortVlanTableVT";
        if (port.IsLag)
        {
            return "/config/vlan_trunk.htm?Query:dot1dBasePort=" + MibValues.Number(port.IfIndex);
        }

        return EwsUrl.Query(page, table, "dot1dBasePort", MibValues.Number(port.IfIndex));
    }
}
