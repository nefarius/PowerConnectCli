using Nefarius.PowerConnect.Ews.Changes;
using Nefarius.PowerConnect.Ews.Common;
using Nefarius.PowerConnect.Ews.Ews;
using Nefarius.PowerConnect.Ews.Http;

namespace Nefarius.PowerConnect.Ews.Stats;

/// <summary>RMON counters and CPU utilization.</summary>
public sealed class StatsRepository
{
    private readonly SwitchClient _client;

    /// <summary>Creates a repository bound to a session.</summary>
    public StatsRepository(SwitchClient client)
    {
        _client = client;
    }

    /// <summary>Reads RMON etherStats for one port. Names containing the counter are returned.</summary>
    public async Task<IReadOnlyDictionary<string, string>> RmonAsync(PortName port, CancellationToken cancellationToken = default)
    {
        FormPage page = await _client.GetFormAsync(Url(port), cancellationToken).ConfigureAwait(false);
        Dictionary<string, string> values = new Dictionary<string, string> { ["port"] = port.Name };
        foreach (FormField field in page.Fields)
        {
            if (field.Role != FieldRole.Query)
            {
                continue;
            }

            if (field.BaseName.Contains("etherStats", StringComparison.OrdinalIgnoreCase) ||
                field.BaseName.Contains("ifIn", StringComparison.OrdinalIgnoreCase) ||
                field.BaseName.Contains("ifOut", StringComparison.OrdinalIgnoreCase))
            {
                values[field.BaseName] = field.Value;
            }
        }

        return values;
    }

    /// <summary>Plans a counter reset for the port.</summary>
    public async Task<ChangePlan> PlanResetAsync(PortName port, CancellationToken cancellationToken = default)
    {
        FormPage page = await _client.GetFormAsync(Url(port), cancellationToken).ConfigureAwait(false);
        ChangePlan plan = new ChangePlan();
        plan.Add(FormChange.Post(
            _client,
            "Reset RMON counters on " + port.Name,
            page,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["rlIfClearPortMibCounters"] = "1"
            }));
        return plan;
    }

    /// <summary>Reads the CPU utilization page fields.</summary>
    public async Task<IReadOnlyDictionary<string, string>> CpuAsync(CancellationToken cancellationToken = default)
    {
        FormPage page = await _client.GetFormAsync("/config/cpu_utilization.htm", cancellationToken).ConfigureAwait(false);
        Dictionary<string, string> values = new Dictionary<string, string>();
        foreach (FormField field in page.Fields)
        {
            if (field.Role is FieldRole.Metadata or FieldRole.Marker)
            {
                continue;
            }

            values[field.Name] = field.Value;
        }

        return values;
    }

    private static string Url(PortName port)
    {
        return "/config/rmon_stats.htm?Query:etherStatsIndex=" + MibValues.Number(port.IfIndex);
    }
}
