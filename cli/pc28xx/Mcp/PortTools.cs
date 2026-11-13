using System.ComponentModel;

using Microsoft.Extensions.DependencyInjection;

using ModelContextProtocol.Server;

using Nefarius.PowerConnect.Ews.Ports;

namespace Nefarius.PowerConnect.Cli.Mcp;

/// <summary>Copper port tools.</summary>
[McpServerToolType]
public static class PortTools
{
    [McpServerTool(Name = "port_list")]
    [Description("Lists administrative and operational state for copper ports g1 through g16.")]
    public static Task<IReadOnlyList<PortConfiguration>> List(
        IServiceProvider services,
        [Description("Configured switch alias.")] string @switch,
        CancellationToken cancellationToken)
    {
        return services.GetRequiredService<SwitchToolService>().ListPortsAsync(@switch, cancellationToken);
    }

    [McpServerTool(Name = "port_show")]
    [Description("Shows administrative and operational state for the selected copper ports.")]
    public static Task<IReadOnlyList<PortConfiguration>> Show(
        IServiceProvider services,
        [Description("Configured switch alias.")] string @switch,
        [Description("Port, range, or list.")] string ports,
        CancellationToken cancellationToken)
    {
        return services.GetRequiredService<SwitchToolService>().ShowPortsAsync(@switch, ports, cancellationToken);
    }

    [McpServerTool(Name = "port_set")]
    [Description("Plans copper port changes. Nothing is posted unless apply is true.")]
    public static Task<McpChangeResult> Set(
        IServiceProvider services,
        [Description("Configured switch alias.")] string @switch,
        [Description("Port, range, or list.")] string ports,
        [Description("up or down.")] string? admin = null,
        [Description("Port description.")] string? description = null,
        [Description("10, 100, or 1000.")] int? speed = null,
        [Description("full or half.")] string? duplex = null,
        [Description("on or off.")] string? autoNegotiation = null,
        [Description("on, off, or autoneg.")] string? flowControl = null,
        [Description("auto, on, or off.")] string? mdix = null,
        [Description("on or off.")] string? backPressure = null,
        [Description("Post the change. Defaults to false, which returns the plan only.")] bool apply = false,
        CancellationToken cancellationToken = default)
    {
        return services.GetRequiredService<SwitchToolService>().SetPortsAsync(
            @switch,
            ports,
            admin,
            description,
            speed,
            duplex,
            autoNegotiation,
            flowControl,
            mdix,
            backPressure,
            apply,
            cancellationToken);
    }
}
