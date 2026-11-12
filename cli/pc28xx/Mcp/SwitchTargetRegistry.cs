using System.Text.Json;
using System.Text.RegularExpressions;

using Nefarius.PowerConnect.Ews.Http;

namespace Nefarius.PowerConnect.Cli.Mcp;

/// <summary>One configured switch. The password stays in the named environment variable.</summary>
internal sealed class SwitchTarget
{
    public required string Alias { get; init; }

    public required string Host { get; init; }

    public required string User { get; init; }

    public required string PasswordEnvironmentVariable { get; init; }

    public int TimeoutSeconds { get; init; } = 30;

    public int LoginRetries { get; init; } = 3;

    public double LoginRetryDelaySeconds { get; init; } = 2;

    public string? CaptureDirectory { get; init; }

    public bool Verbose { get; init; }

    public SwitchTargetInfo Describe()
    {
        return new SwitchTargetInfo
        {
            Alias = Alias,
            Host = Host,
            User = User,
            TimeoutSeconds = TimeoutSeconds,
            LoginRetries = LoginRetries,
            LoginRetryDelaySeconds = LoginRetryDelaySeconds
        };
    }
}

/// <summary>Loads named switch targets and resolves each password from the environment at call time.</summary>
internal sealed class SwitchTargetRegistry
{
    private static readonly string[] KnownProperties =
    {
        "alias",
        "host",
        "user",
        "passwordEnvironmentVariable",
        "timeoutSeconds",
        "loginRetries",
        "loginRetryDelaySeconds",
        "captureDirectory",
        "verbose"
    };

    private static readonly Regex AliasPattern = new Regex("^[A-Za-z0-9][A-Za-z0-9_-]{0,63}$", RegexOptions.CultureInvariant);
    private static readonly Regex EnvironmentVariablePattern = new Regex("^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.CultureInvariant);

    private readonly Dictionary<string, SwitchTarget> _targets;
    private readonly Func<string, string?> _environment;

    private SwitchTargetRegistry(Dictionary<string, SwitchTarget> targets, Func<string, string?> environment)
    {
        _targets = targets;
        _environment = environment;
    }

    public IReadOnlyList<SwitchTargetInfo> List()
    {
        return _targets.Values
            .OrderBy(target => target.Alias, StringComparer.OrdinalIgnoreCase)
            .Select(target => target.Describe())
            .ToList();
    }

    public static SwitchTargetRegistry Load(string path, Func<string, string?>? environment = null)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new SwitchTargetException("A targets file path is required.");
        }

        if (!File.Exists(path))
        {
            throw new SwitchTargetException("Targets file was not found: " + path);
        }

        string json;
        try
        {
            json = File.ReadAllText(path);
        }
        catch (IOException exception)
        {
            throw new SwitchTargetException("Targets file could not be read: " + exception.Message);
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException exception)
        {
            throw new SwitchTargetException("Targets file is not valid JSON: " + exception.Message);
        }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new SwitchTargetException("Targets file must be a JSON object.");
            }

            if (!TryGet(document.RootElement, "switches", out JsonElement switches) || switches.ValueKind != JsonValueKind.Array)
            {
                throw new SwitchTargetException("Targets file must contain a switches array.");
            }

            if (switches.GetArrayLength() == 0)
            {
                throw new SwitchTargetException("Targets file must name at least one switch.");
            }

            Dictionary<string, SwitchTarget> targets = new Dictionary<string, SwitchTarget>(StringComparer.OrdinalIgnoreCase);
            foreach (JsonElement item in switches.EnumerateArray())
            {
                SwitchTarget target = ReadTarget(item);
                if (!targets.TryAdd(target.Alias, target))
                {
                    throw new SwitchTargetException("Switch alias '" + target.Alias + "' is listed more than once.");
                }
            }

            return new SwitchTargetRegistry(targets, environment ?? Environment.GetEnvironmentVariable);
        }
    }

    public SwitchTarget Get(string alias)
    {
        string key = NormalizeAlias(alias);
        if (!_targets.TryGetValue(key, out SwitchTarget? target))
        {
            string known = string.Join(", ", _targets.Keys.OrderBy(name => name, StringComparer.OrdinalIgnoreCase));
            throw new SwitchTargetException("Unknown switch '" + key + "'. Configured aliases: " + known + ".");
        }

        return target;
    }

    public SwitchConnection CreateConnection(SwitchTarget target)
    {
        string? password = _environment(target.PasswordEnvironmentVariable);
        if (string.IsNullOrEmpty(password))
        {
            throw new SwitchTargetException(
                "Password for switch '" + target.Alias + "' is not set in " + target.PasswordEnvironmentVariable + ".");
        }

        return new SwitchConnection
        {
            Host = target.Host,
            User = target.User,
            Password = password,
            Timeout = TimeSpan.FromSeconds(target.TimeoutSeconds),
            LoginRetries = target.LoginRetries,
            LoginRetryDelay = TimeSpan.FromSeconds(target.LoginRetryDelaySeconds),
            CaptureDirectory = target.CaptureDirectory
        };
    }

    private static string NormalizeAlias(string alias)
    {
        if (string.IsNullOrWhiteSpace(alias))
        {
            throw new FormatException("A switch alias is required.");
        }

        return alias.Trim();
    }

    private static SwitchTarget ReadTarget(JsonElement item)
    {
        if (item.ValueKind != JsonValueKind.Object)
        {
            throw new SwitchTargetException("Each switch target must be a JSON object.");
        }

        foreach (JsonProperty property in item.EnumerateObject())
        {
            if (property.Name.Equals("password", StringComparison.OrdinalIgnoreCase) ||
                property.Name.Equals("passwd", StringComparison.OrdinalIgnoreCase))
            {
                throw new SwitchTargetException(
                    "Switch targets must not contain passwords. Set passwordEnvironmentVariable to the name of an environment variable.");
            }

            if (!KnownProperties.Any(name => name.Equals(property.Name, StringComparison.OrdinalIgnoreCase)))
            {
                throw new SwitchTargetException("Unrecognized switch property '" + property.Name + "'.");
            }
        }

        string alias = RequiredString(item, "alias");
        if (!AliasPattern.IsMatch(alias))
        {
            throw new SwitchTargetException("Switch alias '" + alias + "' must start with a letter or digit and contain only letters, digits, '_' or '-'.");
        }

        string host = RequiredString(item, "host");
        ValidateHost(alias, host);
        string variable = RequiredString(item, "passwordEnvironmentVariable");
        if (!EnvironmentVariablePattern.IsMatch(variable))
        {
            throw new SwitchTargetException("passwordEnvironmentVariable for switch '" + alias + "' is not an environment variable name.");
        }

        int timeout = OptionalInt(item, "timeoutSeconds") ?? 30;
        if (timeout is < 1 or > 3600)
        {
            throw new SwitchTargetException("timeoutSeconds for switch '" + alias + "' must be between 1 and 3600.");
        }

        int retries = OptionalInt(item, "loginRetries") ?? 3;
        if (retries < 0 || retries > 10)
        {
            throw new SwitchTargetException("loginRetries for switch '" + alias + "' must be between 0 and 10.");
        }

        double delay = OptionalDouble(item, "loginRetryDelaySeconds") ?? 2d;
        if (double.IsNaN(delay) || double.IsInfinity(delay) || delay < 0 || delay > TimeSpan.MaxValue.TotalSeconds)
        {
            throw new SwitchTargetException("loginRetryDelaySeconds for switch '" + alias + "' must be a non-negative number of seconds.");
        }

        string? capture = OptionalString(item, "captureDirectory");
        if (capture != null && string.IsNullOrWhiteSpace(capture))
        {
            throw new SwitchTargetException("captureDirectory for switch '" + alias + "' is empty.");
        }

        string user = OptionalString(item, "user")?.Trim() ?? "admin";
        if (user.Length == 0)
        {
            throw new SwitchTargetException("user for switch '" + alias + "' is empty.");
        }

        return new SwitchTarget
        {
            Alias = alias,
            Host = host,
            User = user,
            PasswordEnvironmentVariable = variable,
            TimeoutSeconds = timeout,
            LoginRetries = retries,
            LoginRetryDelaySeconds = delay,
            CaptureDirectory = string.IsNullOrWhiteSpace(capture) ? null : capture,
            Verbose = OptionalBool(item, "verbose") ?? false
        };
    }

    private static void ValidateHost(string alias, string host)
    {
        if (host.Any(char.IsWhiteSpace) || host.Contains('@', StringComparison.Ordinal))
        {
            throw new SwitchTargetException("Host for switch '" + alias + "' must not contain whitespace or credentials.");
        }

        if (!host.Contains("://", StringComparison.Ordinal))
        {
            return;
        }

        if (!Uri.TryCreate(host, UriKind.Absolute, out Uri? uri) ||
            !uri.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase) ||
            !string.IsNullOrEmpty(uri.UserInfo))
        {
            throw new SwitchTargetException("Host for switch '" + alias + "' must be an HTTP address without credentials.");
        }
    }

    private static string RequiredString(JsonElement item, string name)
    {
        string? value = OptionalString(item, name);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new SwitchTargetException("Switch target is missing " + name + ".");
        }

        return value.Trim();
    }

    private static string? OptionalString(JsonElement item, string name)
    {
        if (!TryGet(item, name, out JsonElement value))
        {
            return null;
        }

        if (value.ValueKind != JsonValueKind.String)
        {
            throw new SwitchTargetException(name + " must be a string.");
        }

        return value.GetString();
    }

    private static int? OptionalInt(JsonElement item, string name)
    {
        if (!TryGet(item, name, out JsonElement value))
        {
            return null;
        }

        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out int number))
        {
            throw new SwitchTargetException(name + " must be an integer.");
        }

        return number;
    }

    private static double? OptionalDouble(JsonElement item, string name)
    {
        if (!TryGet(item, name, out JsonElement value))
        {
            return null;
        }

        if (value.ValueKind != JsonValueKind.Number || !value.TryGetDouble(out double number))
        {
            throw new SwitchTargetException(name + " must be a number.");
        }

        return number;
    }

    private static bool? OptionalBool(JsonElement item, string name)
    {
        if (!TryGet(item, name, out JsonElement value))
        {
            return null;
        }

        if (value.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            throw new SwitchTargetException(name + " must be true or false.");
        }

        return value.GetBoolean();
    }

    private static bool TryGet(JsonElement item, string name, out JsonElement value)
    {
        foreach (JsonProperty property in item.EnumerateObject())
        {
            if (property.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }
}
