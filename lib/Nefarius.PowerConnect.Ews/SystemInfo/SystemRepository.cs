using Nefarius.PowerConnect.Ews.Changes;
using Nefarius.PowerConnect.Ews.Common;
using Nefarius.PowerConnect.Ews.Ews;
using Nefarius.PowerConnect.Ews.Http;

namespace Nefarius.PowerConnect.Ews.SystemInfo;

/// <summary>Asset, versions, reset, secure mode, and jumbo frames.</summary>
public sealed class SystemRepository
{
    private readonly SwitchClient _client;

    /// <summary>Creates a repository bound to a session.</summary>
    public SystemRepository(SwitchClient client)
    {
        _client = client;
    }

    /// <summary>Reads asset fields. Serial and service tag are returned because they are device identity, not secrets.</summary>
    public async Task<IReadOnlyDictionary<string, string>> GetAssetAsync(CancellationToken cancellationToken = default)
    {
        FormPage page = await _client.GetFormAsync("/config/general_asset.htm", cancellationToken).ConfigureAwait(false);
        return new Dictionary<string, string>
        {
            ["name"] = page.ValueOf("sysName") ?? "",
            ["contact"] = page.ValueOf("sysContact") ?? "",
            ["location"] = page.ValueOf("sysLocation") ?? "",
            ["mac"] = page.ValueOf("rndBasePhysicalAddress") ?? "",
            ["object-id"] = page.ValueOf("sysObjectID") ?? "",
            ["uptime"] = page.ValueOf("sysUpTime") ?? "",
            ["service-tag"] = page.Rows().Select(row => row.Value("productIdentificationServiceTag")).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? "",
            ["serial"] = page.Rows().Select(row => row.Value("productIdentificationSerialNumber")).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? ""
        };
    }

    /// <summary>Reads firmware, boot, and hardware versions.</summary>
    public async Task<IReadOnlyDictionary<string, string>> GetVersionsAsync(CancellationToken cancellationToken = default)
    {
        FormPage page = await _client.GetFormAsync("/config/general_versions.htm", cancellationToken).ConfigureAwait(false);
        return new Dictionary<string, string>
        {
            ["firmware"] = page.ValueOf("rndBrgVersion") ?? "",
            ["boot"] = page.ValueOf("rndBaseBootVersion") ?? "",
            ["hardware"] = page.ValueOf("genGroupHWVersion") ?? ""
        };
    }

    /// <summary>Plans an asset-tag write.</summary>
    public async Task<ChangePlan> PlanAssetTagAsync(string tag, CancellationToken cancellationToken = default)
    {
        FormPage page = await _client.GetFormAsync("/config/general_asset.htm", cancellationToken).ConfigureAwait(false);
        ChangePlan plan = new ChangePlan();
        plan.Add(FormChange.Post(
            _client,
            "Set asset tag",
            page,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["rlPhdUnitGenParamAssetTag$repeat?1"] = tag
            }));
        return plan;
    }

    /// <summary>Plans a switch reset (rndAction 1). Destructive.</summary>
    public async Task<ChangePlan> PlanResetAsync(CancellationToken cancellationToken = default)
    {
        FormPage page = await _client.GetFormAsync("/config/switch_reset.htm", cancellationToken).ConfigureAwait(false);
        ChangePlan plan = new ChangePlan();
        plan.Add(FormChange.Post(
            _client,
            "Reset the switch",
            page,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["rndAction"] = "1" },
            destructive: true));
        return plan;
    }

    /// <summary>Reads secure mode. 2 is unmanaged; anything else is treated as managed.</summary>
    public async Task<string> GetSecureModeAsync(CancellationToken cancellationToken = default)
    {
        FormPage page = await _client.GetFormAsync("/config/secure_mode.htm", cancellationToken).ConfigureAwait(false);
        return page.ValueOf("rlOperationalModeState") == "2" ? "unmanaged" : "managed";
    }

    /// <summary>Plans secure mode. Unmanaged ends the HTTP session and is destructive.</summary>
    public async Task<ChangePlan> PlanSecureModeAsync(string mode, CancellationToken cancellationToken = default)
    {
        FormPage page = await _client.GetFormAsync("/config/secure_mode.htm", cancellationToken).ConfigureAwait(false);
        bool unmanaged = mode.Trim().ToLowerInvariant() == "unmanaged";
        if (!unmanaged && mode.Trim().ToLowerInvariant() != "managed")
        {
            throw new FormatException("Secure mode must be managed or unmanaged.");
        }

        ChangePlan plan = new ChangePlan();
        plan.Add(FormChange.Post(
            _client,
            "Set secure mode to " + (unmanaged ? "unmanaged" : "managed"),
            page,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["rlOperationalModeState"] = unmanaged ? "2" : "1"
            },
            destructive: unmanaged));
        return plan;
    }

    /// <summary>Reads jumbo-frame status. The applied value takes effect after reset.</summary>
    public async Task<IReadOnlyDictionary<string, string>> GetJumboAsync(CancellationToken cancellationToken = default)
    {
        FormPage page = await _client.GetFormAsync("/config/tun_general.htm", cancellationToken).ConfigureAwait(false);
        return new Dictionary<string, string>
        {
            ["current"] = page.ValueOf("rlJumboFramesCurrentStatus") == "1" ? "on" : "off",
            ["after-reset"] = page.ValueOf("rlJumboFramesStatusAfterReset") == "1" ? "on" : "off"
        };
    }

    /// <summary>Plans the after-reset jumbo-frame flag.</summary>
    public async Task<ChangePlan> PlanJumboAsync(bool enabled, CancellationToken cancellationToken = default)
    {
        FormPage page = await _client.GetFormAsync("/config/tun_general.htm", cancellationToken).ConfigureAwait(false);
        ChangePlan plan = new ChangePlan();
        string expected = MibValues.Enable(enabled);
        plan.Add(FormChange.Post(
            _client,
            "Set jumbo frames " + (enabled ? "on" : "off") + " after reset",
            page,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["rlJumboFramesStatusAfterReset"] = expected
            },
            verifyPath: "/config/tun_general.htm",
            verify: after => after.ValueOf("rlJumboFramesStatusAfterReset") == expected
                ? null
                : "Jumbo frames did not stick"));
        return plan;
    }
}
