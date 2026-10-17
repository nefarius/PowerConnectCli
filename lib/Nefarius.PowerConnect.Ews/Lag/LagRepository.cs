using System.Globalization;

using Nefarius.PowerConnect.Ews.Changes;
using Nefarius.PowerConnect.Ews.Common;
using Nefarius.PowerConnect.Ews.Ews;
using Nefarius.PowerConnect.Ews.Http;

namespace Nefarius.PowerConnect.Ews.Lag;

/// <summary>One LAG and the ports whose admin key points at it.</summary>
public sealed class LagInfo
{
    /// <summary>LAG name, <c>ch1</c> through <c>ch8</c>.</summary>
    public string Name { get; init; } = "";

    /// <summary>ifIndex, 1000 through 1007.</summary>
    public int IfIndex { get; init; }

    /// <summary>Description.</summary>
    public string Description { get; init; } = "";

    /// <summary>Load balance layer: l2, l3, or l2l3.</summary>
    public string Balance { get; init; } = "";

    /// <summary>Member port names.</summary>
    public IReadOnlyList<string> Members { get; init; } = Array.Empty<string>();
}

/// <summary>LAG configuration and membership.</summary>
public sealed class LagRepository
{
    private readonly SwitchClient _client;

    /// <summary>Creates a repository bound to a session.</summary>
    public LagRepository(SwitchClient client)
    {
        _client = client;
    }

    /// <summary>Lists ch1 through ch8.</summary>
    public async Task<IReadOnlyList<LagInfo>> ListAsync(CancellationToken cancellationToken = default)
    {
        FormPage members = await _client.GetFormAsync("/config/trunk_set.htm", cancellationToken).ConfigureAwait(false);
        List<LagInfo> lags = new List<LagInfo>();
        for (int index = 1; index <= PortName.LagCount; index++)
        {
            PortName lag = PortName.Parse("ch" + index.ToString(CultureInfo.InvariantCulture));
            FormPage page = await _client.GetFormAsync(ConfigUrl(lag), cancellationToken).ConfigureAwait(false);
            lags.Add(Map(lag, page, MembersOf(members, lag.IfIndex)));
        }

        return lags;
    }

    /// <summary>Reads one LAG.</summary>
    public async Task<LagInfo> GetAsync(PortName lag, CancellationToken cancellationToken = default)
    {
        FormPage page = await _client.GetFormAsync(ConfigUrl(lag), cancellationToken).ConfigureAwait(false);
        FormPage members = await _client.GetFormAsync("/config/trunk_set.htm", cancellationToken).ConfigureAwait(false);
        return Map(lag, page, MembersOf(members, lag.IfIndex));
    }

    /// <summary>Plans description and load-balance changes.</summary>
    public async Task<ChangePlan> PlanSetAsync(
        PortName lag,
        string? description,
        string? balance,
        CancellationToken cancellationToken = default)
    {
        FormPage page = await _client.GetFormAsync(ConfigUrl(lag), cancellationToken).ConfigureAwait(false);
        Dictionary<string, string> mutations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (description != null)
        {
            mutations["ifAlias$query"] = description;
        }

        if (balance != null)
        {
            mutations["rlDot3adAggBalanceLayer$query"] = balance.Trim().ToLowerInvariant() switch
            {
                "l2" => "2",
                "l3" => "3",
                "l2l3" or "l2/l3" => "1",
                _ => throw new FormatException("Balance must be l2, l3, or l2l3.")
            };
        }

        ChangePlan plan = new ChangePlan();
        plan.Add(FormChange.Post(_client, "Configure " + lag.Name, page, mutations, verifyPath: ConfigUrl(lag)));
        return plan;
    }

    /// <summary>
    ///     Plans membership. Ports in <paramref name="ports"/> get this LAG's admin key.
    ///     Ports that were members and are absent are set back to 0. <paramref name="lacp"/> sets the actor admin state bits.
    /// </summary>
    public async Task<ChangePlan> PlanMembersAsync(
        PortName lag,
        IReadOnlyList<PortName> ports,
        bool lacp,
        CancellationToken cancellationToken = default)
    {
        FormPage page = await _client.GetFormAsync("/config/trunk_set.htm", cancellationToken).ConfigureAwait(false);
        HashSet<int> wanted = ports.Select(port => port.IfIndex).ToHashSet();
        Dictionary<string, string> mutations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (FormRow row in page.Rows())
        {
            if (!int.TryParse(row.Value("dot3adAggPortIndex"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int ifIndex))
            {
                continue;
            }

            if (ifIndex > PortName.PortCount)
            {
                continue;
            }

            int currentKey = int.TryParse(row.Value("dot3adAggPortActorAdminKey"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int key)
                ? key
                : 0;
            bool include = wanted.Contains(ifIndex);
            bool wasMember = currentKey == lag.IfIndex;
            if (!include && !wasMember)
            {
                continue;
            }

            string keyField = "dot3adAggPortActorAdminKey$repeat?" + row.Index.ToString(CultureInfo.InvariantCulture);
            mutations[keyField] = include ? MibValues.Number(lag.IfIndex) : "0";
            if (lacp && include)
            {
                mutations["dot3adAggPortActorAdminState$repeat?" + row.Index.ToString(CultureInfo.InvariantCulture)] = "00100111";
            }
        }

        ChangePlan plan = new ChangePlan();
        plan.Add(FormChange.Post(
            _client,
            "Set members of " + lag.Name + (lacp ? " (LACP)" : ""),
            page,
            mutations,
            verifyPath: "/config/trunk_set.htm"));
        return plan;
    }

    private static IReadOnlyList<string> MembersOf(FormPage page, int lagIfIndex)
    {
        List<string> members = new List<string>();
        foreach (FormRow row in page.Rows())
        {
            if (row.Value("dot3adAggPortActorAdminKey") != MibValues.Number(lagIfIndex))
            {
                continue;
            }

            if (int.TryParse(row.Value("dot3adAggPortIndex"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int ifIndex) &&
                PortName.TryFromIfIndex(ifIndex, out PortName port))
            {
                members.Add(port.Name);
            }
        }

        return members;
    }

    private static LagInfo Map(PortName lag, FormPage page, IReadOnlyList<string> members)
    {
        string balance = page.Query("rlDot3adAggBalanceLayer") switch
        {
            "2" => "l2",
            "3" => "l3",
            "1" => "l2l3",
            _ => page.Query("rlDot3adAggBalanceLayer") ?? ""
        };
        return new LagInfo
        {
            Name = lag.Name,
            IfIndex = lag.IfIndex,
            Description = page.Query("ifAlias") ?? "",
            Balance = balance,
            Members = members
        };
    }

    private static string ConfigUrl(PortName lag)
    {
        return EwsUrl.Config(
            "port_trunk.htm",
            new EwsUrl.Selector
            {
                Table = "TrunkPortsTable",
                Filter = "dot3adAggPortActorAdminKey=" + MibValues.Number(lag.IfIndex)
            },
            new EwsUrl.Selector
            {
                Table = "TrunkConfigTable",
                Query = new[] { new KeyValuePair<string, string>("dot3adAggIndex", MibValues.Number(lag.IfIndex)) }
            });
    }
}
