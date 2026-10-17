using System.Globalization;

using Nefarius.PowerConnect.Ews.Changes;
using Nefarius.PowerConnect.Ews.Common;
using Nefarius.PowerConnect.Ews.Ews;
using Nefarius.PowerConnect.Ews.Http;

namespace Nefarius.PowerConnect.Ews.Stp;

/// <summary>Bridge spanning-tree settings.</summary>
public sealed class StpBridge
{
    /// <summary>True when spanning tree is enabled.</summary>
    public bool Enabled { get; init; }

    /// <summary><c>stp</c> or <c>rstp</c>.</summary>
    public string Mode { get; init; } = "";

    /// <summary>Bridge priority.</summary>
    public int Priority { get; init; }

    /// <summary>Hello time in seconds.</summary>
    public int HelloSeconds { get; init; }

    /// <summary>Max age in seconds.</summary>
    public int MaxAgeSeconds { get; init; }

    /// <summary>Forward delay in seconds.</summary>
    public int ForwardDelaySeconds { get; init; }

    /// <summary><c>short</c> or <c>long</c>.</summary>
    public string PathCostMethod { get; init; } = "";
}

/// <summary>Per-port spanning-tree settings.</summary>
public sealed class StpPort
{
    /// <summary>Port or LAG name.</summary>
    public string Port { get; init; } = "";

    /// <summary>True when STP is enabled on the port.</summary>
    public bool Enabled { get; init; }

    /// <summary>Port priority.</summary>
    public int Priority { get; init; }

    /// <summary>Admin path cost.</summary>
    public int PathCost { get; init; }

    /// <summary>Edge (fast link).</summary>
    public bool Edge { get; init; }

    /// <summary>Restricted role (root guard on this UI).</summary>
    public string RestrictedRole { get; init; } = "";
}

/// <summary>Spanning Tree global, port, and LAG pages.</summary>
public sealed class StpRepository
{
    private readonly SwitchClient _client;

    /// <summary>Creates a repository bound to a session.</summary>
    public StpRepository(SwitchClient client)
    {
        _client = client;
    }

    /// <summary>Reads bridge settings.</summary>
    public async Task<StpBridge> GetBridgeAsync(CancellationToken cancellationToken = default)
    {
        FormPage page = await _client.GetFormAsync("/config/sta_bridge.htm", cancellationToken).ConfigureAwait(false);
        return new StpBridge
        {
            Enabled = MibValues.IsEnabled(page.ValueOf("rldot1dStpEnable")),
            Mode = page.ValueOf("dot1dStpVersion") == "0" ? "stp" : "rstp",
            Priority = Parse(page.ValueOf("dot1dStpPriority"), 32768),
            HelloSeconds = Parse(page.ValueOf("dot1dStpBridgeHelloTime"), 200) / 100,
            MaxAgeSeconds = Parse(page.ValueOf("dot1dStpBridgeMaxAge"), 2000) / 100,
            ForwardDelaySeconds = Parse(page.ValueOf("dot1dStpBridgeForwardDelay"), 1500) / 100,
            PathCostMethod = page.ValueOf("dot1dStpPathCostDefault") == "2" ? "long" : "short"
        };
    }

    /// <summary>Plans bridge setting changes. Times are seconds and are posted in centiseconds.</summary>
    public async Task<ChangePlan> PlanBridgeAsync(
        bool? enabled,
        string? mode,
        int? priority,
        int? helloSeconds,
        int? maxAgeSeconds,
        int? forwardDelaySeconds,
        string? pathCostMethod,
        CancellationToken cancellationToken = default)
    {
        FormPage page = await _client.GetFormAsync("/config/sta_bridge.htm", cancellationToken).ConfigureAwait(false);
        Dictionary<string, string> mutations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (enabled.HasValue)
        {
            mutations["rldot1dStpEnable"] = MibValues.Enable(enabled.Value);
        }

        if (mode != null)
        {
            mutations["dot1dStpVersion"] = mode.Trim().ToLowerInvariant() switch
            {
                "stp" or "classic" => "0",
                "rstp" or "rapid" => "2",
                _ => throw new FormatException("STP mode must be stp or rstp.")
            };
        }

        if (priority.HasValue)
        {
            mutations["dot1dStpPriority"] = MibValues.Number(priority.Value);
        }

        if (helloSeconds.HasValue)
        {
            mutations["dot1dStpBridgeHelloTime"] = MibValues.Number(helloSeconds.Value * 100);
        }

        if (maxAgeSeconds.HasValue)
        {
            mutations["dot1dStpBridgeMaxAge"] = MibValues.Number(maxAgeSeconds.Value * 100);
        }

        if (forwardDelaySeconds.HasValue)
        {
            mutations["dot1dStpBridgeForwardDelay"] = MibValues.Number(forwardDelaySeconds.Value * 100);
        }

        if (pathCostMethod != null)
        {
            mutations["dot1dStpPathCostDefault"] = pathCostMethod.Trim().ToLowerInvariant() switch
            {
                "short" => "1",
                "long" => "2",
                _ => throw new FormatException("Path cost method must be short or long.")
            };
        }

        ChangePlan plan = new ChangePlan();
        plan.Add(FormChange.Post(_client, "Set spanning tree bridge", page, mutations, verifyPath: "/config/sta_bridge.htm"));
        return plan;
    }

    /// <summary>Reads STP settings for one port or LAG.</summary>
    public async Task<StpPort> GetPortAsync(PortName port, CancellationToken cancellationToken = default)
    {
        FormPage page = await _client.GetFormAsync(PortUrl(port), cancellationToken).ConfigureAwait(false);
        return new StpPort
        {
            Port = port.Name,
            Enabled = MibValues.IsEnabled(page.Query("dot1dStpPortEnable")),
            Priority = Parse(page.Query("dot1dStpPortPriority"), 128),
            PathCost = Parse(page.Query("dot1dStpPortAdminPathCost"), 0),
            Edge = MibValues.IsEnabled(page.Query("dot1dStpPortAdminEdgePort")),
            RestrictedRole = page.Query("rldot1dStpPortRestrictedRole") ?? ""
        };
    }

    /// <summary>Lists STP settings for copper ports.</summary>
    public async Task<IReadOnlyList<StpPort>> ListPortsAsync(CancellationToken cancellationToken = default)
    {
        List<StpPort> ports = new List<StpPort>();
        for (int index = 1; index <= PortName.PortCount; index++)
        {
            ports.Add(await GetPortAsync(PortName.Parse("g" + index.ToString(CultureInfo.InvariantCulture)), cancellationToken).ConfigureAwait(false));
        }

        return ports;
    }

    /// <summary>Plans per-port STP changes. <paramref name="migrate"/> posts the migration checkbox path on the tree page.</summary>
    public async Task<ChangePlan> PlanPortAsync(
        PortName port,
        bool? enabled,
        int? priority,
        int? pathCost,
        bool? edge,
        bool? restrictedRole,
        bool migrate,
        CancellationToken cancellationToken = default)
    {
        FormPage page = await _client.GetFormAsync(PortUrl(port), cancellationToken).ConfigureAwait(false);
        Dictionary<string, string> mutations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (enabled.HasValue)
        {
            mutations["dot1dStpPortEnable$query"] = MibValues.Enable(enabled.Value);
        }

        if (priority.HasValue)
        {
            mutations["dot1dStpPortPriority$query"] = MibValues.Number(priority.Value);
        }

        if (pathCost.HasValue)
        {
            mutations["dot1dStpPortAdminPathCost$query"] = MibValues.Number(pathCost.Value);
        }

        if (edge.HasValue)
        {
            mutations["dot1dStpPortAdminEdgePort$query"] = MibValues.Enable(edge.Value);
        }

        if (restrictedRole.HasValue)
        {
            mutations["rldot1dStpPortRestrictedRole$query"] = MibValues.Enable(restrictedRole.Value);
        }

        ChangePlan plan = new ChangePlan();
        if (mutations.Count > 0)
        {
            plan.Add(FormChange.Post(_client, "Set STP on " + port.Name, page, mutations, verifyPath: PortUrl(port)));
        }

        if (migrate)
        {
            FormPage tree = await _client.GetFormAsync("/config/sta_tree.htm", cancellationToken).ConfigureAwait(false);
            plan.Add(FormChange.Post(
                _client,
                "Migrate " + port.Name + " to the point-to-point protocol",
                tree,
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["CBoxMigration"] = "on"
                }));
        }

        return plan;
    }

    private static int Parse(string? text, int fallback)
    {
        return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) ? value : fallback;
    }

    private static string PortUrl(PortName port)
    {
        return port.IsLag
            ? "/config/sta_trunk.htm?Query:dot3adAggIndex=" + MibValues.Number(port.IfIndex)
            : "/config/sta_port.htm?Query:rlPhdPortsIfIndex=" + MibValues.Number(port.IfIndex);
    }
}
