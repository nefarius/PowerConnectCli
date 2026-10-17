using System.Globalization;

using Nefarius.PowerConnect.Ews.Changes;
using Nefarius.PowerConnect.Ews.Common;
using Nefarius.PowerConnect.Ews.Ews;
using Nefarius.PowerConnect.Ews.Http;

namespace Nefarius.PowerConnect.Ews.Ports;

/// <summary>Administrative and operational state of one copper port.</summary>
public sealed class PortConfiguration
{
    /// <summary>Port name.</summary>
    public string Port { get; init; } = "";

    /// <summary>Up or Down.</summary>
    public string AdminStatus { get; init; } = "";

    /// <summary>Interface description.</summary>
    public string Description { get; init; } = "";

    /// <summary>Admin speed in megabits, or auto.</summary>
    public string Speed { get; init; } = "";

    /// <summary>Admin duplex word.</summary>
    public string Duplex { get; init; } = "";

    /// <summary>True when auto-negotiation is enabled.</summary>
    public bool AutoNegotiation { get; init; }

    /// <summary>Flow control enumeration text.</summary>
    public string FlowControl { get; init; } = "";

    /// <summary>MDI/MDIX enumeration text.</summary>
    public string Mdix { get; init; } = "";

    /// <summary>True when back pressure is enabled.</summary>
    public bool BackPressure { get; init; }

    /// <summary>Operational link status: up, down, or another ifOperStatus name.</summary>
    public string OperStatus { get; init; } = "";
}

/// <summary>Options for <see cref="PortRepository.PlanSetAsync"/>.</summary>
public sealed class PortUpdate
{
    /// <summary>Admin up or down.</summary>
    public bool? AdminUp { get; init; }

    /// <summary>Speed in megabits.</summary>
    public int? SpeedMegabits { get; init; }

    /// <summary>full or half.</summary>
    public string? Duplex { get; init; }

    /// <summary>Auto-negotiation.</summary>
    public bool? AutoNegotiation { get; init; }

    /// <summary>on, off, or autoneg.</summary>
    public string? FlowControl { get; init; }

    /// <summary>auto, on, or off.</summary>
    public string? Mdix { get; init; }

    /// <summary>Back pressure.</summary>
    public bool? BackPressure { get; init; }

    /// <summary>Port description.</summary>
    public string? Description { get; init; }
}

/// <summary>Port Configuration page.</summary>
public sealed class PortRepository
{
    /// <summary>
    ///     Fields the page script disables before submit. Unchanged query fields are in this list too:
    ///     posting them (for example speed 0 while auto-negotiation is on) makes the switch ignore the write.
    ///     Mutated names are still sent.
    /// </summary>
    internal static readonly string[] OmittedUnlessMutated =
    {
        "ifOperStatus$query",
        "ifSpeed$query",
        "swIfDuplexOperMode$query",
        "swIfOperSpeedDuplexAutoNegotiation$query",
        "swIfOperBackPressureMode$query",
        "swIfOperFlowControlMode$query",
        "swIfOperMdix$query",
        "swIfOperSuspendedStatus$query",
        "swIfReActivateCB",
        "swIfSpeedAdminMode$select",
        "swIfDuplexAdminMode$select",
        "ifIndex$select",
        "adminAdvertisementFlags",
        "ifAdminStatus$query",
        "swIfSpeedAdminMode$query",
        "swIfDuplexAdminMode$query",
        "swIfSpeedDuplexAutoNegotiation$query",
        "swIfBackPressureMode$query",
        "swIfFlowControlMode$query",
        "swIfAdminMdix$query",
        "swIfType$query",
        "swIfTransceiverType$query",
        "rlPhdPortsModuleNumber$query",
        "ifAlias$query"
    };

    private readonly SwitchClient _client;

    /// <summary>Creates a repository bound to a session.</summary>
    public PortRepository(SwitchClient client)
    {
        _client = client;
    }

    /// <summary>Reads one port.</summary>
    public async Task<PortConfiguration> GetAsync(PortName port, CancellationToken cancellationToken = default)
    {
        FormPage page = await _client.GetFormAsync(Url(port), cancellationToken).ConfigureAwait(false);
        return Map(port, page);
    }

    /// <summary>Reads g1 through g16.</summary>
    public async Task<IReadOnlyList<PortConfiguration>> ListAsync(CancellationToken cancellationToken = default)
    {
        List<PortConfiguration> ports = new List<PortConfiguration>();
        for (int index = 1; index <= PortName.PortCount; index++)
        {
            ports.Add(await GetAsync(PortName.Parse("g" + index.ToString(CultureInfo.InvariantCulture)), cancellationToken).ConfigureAwait(false));
        }

        return ports;
    }

    /// <summary>Plans a port edit. With an empty update this resubmits the current form (no-op write).</summary>
    public async Task<ChangePlan> PlanSetAsync(PortName port, PortUpdate update, CancellationToken cancellationToken = default)
    {
        FormPage page = await _client.GetFormAsync(Url(port), cancellationToken).ConfigureAwait(false);
        Dictionary<string, string> mutations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (update.AdminUp.HasValue)
        {
            mutations["ifAdminStatus$query"] = update.AdminUp.Value ? "1" : "2";
        }

        if (update.Description != null)
        {
            mutations["ifAlias$query"] = update.Description;
        }

        if (update.AutoNegotiation.HasValue)
        {
            mutations["swIfSpeedDuplexAutoNegotiation$query"] = MibValues.Enable(update.AutoNegotiation.Value);
        }

        if (update.SpeedMegabits.HasValue && update.AutoNegotiation == true)
        {
            throw new FormatException("Speed cannot be set while auto-negotiation is on.");
        }

        if (update.SpeedMegabits.HasValue)
        {
            mutations["swIfSpeedAdminMode$query"] = MibValues.Speed(update.SpeedMegabits.Value);
            mutations["swIfSpeedDuplexAutoNegotiation$query"] = "2";
        }

        if (update.Duplex != null)
        {
            mutations["swIfDuplexAdminMode$query"] = MibValues.Duplex(update.Duplex);
        }

        if (update.FlowControl != null)
        {
            mutations["swIfFlowControlMode$query"] = MibValues.FlowControl(update.FlowControl);
        }

        if (update.Mdix != null)
        {
            mutations["swIfAdminMdix$query"] = MibValues.Mdix(update.Mdix);
        }

        if (update.BackPressure.HasValue)
        {
            mutations["swIfBackPressureMode$query"] = MibValues.Enable(update.BackPressure.Value);
        }

        ChangePlan plan = new ChangePlan();
        plan.Add(FormChange.Post(
            _client,
            mutations.Count == 0 ? "Resubmit port " + port.Name : "Configure port " + port.Name,
            page,
            mutations,
            verifyPath: Url(port),
            verify: after =>
            {
                if (update.Description != null && (after.Query("ifAlias") ?? "") != update.Description)
                {
                    return "Description did not stick (read '" + (after.Query("ifAlias") ?? "") + "')";
                }

                if (update.AdminUp.HasValue && after.Query("ifAdminStatus") != (update.AdminUp.Value ? "1" : "2"))
                {
                    return "Admin status did not stick";
                }

                return null;
            },
            omit: OmittedUnlessMutated));
        return plan;
    }

    private static PortConfiguration Map(PortName port, FormPage page)
    {
        string duplex = page.Query("swIfDuplexAdminMode") switch
        {
            "2" => "half",
            "3" => "full",
            "1" => "full",
            _ => page.Query("swIfDuplexAdminMode") ?? ""
        };
        string flow = page.Query("swIfFlowControlMode") switch
        {
            "1" => "on",
            "2" => "off",
            "3" => "autoneg",
            _ => page.Query("swIfFlowControlMode") ?? ""
        };
        string mdix = page.Query("swIfAdminMdix") switch
        {
            "1" => "on",
            "2" => "off",
            "3" => "auto",
            _ => page.Query("swIfAdminMdix") ?? ""
        };
        return new PortConfiguration
        {
            Port = port.Name,
            AdminStatus = page.Query("ifAdminStatus") == "2" ? "down" : "up",
            Description = page.Query("ifAlias") ?? "",
            Speed = MibValues.SpeedText(page.Query("swIfSpeedAdminMode")),
            Duplex = duplex,
            AutoNegotiation = MibValues.IsEnabled(page.Query("swIfSpeedDuplexAutoNegotiation")),
            FlowControl = flow,
            Mdix = mdix,
            BackPressure = MibValues.IsEnabled(page.Query("swIfBackPressureMode")),
            OperStatus = MibValues.OperStatusText(page.Query("ifOperStatus"))
        };
    }

    private static string Url(PortName port)
    {
        return EwsUrl.Query("port_config.htm", "PortConfigTable", "ifIndex", MibValues.Number(port.IfIndex));
    }
}
