using System.ComponentModel;

using Microsoft.Extensions.DependencyInjection;

using ModelContextProtocol.Server;

namespace Nefarius.PowerConnect.Cli.Mcp;

/// <summary>Alias discovery and raw page access.</summary>
[McpServerToolType]
public static class SwitchTools
{
    [McpServerTool(Name = "list_switches")]
    [Description("Lists configured switch aliases and their non-secret connection settings. Passwords are never returned.")]
    public static IReadOnlyList<SwitchTargetInfo> List(IServiceProvider services)
    {
        return services.GetRequiredService<SwitchToolService>().ListSwitches();
    }

    [McpServerTool(Name = "raw_get")]
    [Description("Reads one web UI page and redacts password, RADIUS key, and SNMP community fields.")]
    public static Task<IReadOnlyDictionary<string, string>> Get(
        IServiceProvider services,
        [Description("Configured switch alias.")] string @switch,
        [Description("Page file name or /config path.")] string page,
        CancellationToken cancellationToken)
    {
        return services.GetRequiredService<SwitchToolService>().RawGetAsync(@switch, page, cancellationToken);
    }

    [McpServerTool(Name = "raw_set")]
    [Description("Plans a raw field write. Values are posted only when apply and i_understand are both true. Secret field values are redacted in the result.")]
    public static Task<McpChangeResult> Set(
        IServiceProvider services,
        [Description("Configured switch alias.")] string @switch,
        [Description("Page file name or /config path.")] string page,
        [Description("Form fields to post.")] IReadOnlyDictionary<string, string> fields,
        [Description("Post the change. Defaults to false, which returns the plan only.")] bool apply = false,
        [Description("Required together with apply. Raw writes are always treated as destructive.")] bool i_understand = false,
        CancellationToken cancellationToken = default)
    {
        return services.GetRequiredService<SwitchToolService>().RawSetAsync(@switch, page, fields, apply, i_understand, cancellationToken);
    }
}
