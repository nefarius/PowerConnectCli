using System.Globalization;

using Nefarius.PowerConnect.Ews.Changes;
using Nefarius.PowerConnect.Ews.Common;
using Nefarius.PowerConnect.Ews.Ews;
using Nefarius.PowerConnect.Ews.Http;

namespace Nefarius.PowerConnect.Ews.Qos;

/// <summary>CoS / QoS pages. This 2816 firmware exposes mode as disable (1) or enable (2).</summary>
public sealed class QosRepository
{
    private readonly SwitchClient _client;

    /// <summary>Creates a repository bound to a session.</summary>
    public QosRepository(SwitchClient client)
    {
        _client = client;
    }

    /// <summary>Reads global QoS mode and trust.</summary>
    public async Task<IReadOnlyDictionary<string, string>> GetGlobalAsync(CancellationToken cancellationToken = default)
    {
        FormPage page = await _client.GetFormAsync("/config/cosqos_global.htm", cancellationToken).ConfigureAwait(false);
        string mode = page.ValueOf("rlQosCliQosMode$scalar") switch
        {
            "1" => "disabled",
            "2" => "basic",
            _ => page.ValueOf("rlQosCliQosMode$scalar") ?? ""
        };
        string trust = page.ValueOf("rlQosCliBasicModeCfg$scalar") switch
        {
            "2" => "cos",
            "3" => "dscp",
            _ => page.ValueOf("rlQosCliBasicModeCfg$scalar") ?? ""
        };
        return new Dictionary<string, string>
        {
            ["mode"] = mode,
            ["trust"] = trust
        };
    }

    /// <summary>
    ///     Plans global QoS. <paramref name="mode"/> is disabled, basic, or advanced.
    ///     This model's page only has disable and enable, so advanced is posted as enable (2).
    /// </summary>
    public async Task<ChangePlan> PlanGlobalAsync(string? mode, string? trust, CancellationToken cancellationToken = default)
    {
        FormPage page = await _client.GetFormAsync("/config/cosqos_global.htm", cancellationToken).ConfigureAwait(false);
        Dictionary<string, string> mutations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (mode != null)
        {
            mutations["rlQosCliQosMode$scalar"] = mode.Trim().ToLowerInvariant() switch
            {
                "disabled" or "disable" => "1",
                "basic" or "advanced" or "enable" => "2",
                _ => throw new FormatException("QoS mode must be basic, advanced, or disabled.")
            };
        }

        if (trust != null)
        {
            mutations["rlQosCliBasicModeCfg$scalar"] = trust.Trim().ToLowerInvariant() switch
            {
                "cos" => "2",
                "dscp" => "3",
                _ => throw new FormatException("Trust must be cos or dscp.")
            };
        }

        ChangePlan plan = new ChangePlan();
        plan.Add(FormChange.Post(_client, "Set QoS global mode", page, mutations, verifyPath: "/config/cosqos_global.htm"));
        return plan;
    }

    /// <summary>Reads the default user priority of one interface.</summary>
    public async Task<string> GetInterfaceAsync(PortName port, CancellationToken cancellationToken = default)
    {
        FormPage page = await _client.GetFormAsync(InterfaceUrl(port), cancellationToken).ConfigureAwait(false);
        return page.Query("dot1dPortDefaultUserPriority") ?? "";
    }

    /// <summary>Plans the default CoS priority of one interface.</summary>
    public async Task<ChangePlan> PlanInterfaceAsync(PortName port, int priority, CancellationToken cancellationToken = default)
    {
        FormPage page = await _client.GetFormAsync(InterfaceUrl(port), cancellationToken).ConfigureAwait(false);
        ChangePlan plan = new ChangePlan();
        plan.Add(FormChange.Post(
            _client,
            "Set default priority on " + port.Name,
            page,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["dot1dPortDefaultUserPriority$query"] = MibValues.Number(priority)
            },
            verifyPath: InterfaceUrl(port)));
        return plan;
    }

    /// <summary>Reads queue weights and strict-priority flags.</summary>
    public async Task<IReadOnlyDictionary<string, string>> GetQueuesAsync(CancellationToken cancellationToken = default)
    {
        FormPage page = await _client.GetFormAsync("/config/cosqos_queue.htm", cancellationToken).ConfigureAwait(false);
        Dictionary<string, string> values = new Dictionary<string, string>();
        for (int queue = 1; queue <= 8; queue++)
        {
            values["weight" + queue.ToString(CultureInfo.InvariantCulture)] =
                page.Query("rlQosValueQueue" + queue.ToString(CultureInfo.InvariantCulture)) ?? "";
        }

        foreach (FormRow row in page.Rows())
        {
            string? id = row.Value("rlQosEfQueueId");
            string? state = row.Value("rlQosEfState");
            if (id != null)
            {
                values["strict" + id] = state ?? "";
            }
        }

        return values;
    }

    /// <summary>Plans WRR weights. Pass eight weights, or null entries to leave a queue unchanged.</summary>
    public async Task<ChangePlan> PlanQueuesAsync(IReadOnlyList<int?> weights, bool? strict, CancellationToken cancellationToken = default)
    {
        FormPage page = await _client.GetFormAsync("/config/cosqos_queue.htm", cancellationToken).ConfigureAwait(false);
        Dictionary<string, string> mutations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (int index = 0; index < weights.Count && index < 8; index++)
        {
            if (weights[index].HasValue)
            {
                mutations["rlQosValueQueue" + (index + 1).ToString(CultureInfo.InvariantCulture) + "$query"] =
                    MibValues.Number(weights[index]!.Value);
            }
        }

        if (strict.HasValue)
        {
            foreach (FormRow row in page.Rows())
            {
                mutations["rlQosEfState$repeat?" + row.Index.ToString(CultureInfo.InvariantCulture)] = strict.Value ? "1" : "2";
            }
        }

        ChangePlan plan = new ChangePlan();
        plan.Add(FormChange.Post(_client, "Set QoS queues", page, mutations, verifyPath: "/config/cosqos_queue.htm"));
        return plan;
    }

    /// <summary>Reads the CoS-to-queue map.</summary>
    public async Task<IReadOnlyDictionary<int, int>> GetCosMapAsync(CancellationToken cancellationToken = default)
    {
        FormPage page = await _client.GetFormAsync("/config/cosqos_to.htm", cancellationToken).ConfigureAwait(false);
        Dictionary<int, int> map = new Dictionary<int, int>();
        foreach (FormRow row in page.Rows())
        {
            if (int.TryParse(row.Value("rlQosCosIndex"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int cos) &&
                int.TryParse(row.Value("rlQosCosQueueId"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int queue))
            {
                map[cos] = queue;
            }
        }

        return map;
    }

    /// <summary>Plans one CoS-to-queue entry.</summary>
    public async Task<ChangePlan> PlanCosMapAsync(int cos, int queue, CancellationToken cancellationToken = default)
    {
        FormPage page = await _client.GetFormAsync("/config/cosqos_to.htm", cancellationToken).ConfigureAwait(false);
        FormRow? row = page.Rows().FirstOrDefault(candidate => candidate.Value("rlQosCosIndex") == MibValues.Number(cos));
        if (row == null)
        {
            throw new SwitchException("CoS " + cos.ToString(CultureInfo.InvariantCulture) + " is not on the map page.");
        }

        ChangePlan plan = new ChangePlan();
        plan.Add(FormChange.Post(
            _client,
            "Map CoS " + cos.ToString(CultureInfo.InvariantCulture) + " to queue " + queue.ToString(CultureInfo.InvariantCulture),
            page,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["rlQosCosQueueId$repeat?" + row.Index.ToString(CultureInfo.InvariantCulture)] = MibValues.Number(queue)
            },
            verifyPath: "/config/cosqos_to.htm"));
        return plan;
    }

    /// <summary>Reads the DSCP-to-queue map.</summary>
    public async Task<IReadOnlyDictionary<int, int>> GetDscpMapAsync(CancellationToken cancellationToken = default)
    {
        FormPage page = await _client.GetFormAsync("/config/cosqos_dscp.htm", cancellationToken).ConfigureAwait(false);
        Dictionary<int, int> map = new Dictionary<int, int>();
        foreach (FormRow row in page.Rows())
        {
            if (int.TryParse(row.Value("rlQosOldDscp"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int dscp) &&
                int.TryParse(row.Value("rlQosQueueNum"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int queue))
            {
                map[dscp] = queue;
            }
        }

        return map;
    }

    /// <summary>Plans one DSCP-to-queue entry.</summary>
    public async Task<ChangePlan> PlanDscpMapAsync(int dscp, int queue, CancellationToken cancellationToken = default)
    {
        FormPage page = await _client.GetFormAsync("/config/cosqos_dscp.htm", cancellationToken).ConfigureAwait(false);
        FormRow? row = page.Rows().FirstOrDefault(candidate => candidate.Value("rlQosOldDscp") == MibValues.Number(dscp));
        if (row == null)
        {
            throw new SwitchException("DSCP " + dscp.ToString(CultureInfo.InvariantCulture) + " is not on the map page.");
        }

        ChangePlan plan = new ChangePlan();
        plan.Add(FormChange.Post(
            _client,
            "Map DSCP " + dscp.ToString(CultureInfo.InvariantCulture) + " to queue " + queue.ToString(CultureInfo.InvariantCulture),
            page,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["rlQosQueueNum$repeat?" + row.Index.ToString(CultureInfo.InvariantCulture)] = MibValues.Number(queue)
            },
            verifyPath: "/config/cosqos_dscp.htm"));
        return plan;
    }

    private static string InterfaceUrl(PortName port)
    {
        string kind = port.IsLag ? "1" : "2";
        return EwsUrl.Config(
            "cosqos_interface.htm",
            new EwsUrl.Selector
            {
                Table = "IfPolicyVT",
                Query = new[]
                {
                    new KeyValuePair<string, string>("rlIfType", kind),
                    new KeyValuePair<string, string>("rlIfIndex", MibValues.Number(port.IfIndex))
                }
            });
    }
}
