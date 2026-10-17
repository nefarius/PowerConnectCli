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
        string? queried = page.Query("rlAAALocalUserName")?.Trim();
        List<string> names = new List<string>();
        foreach (SelectOption option in page.Options("rlAAALocalUserName$select"))
        {
            string name = option.Text.Trim();
            if (name.Length == 0)
            {
                continue;
            }

            names.Add(name);
        }

        if (names.Count == 0 && queried is string current && current.Length > 0)
        {
            names.Add(current);
        }

        List<Dictionary<string, string>> users = new List<Dictionary<string, string>>();
        foreach (string name in names)
        {
            FormPage userPage = string.Equals(name, queried, StringComparison.Ordinal)
                ? page
                : await _client.GetFormAsync(UserPage(name), cancellationToken).ConfigureAwait(false);
            users.Add(new Dictionary<string, string>
            {
                ["name"] = name,
                ["level"] = userPage.Query("rlAAALocalUserPrivilage") ?? ""
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
        FormPage page = await _client.GetFormAsync(create ? "/config/sec_db.htm" : UserPage(name), cancellationToken).ConfigureAwait(false);
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

        FormPage page = await _client.GetFormAsync(UserPage(name), cancellationToken).ConfigureAwait(false);
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

    private static string UserPage(string name)
    {
        return "/config/sec_db.htm?Query:rlAAALocalUserName=" + Uri.EscapeDataString(name);
    }
}

/// <summary>Create, update, or delete one RADIUS server row.</summary>
public enum RadiusRowAction
{
    /// <summary>Post the <c>$add</c> fields with create-and-go status.</summary>
    Create,

    /// <summary>Post the <c>$query</c> fields with active status.</summary>
    Update,

    /// <summary>Post the <c>$query</c> fields with destroy status.</summary>
    Delete
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
        RadiusRowAction action,
        CancellationToken cancellationToken = default)
    {
        string path = action == RadiusRowAction.Create
            ? "/config/radius_params.htm"
            : "/config/radius_params.htm?Query:rlRadiusServerInetAddress=" + Uri.EscapeDataString(address);
        FormPage page = await _client.GetFormAsync(path, cancellationToken).ConfigureAwait(false);
        string suffix = action == RadiusRowAction.Create ? "$add" : "$query";
        string status = action switch
        {
            RadiusRowAction.Delete => "6",
            RadiusRowAction.Create => "4",
            _ => "1"
        };
        Dictionary<string, string> mutations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["rlRadiusServerInetAddress" + suffix] = address,
            ["rlRadiusServerInetStatus" + suffix] = status
        };
        if (key != null)
        {
            mutations["rlRadiusServerInetKey" + suffix] = key;
        }

        if (authPort.HasValue)
        {
            mutations["rlRadiusServerInetAuthPortNumber" + suffix] = MibValues.Number(authPort.Value);
        }

        if (timeout.HasValue)
        {
            mutations["showTimeout"] = MibValues.Number(timeout.Value);
        }

        if (retries.HasValue)
        {
            mutations["showRetries"] = MibValues.Number(retries.Value);
        }

        string verb = action switch
        {
            RadiusRowAction.Delete => "Delete",
            RadiusRowAction.Create => "Add",
            _ => "Update"
        };
        ChangePlan plan = new ChangePlan();
        plan.Add(FormChange.Post(
            _client,
            verb + " RADIUS server " + address,
            page,
            mutations,
            destructive: action == RadiusRowAction.Delete));
        return plan;
    }
}
