using System.Globalization;

using Nefarius.PowerConnect.Ews.Changes;
using Nefarius.PowerConnect.Ews.Common;
using Nefarius.PowerConnect.Ews.Ews;
using Nefarius.PowerConnect.Ews.Http;

namespace Nefarius.PowerConnect.Ews.Dot1x;

/// <summary>802.1X port-based authentication.</summary>
public sealed class Dot1xRepository
{
    private readonly SwitchClient _client;

    /// <summary>Creates a repository bound to a session.</summary>
    public Dot1xRepository(SwitchClient client)
    {
        _client = client;
    }

    /// <summary>Reads the port authentication page for one port.</summary>
    public Task<FormPage> GetPortAsync(PortName port, CancellationToken cancellationToken = default)
    {
        return _client.GetFormAsync(
            "/config/security_pauth.htm?[securityPortAuthTable]Query:dot1xPaePortNumber=" + MibValues.Number(port.IfIndex),
            cancellationToken);
    }

    /// <summary>
    ///     Plans port-control changes. auto = 2, force-authorized = 3, force-unauthorized = 1.
    /// </summary>
    public async Task<ChangePlan> PlanPortAsync(
        PortName port,
        string? control,
        bool? reauthenticate,
        CancellationToken cancellationToken = default)
    {
        FormPage page = await GetPortAsync(port, cancellationToken).ConfigureAwait(false);
        Dictionary<string, string> mutations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (control != null)
        {
            mutations["dot1xAuthAuthControlledPortControl$query"] = control.Trim().ToLowerInvariant() switch
            {
                "auto" => "2",
                "force-authorized" or "authorized" => "3",
                "force-unauthorized" or "unauthorized" => "1",
                _ => throw new FormatException("Control must be auto, force-authorized, or force-unauthorized.")
            };
        }

        if (reauthenticate.HasValue)
        {
            mutations["dot1xAuthReAuthEnabled$query"] = MibValues.Enable(reauthenticate.Value);
        }

        ChangePlan plan = new ChangePlan();
        plan.Add(FormChange.Post(_client, "Set 802.1X on " + port.Name, page, mutations));
        return plan;
    }

    /// <summary>Reads multiple-hosts settings.</summary>
    public Task<FormPage> GetHostsAsync(PortName port, CancellationToken cancellationToken = default)
    {
        return _client.GetFormAsync(
            "/config/security_pauth_advanced.htm?[secPAuthAdvTable]Query:ifIndex=" + MibValues.Number(port.IfIndex),
            cancellationToken);
    }

    /// <summary>Plans host-mode changes by posting the matching select value when the field exists.</summary>
    public async Task<ChangePlan> PlanHostsAsync(PortName port, string hostMode, CancellationToken cancellationToken = default)
    {
        FormPage page = await GetHostsAsync(port, cancellationToken).ConfigureAwait(false);
        string value = hostMode.Trim().ToLowerInvariant() switch
        {
            "single" => "1",
            "multiple" => "2",
            "multi-session" => "3",
            _ => throw new FormatException("Host mode must be single, multiple, or multi-session.")
        };
        FormField? field = page.Fields.FirstOrDefault(candidate =>
            candidate.Name.Contains("HostMode", StringComparison.OrdinalIgnoreCase) ||
            candidate.Name.Contains("MultiHost", StringComparison.OrdinalIgnoreCase));
        string fieldName = field?.Name ?? "rlAPHostMode$query";
        ChangePlan plan = new ChangePlan();
        plan.Add(FormChange.Post(
            _client,
            "Set 802.1X host mode on " + port.Name,
            page,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { [fieldName] = value }));
        return plan;
    }

    /// <summary>Reads the authenticated users page.</summary>
    public Task<FormPage> UsersAsync(CancellationToken cancellationToken = default)
    {
        return _client.GetFormAsync("/config/security_pusers.htm", cancellationToken);
    }
}
