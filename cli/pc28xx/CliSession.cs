using System.CommandLine;

using Nefarius.PowerConnect.Ews;
using Nefarius.PowerConnect.Ews.Changes;
using Nefarius.PowerConnect.Ews.Http;

using Serilog;

using Spectre.Console;

namespace Nefarius.PowerConnect.Cli;

/// <summary>Parsed global options for one invocation.</summary>
internal sealed class CliSession
{
    public required string Host { get; init; }

    public required string User { get; init; }

    public required string Password { get; init; }

    public bool Yes { get; init; }

    public bool DryRun { get; init; }

    public bool Json { get; init; }

    public bool IUnderstand { get; init; }

    public bool Verbose { get; init; }

    public int TimeoutSeconds { get; init; }

    public string? CaptureDirectory { get; init; }

    public SwitchConnection Connection => new SwitchConnection
    {
        Host = Host,
        User = User,
        Password = Password,
        Timeout = TimeSpan.FromSeconds(TimeoutSeconds),
        CaptureDirectory = CaptureDirectory
    };

    public ChangeExecutionOptions Execution(Action<string> write)
    {
        return new ChangeExecutionOptions
        {
            Yes = Yes,
            DryRun = DryRun,
            IUnderstand = IUnderstand,
            WriteLine = write,
            ConfirmAsync = AnsiConsole.Profile.Capabilities.Interactive && !Console.IsInputRedirected
                ? plan => Task.FromResult(AnsiConsole.Confirm("Apply these changes?"))
                : null
        };
    }
}

/// <summary>Shared options and the connect/apply helpers.</summary>
internal static class CliHost
{
    public static readonly Option<string> Host = new("--host")
    {
        Description = "Switch address",
        Recursive = true,
        DefaultValueFactory = _ => "192.168.2.17"
    };

    public static readonly Option<string> User = new("--user")
    {
        Description = "Web UI user",
        Recursive = true,
        DefaultValueFactory = _ => "admin"
    };

    public static readonly Option<string?> Password = new("--password")
    {
        Description = "Web UI password. Defaults to the PC28XX_PASSWORD environment variable",
        Recursive = true
    };

    public static readonly Option<bool> Yes = new("--yes")
    {
        Description = "Apply writes without prompting",
        Recursive = true
    };

    public static readonly Option<bool> DryRun = new("--dry-run")
    {
        Description = "Print the change plan and do not post",
        Recursive = true
    };

    public static readonly Option<bool> Json = new("--json")
    {
        Description = "Print results as JSON",
        Recursive = true
    };

    public static readonly Option<bool> IUnderstand = new("--i-understand")
    {
        Description = "Required for reset, factory defaults, firmware or boot download, unmanaged secure mode, and IP changes",
        Recursive = true
    };

    public static readonly Option<bool> Verbose = new("--verbose")
    {
        Description = "Log HTTP traffic (passwords redacted)",
        Recursive = true
    };

    public static readonly Option<int> Timeout = new("--timeout")
    {
        Description = "Per-request timeout in seconds",
        Recursive = true,
        DefaultValueFactory = _ => 30
    };

    public static readonly Option<string?> CaptureDirectory = new("--capture-dir")
    {
        Description = "Directory for redacted request and response captures",
        Recursive = true
    };

    public static void AddGlobals(Command command)
    {
        command.Options.Add(Host);
        command.Options.Add(User);
        command.Options.Add(Password);
        command.Options.Add(Yes);
        command.Options.Add(DryRun);
        command.Options.Add(Json);
        command.Options.Add(IUnderstand);
        command.Options.Add(Verbose);
        command.Options.Add(Timeout);
        command.Options.Add(CaptureDirectory);
    }

    public static CliSession Read(ParseResult parse)
    {
        string? password = parse.GetValue(Password);
        if (string.IsNullOrEmpty(password))
        {
            password = Environment.GetEnvironmentVariable("PC28XX_PASSWORD");
        }

        if (string.IsNullOrEmpty(password))
        {
            throw new SwitchException("Password is required via --password or PC28XX_PASSWORD.");
        }

        return new CliSession
        {
            Host = parse.GetValue(Host) ?? "192.168.2.17",
            User = parse.GetValue(User) ?? "admin",
            Password = password,
            Yes = parse.GetValue(Yes),
            DryRun = parse.GetValue(DryRun),
            Json = parse.GetValue(Json),
            IUnderstand = parse.GetValue(IUnderstand),
            Verbose = parse.GetValue(Verbose),
            TimeoutSeconds = parse.GetValue(Timeout),
            CaptureDirectory = parse.GetValue(CaptureDirectory)
        };
    }

    public static async Task<int> Run(ParseResult parse, Func<SwitchDevice, CliSession, CancellationToken, Task<int>> action)
    {
        CliSession session = Read(parse);
        if (session.Verbose)
        {
            Log.Logger = new LoggerConfiguration().MinimumLevel.Debug().WriteTo.Console().CreateLogger();
        }

        await using SwitchDevice device = await SwitchDevice.ConnectAsync(session.Connection, Log.Logger).ConfigureAwait(false);
        return await action(device, session, CancellationToken.None).ConfigureAwait(false);
    }

    public static async Task<int> Apply(SwitchDevice device, CliSession session, ChangePlan plan)
    {
        ChangeExecutor executor = new ChangeExecutor();
        return await executor.ExecuteAsync(plan, session.Execution(AnsiConsole.WriteLine)).ConfigureAwait(false);
    }
}
