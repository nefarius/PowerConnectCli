using Nefarius.PowerConnect.Ews.Changes;
using Nefarius.PowerConnect.Ews.Common;
using Nefarius.PowerConnect.Ews.Ews;
using Nefarius.PowerConnect.Ews.Http;

namespace Nefarius.PowerConnect.Ews.Security;

/// <summary>Local management users.</summary>
public sealed class UserRepository
{
    private readonly SwitchClient _client;

    /// <summary>Creates a repository bound to a session.</summary>
    public UserRepository(SwitchClient client)
    {
        _client = client;
    }

    /// <summary>Lists user names and privilege levels. Passwords are not returned.</summary>
    public async Task<IReadOnlyList<IReadOnlyDictionary<string, string>>> ListAsync(CancellationToken cancellationToken = default)
    {
        FormPage page = await _client.GetFormAsync("/config/sec_db.htm", cancellationToken).ConfigureAwait(false);
        List<Dictionary<string, string>> users = new List<Dictionary<string, string>>();
        foreach (SelectOption option in page.Options("rlAAALocalUserName$select"))
        {
            if (string.IsNullOrWhiteSpace(option.Text))
            {
                continue;
            }

            users.Add(new Dictionary<string, string>
            {
                ["name"] = option.Text.Trim(),
                ["level"] = page.Query("rlAAALocalUserPrivilage") ?? ""
            });
        }

        if (users.Count == 0 && page.Query("rlAAALocalUserName") is string current && current.Length > 0)
        {
            users.Add(new Dictionary<string, string>
            {
                ["name"] = current,
                ["level"] = page.Query("rlAAALocalUserPrivilage") ?? ""
            });
        }

        return users;
    }

    /// <summary>
    ///     Plans a user add or update. The password is posted as <c>$</c> plus the cleartext, which is how the page
    ///     fills <c>rlAAALocalUserPassword$query</c>. The plan redacts it.
    /// </summary>
    public async Task<ChangePlan> PlanUpsertAsync(
        string name,
        string? password,
        int? level,
        bool create,
        CancellationToken cancellationToken = default)
    {
        string url = "/config/sec_db.htm?Query:rlAAALocalUserName=" + Uri.EscapeDataString(name);
        FormPage page = await _client.GetFormAsync(create ? "/config/sec_db.htm" : url, cancellationToken).ConfigureAwait(false);
        Dictionary<string, string> mutations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["rlAAALocalUserName$query"] = name,
            ["rlAAALocalHostStatus$query"] = "1"
        };
        if (level.HasValue)
        {
            mutations["rlAAALocalUserPrivilage$query"] = MibValues.Number(level.Value);
        }

        if (password != null)
        {
            mutations["rlAAALocalUserPassword$query"] = "$" + password;
        }

        ChangePlan plan = new ChangePlan();
        plan.Add(FormChange.Post(_client, (create ? "Add" : "Update") + " user " + name, page, mutations));
        return plan;
    }

    /// <summary>Plans deletion. Refuses to delete the user that opened the session when names match.</summary>
    public async Task<ChangePlan> PlanDeleteAsync(string name, string sessionUser, CancellationToken cancellationToken = default)
    {
        if (string.Equals(name, sessionUser, StringComparison.OrdinalIgnoreCase))
        {
            throw new SwitchException("Refusing to delete the user that is logged in (" + sessionUser + ").");
        }

        FormPage page = await _client.GetFormAsync(
            "/config/sec_db.htm?Query:rlAAALocalUserName=" + Uri.EscapeDataString(name),
            cancellationToken).ConfigureAwait(false);
        ChangePlan plan = new ChangePlan();
        plan.Add(FormChange.Post(
            _client,
            "Delete user " + name,
            page,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["rlAAALocalUserName$query"] = name,
                ["rlAAALocalHostStatus$query"] = "6"
            },
            destructive: false));
        return plan;
    }
}

/// <summary>RADIUS server rows.</summary>
public sealed class RadiusRepository
{
    private readonly SwitchClient _client;

    /// <summary>Creates a repository bound to a session.</summary>
    public RadiusRepository(SwitchClient client)
    {
        _client = client;
    }

    /// <summary>Reads the RADIUS page.</summary>
    public Task<FormPage> GetAsync(CancellationToken cancellationToken = default)
    {
        return _client.GetFormAsync("/config/radius_params.htm", cancellationToken);
    }

    /// <summary>Plans a RADIUS row create, update, or delete using the page's <c>$add</c> / <c>$query</c> fields.</summary>
    public async Task<ChangePlan> PlanAsync(
        string address,
        string? key,
        int? authPort,
        int? timeout,
        int? retries,
        bool delete,
        CancellationToken cancellationToken = default)
    {
        FormPage page = await GetAsync(cancellationToken).ConfigureAwait(false);
        string suffix = delete ? "$query" : "$add";
        Dictionary<string, string> mutations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["rlRadiusServerInetAddress" + suffix] = address,
            ["rlRadiusServerInetRowStatus" + suffix] = delete ? "6" : "4"
        };
        if (key != null)
        {
            mutations["rlRadiusServerInetKey" + suffix] = key;
        }

        if (authPort.HasValue)
        {
            mutations["rlRadiusServerInetAuthPort" + suffix] = MibValues.Number(authPort.Value);
        }

        if (timeout.HasValue)
        {
            mutations["showTimeout"] = MibValues.Number(timeout.Value);
        }

        if (retries.HasValue)
        {
            mutations["showRetries"] = MibValues.Number(retries.Value);
        }

        ChangePlan plan = new ChangePlan();
        plan.Add(FormChange.Post(
            _client,
            (delete ? "Delete" : "Set") + " RADIUS server " + address,
            page,
            mutations,
            destructive: delete));
        return plan;
    }
}
