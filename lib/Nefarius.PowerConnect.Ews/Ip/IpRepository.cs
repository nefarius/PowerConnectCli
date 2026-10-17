using Nefarius.PowerConnect.Ews.Changes;
using Nefarius.PowerConnect.Ews.Ews;
using Nefarius.PowerConnect.Ews.Http;

namespace Nefarius.PowerConnect.Ews.Ip;

/// <summary>Management IP address. Changing it is destructive because the session moves.</summary>
public sealed class IpRepository
{
    private readonly SwitchClient _client;

    /// <summary>Creates a repository bound to a session.</summary>
    public IpRepository(SwitchClient client)
    {
        _client = client;
    }

    /// <summary>Reads address, mask, and gateway.</summary>
    public async Task<IReadOnlyDictionary<string, string>> GetAsync(CancellationToken cancellationToken = default)
    {
        FormPage page = await _client.GetFormAsync("/config/ipa.htm", cancellationToken).ConfigureAwait(false);
        return new Dictionary<string, string>
        {
            ["address"] = page.ValueOf("rlGlobalIpAdEntAddr$add") ?? "",
            ["mask"] = page.ValueOf("rlGlobalIpAdEntNetMask$add") ?? "",
            ["gateway"] = page.ValueOf("rlGlobalIpAdDefaultGateway$add") ?? "",
            ["owner"] = page.ValueOf("rlGlobalIpAdOwner$add") == "2" ? "dhcp" : "static"
        };
    }

    /// <summary>Plans a static IP change. The step is marked destructive.</summary>
    public async Task<ChangePlan> PlanSetAsync(
        string? address,
        string? mask,
        string? gateway,
        CancellationToken cancellationToken = default)
    {
        FormPage page = await _client.GetFormAsync("/config/ipa.htm", cancellationToken).ConfigureAwait(false);
        Dictionary<string, string> mutations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["rlGlobalIpAdOwner$add"] = "1"
        };
        if (address != null)
        {
            mutations["rlGlobalIpAdEntAddr$add"] = address;
        }

        if (mask != null)
        {
            mutations["rlGlobalIpAdEntNetMask$add"] = mask;
        }

        if (gateway != null)
        {
            mutations["rlGlobalIpAdDefaultGateway$add"] = gateway;
        }

        ChangePlan plan = new ChangePlan();
        plan.Add(FormChange.Post(
            _client,
            "Change management IP address",
            page,
            mutations,
            destructive: true));
        return plan;
    }
}
