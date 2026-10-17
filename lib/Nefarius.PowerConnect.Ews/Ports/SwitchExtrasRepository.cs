using System.Globalization;

using Nefarius.PowerConnect.Ews.Changes;
using Nefarius.PowerConnect.Ews.Common;
using Nefarius.PowerConnect.Ews.Ews;
using Nefarius.PowerConnect.Ews.Http;

namespace Nefarius.PowerConnect.Ews.Ports;

/// <summary>Green Ethernet global flags and per-port admin state.</summary>
public sealed class GreenEthernetRepository
{
    private readonly SwitchClient _client;

    /// <summary>Creates a repository bound to a session.</summary>
    public GreenEthernetRepository(SwitchClient client)
    {
        _client = client;
    }

    /// <summary>Reads global energy-detect and short-reach, plus optional per-port rows.</summary>
    public async Task<IReadOnlyDictionary<string, string>> GetAsync(CancellationToken cancellationToken = default)
    {
        FormPage page = await _client.GetFormAsync("/config/green_ethernet.htm", cancellationToken).ConfigureAwait(false);
        Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["energy-detect"] = MibValues.IsEnabled(page.ValueOf("rlGreenEthEnergyDetectEnable")) ? "on" : "off",
            ["short-reach"] = MibValues.IsEnabled(page.ValueOf("rlGreenEthShortReachEnable")) ? "on" : "off"
        };
        foreach (FormRow row in page.Rows())
        {
            string? ifIndex = row.Value("ifIndex");
            string? kind = row.Value("rlGreenEthPortSavingTypeValue");
            string? admin = row.Value("rlGreenEthPortAdminState");
            if (ifIndex == null || kind == null)
            {
                continue;
            }

            string feature = kind == "1" ? "energy-detect" : "short-reach";
            values["g" + ifIndex + "." + feature] = admin == "1" ? "on" : "off";
        }

        return values;
    }

    /// <summary>Plans global and optional per-port green ethernet changes. Saving type 1 is energy detect, 2 is short reach.</summary>
    public async Task<ChangePlan> PlanSetAsync(
        bool? energyDetect,
        bool? shortReach,
        PortName? port,
        CancellationToken cancellationToken = default)
    {
        FormPage page = await _client.GetFormAsync("/config/green_ethernet.htm", cancellationToken).ConfigureAwait(false);
        Dictionary<string, string> mutations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (energyDetect.HasValue && port == null)
        {
            mutations["rlGreenEthEnergyDetectEnable"] = MibValues.Enable(energyDetect.Value);
        }

        if (shortReach.HasValue && port == null)
        {
            mutations["rlGreenEthShortReachEnable"] = MibValues.Enable(shortReach.Value);
        }

        if (port != null)
        {
            foreach (FormRow row in page.Rows())
            {
                if (row.Value("ifIndex") != MibValues.Number(port.Value.IfIndex))
                {
                    continue;
                }

                string? kind = row.Value("rlGreenEthPortSavingTypeValue");
                bool? flag = kind == "1" ? energyDetect : shortReach;
                if (!flag.HasValue)
                {
                    continue;
                }

                mutations["rlGreenEthPortAdminState$repeat11?" + row.Index.ToString(CultureInfo.InvariantCulture)] =
                    MibValues.Enable(flag.Value);
            }
        }

        ChangePlan plan = new ChangePlan();
        plan.Add(FormChange.Post(
            _client,
            "Set Green Ethernet",
            page,
            mutations,
            verifyPath: "/config/green_ethernet.htm",
            verify: after =>
            {
                foreach (KeyValuePair<string, string> pair in mutations)
                {
                    if ((after.ValueOf(pair.Key) ?? "") != pair.Value)
                    {
                        return pair.Key + " did not stick";
                    }
                }

                return null;
            }));
        return plan;
    }
}

/// <summary>Storm control for one port.</summary>
public sealed class StormControlRepository
{
    private readonly SwitchClient _client;

    /// <summary>Creates a repository bound to a session.</summary>
    public StormControlRepository(SwitchClient client)
    {
        _client = client;
    }

    /// <summary>Reads broadcast, multicast, and unknown-unicast storm control.</summary>
    public async Task<IReadOnlyDictionary<string, string>> GetAsync(PortName port, CancellationToken cancellationToken = default)
    {
        FormPage page = await _client.GetFormAsync(Url(port), cancellationToken).ConfigureAwait(false);
        return new Dictionary<string, string>
        {
            ["port"] = port.Name,
            ["broadcast"] = MibValues.IsEnabled(page.Query("rlStormCtrlBroadcastEnable")) ? "on" : "off",
            ["rate"] = page.Query("rlStormCtrlBroadcastRate") ?? "",
            ["multicast"] = MibValues.IsEnabled(page.Query("rlStormCtrlMulticastEnable")) ? "on" : "off",
            ["unknown-unicast"] = MibValues.IsEnabled(page.Query("rlStormCtrlUnknownUnicastEnable")) ? "on" : "off"
        };
    }

    /// <summary>Plans storm control changes. Rate is the raw page counter (the UI displays it divided by 1000).</summary>
    public async Task<ChangePlan> PlanSetAsync(
        PortName port,
        bool? broadcast,
        int? rate,
        bool? multicast,
        bool? unknownUnicast,
        CancellationToken cancellationToken = default)
    {
        FormPage page = await _client.GetFormAsync(Url(port), cancellationToken).ConfigureAwait(false);
        Dictionary<string, string> mutations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (broadcast.HasValue)
        {
            mutations["rlStormCtrlBroadcastEnable$query"] = MibValues.Enable(broadcast.Value);
        }

        if (rate.HasValue)
        {
            mutations["rlStormCtrlBroadcastRate$query"] = MibValues.Number(rate.Value);
        }

        if (multicast.HasValue)
        {
            mutations["rlStormCtrlMulticastEnable$query"] = MibValues.Enable(multicast.Value);
        }

        if (unknownUnicast.HasValue)
        {
            mutations["rlStormCtrlUnknownUnicastEnable$query"] = MibValues.Enable(unknownUnicast.Value);
        }

        ChangePlan plan = new ChangePlan();
        plan.Add(FormChange.Post(_client, "Set storm control on " + port.Name, page, mutations, verifyPath: Url(port)));
        return plan;
    }

    private static string Url(PortName port)
    {
        return "/config/port_bprotect.htm?Query:dot1dBasePort=" + MibValues.Number(port.IfIndex);
    }
}

/// <summary>Port mirroring.</summary>
public sealed class MirrorRepository
{
    private readonly SwitchClient _client;

    /// <summary>Creates a repository bound to a session.</summary>
    public MirrorRepository(SwitchClient client)
    {
        _client = client;
    }

    /// <summary>Reads the current mirror source and destination. Empty strings mean no session.</summary>
    public async Task<(string Source, string Destination)> GetAsync(CancellationToken cancellationToken = default)
    {
        FormPage page = await _client.GetFormAsync("/config/port_mirror.htm", cancellationToken).ConfigureAwait(false);
        return (Name(page.Query("portCopySource")), Name(page.Query("portCopyDest")));
    }

    /// <summary>Plans a mirror session (row status 4) or clears it (row status 6).</summary>
    public async Task<ChangePlan> PlanSetAsync(PortName? source, PortName? destination, bool clear, CancellationToken cancellationToken = default)
    {
        FormPage page = await _client.GetFormAsync("/config/port_mirror.htm", cancellationToken).ConfigureAwait(false);
        Dictionary<string, string> mutations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (clear)
        {
            mutations["portCopyStatus$query"] = "6";
        }
        else
        {
            if (source == null || destination == null)
            {
                throw new SwitchException("Mirror set requires --source and --dest.");
            }

            mutations["portCopySource$query"] = MibValues.Number(source.Value.IfIndex);
            mutations["portCopyDest$query"] = MibValues.Number(destination.Value.IfIndex);
            mutations["portCopyStatus$query"] = "4";
            mutations["portCopyDirection$query"] = "3";
        }

        ChangePlan plan = new ChangePlan();
        plan.Add(FormChange.Post(_client, clear ? "Clear port mirror" : "Set port mirror", page, mutations));
        return plan;
    }

    private static string Name(string? ifIndex)
    {
        if (int.TryParse(ifIndex, NumberStyles.Integer, CultureInfo.InvariantCulture, out int index) &&
            PortName.TryFromIfIndex(index, out PortName port))
        {
            return port.Name;
        }

        return "";
    }
}

/// <summary>Dynamic MAC address table.</summary>
public sealed class MacTableRepository
{
    private readonly SwitchClient _client;

    /// <summary>Creates a repository bound to a session.</summary>
    public MacTableRepository(SwitchClient client)
    {
        _client = client;
    }

    /// <summary>Lists learned addresses. Optional VLAN and port filters are applied client-side.</summary>
    public async Task<IReadOnlyList<IReadOnlyDictionary<string, string>>> ListAsync(
        int? vlan,
        PortName? port,
        CancellationToken cancellationToken = default)
    {
        FormPage page = await _client.GetFormAsync("/config/address_dynamic.htm", cancellationToken).ConfigureAwait(false);
        List<Dictionary<string, string>> rows = new List<Dictionary<string, string>>();
        foreach (FormRow row in page.Rows())
        {
            string address = row.Value("dot1qTpFdbAddress") ?? "";
            string vlanId = row.Value("dot1qFdbId") ?? "";
            string portIndex = row.Value("dot1qTpFdbPort") ?? "";
            if (address.Length == 0)
            {
                continue;
            }

            if (vlan.HasValue && vlanId != MibValues.Number(vlan.Value))
            {
                continue;
            }

            string portName = portIndex;
            if (int.TryParse(portIndex, NumberStyles.Integer, CultureInfo.InvariantCulture, out int ifIndex) &&
                PortName.TryFromIfIndex(ifIndex, out PortName named))
            {
                portName = named.Name;
            }

            if (port != null && portName != port.Value.Name)
            {
                continue;
            }

            rows.Add(new Dictionary<string, string>
            {
                ["address"] = address,
                ["vlan"] = vlanId,
                ["port"] = portName
            });
        }

        return rows;
    }

    /// <summary>Plans a clear of the dynamic table (rndAction 23).</summary>
    public async Task<ChangePlan> PlanClearAsync(CancellationToken cancellationToken = default)
    {
        FormPage page = await _client.GetFormAsync("/config/address_dynamic.htm", cancellationToken).ConfigureAwait(false);
        ChangePlan plan = new ChangePlan();
        plan.Add(FormChange.Post(
            _client,
            "Clear dynamic MAC address table",
            page,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["rndAction$hidden"] = "23"
            }));
        return plan;
    }
}
