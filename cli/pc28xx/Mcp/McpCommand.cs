using System.Reflection;
using System.Text.Json;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Nefarius.PowerConnect.Cli.Mcp;

/// <summary>Starts the stdio MCP server without letting the CLI parser write to stdout.</summary>
internal static class McpCommand
{
    public const string TargetsFileVariable = "PC28XX_MCP_TARGETS_FILE";

    public static bool IsDirectInvocation(string[] args)
    {
        return args.Length > 0 &&
               args[0].Equals("mcp", StringComparison.Ordinal) &&
               !args.Any(static arg => arg is "-h" or "--help" or "-?" or "/?");
    }

    public static Task<int> RunAsync(string[] args)
    {
        try
        {
            return RunAsync(ReadTargetsFileArgument(args.AsSpan(1)), CancellationToken.None);
        }
        catch (SwitchTargetException exception)
        {
            Console.Error.WriteLine("Error: " + exception.Message);
            return Task.FromResult(1);
        }
    }

    public static async Task<int> RunAsync(string? targetsFile, CancellationToken cancellationToken)
    {
        try
        {
            SwitchTargetRegistry registry = SwitchTargetRegistry.Load(ResolveTargetsPath(targetsFile));
            await RunServerAsync(registry, cancellationToken).ConfigureAwait(false);
            return 0;
        }
        catch (Exception exception) when (exception is SwitchTargetException or IOException)
        {
            Console.Error.WriteLine("Error: " + exception.Message);
            return 1;
        }
    }

    internal static string? ReadTargetsFileArgument(ReadOnlySpan<string> args)
    {
        string? path = null;
        for (int index = 0; index < args.Length; index++)
        {
            string arg = args[index];
            if (arg.StartsWith("--targets-file=", StringComparison.Ordinal))
            {
                path = arg["--targets-file=".Length..];
                if (path.Length == 0)
                {
                    throw new SwitchTargetException("--targets-file requires a path.");
                }

                continue;
            }

            if (arg.Equals("--targets-file", StringComparison.Ordinal))
            {
                if (index + 1 >= args.Length)
                {
                    throw new SwitchTargetException("--targets-file requires a path.");
                }

                path = args[++index];
                continue;
            }

            throw new SwitchTargetException("Unrecognized argument '" + arg + "'.");
        }

        return path;
    }

    private static string ResolveTargetsPath(string? targetsFile)
    {
        if (!string.IsNullOrWhiteSpace(targetsFile))
        {
            return targetsFile;
        }

        string? environment = Environment.GetEnvironmentVariable(TargetsFileVariable);
        if (string.IsNullOrWhiteSpace(environment))
        {
            throw new SwitchTargetException("A targets file is required via --targets-file or " + TargetsFileVariable + ".");
        }

        return environment;
    }

    private static async Task RunServerAsync(SwitchTargetRegistry registry, CancellationToken cancellationToken)
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            Args = Array.Empty<string>(),
            ContentRootPath = AppContext.BaseDirectory,
            ApplicationName = "pc28xx"
        });
        builder.Logging.ClearProviders();
        builder.Logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);
        builder.Logging.SetMinimumLevel(LogLevel.Information);
        builder.Services.Configure<ConsoleLifetimeOptions>(options => options.SuppressStatusMessages = true);

        builder.Services.AddSingleton(registry);
        builder.Services.AddSingleton<SwitchCallGate>();
        builder.Services.AddSingleton<IPowerConnectSessions, DevicePowerConnectSessions>();
        builder.Services.AddSingleton<IMcpChangeRunner, McpChangeRunner>();
        builder.Services.AddSingleton<SwitchToolService>();

        JsonSerializerOptions serializer = new JsonSerializerOptions(McpJsonUtilities.DefaultOptions)
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        builder.Services
            .AddMcpServer(options =>
            {
                options.ServerInfo = new Implementation
                {
                    Name = "pc28xx",
                    Version = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0"
                };
            })
            .WithStdioServerTransport()
            .WithToolsFromAssembly(typeof(McpCommand).Assembly, serializer);

        await builder.Build().RunAsync(cancellationToken).ConfigureAwait(false);
    }
}
