using System.ComponentModel;

using Microsoft.Extensions.DependencyInjection;

using ModelContextProtocol.Server;

using Nefarius.PowerConnect.Ews.Vlan;

namespace Nefarius.PowerConnect.Cli.Mcp;

/// <summary>VLAN tools. Every call names a switch alias configured outside the model.</summary>
[McpServerToolType]
public static class VlanTools
{
    [McpServerTool(Name = "vlan_list")]
    [Description("Lists VLAN ids and names on one switch.")]
    public static Task<IReadOnlyList<VlanSummary>> List(
        IServiceProvider services,
        [Description("Configured switch alias.")] string @switch,
        CancellationToken cancellationToken)
    {
        return services.GetRequiredService<SwitchToolService>().ListVlansAsync(@switch, cancellationToken);
    }

    [McpServerTool(Name = "vlan_show")]
    [Description("Shows static membership for one VLAN.")]
    public static Task<VlanDetails> Show(
        IServiceProvider services,
        [Description("Configured switch alias.")] string @switch,
        [Description("VLAN id.")] int id,
        CancellationToken cancellationToken)
    {
        return services.GetRequiredService<SwitchToolService>().ShowVlanAsync(@switch, id, cancellationToken);
    }

    [McpServerTool(Name = "vlan_create")]
    [Description("Plans creation of a VLAN. Nothing is posted unless apply is true.")]
    public static Task<McpChangeResult> Create(
        IServiceProvider services,
        [Description("Configured switch alias.")] string @switch,
        [Description("VLAN id from 2 through 4094.")] int id,
        [Description("VLAN name.")] string name,
        [Description("Post the change. Defaults to false, which returns the plan only.")] bool apply = false,
        CancellationToken cancellationToken = default)
    {
        return services.GetRequiredService<SwitchToolService>().CreateVlanAsync(@switch, id, name, apply, cancellationToken);
    }

    [McpServerTool(Name = "vlan_delete")]
    [Description("Plans deletion of a VLAN. VLAN 1 cannot be deleted. Nothing is posted unless apply is true.")]
    public static Task<McpChangeResult> Delete(
        IServiceProvider services,
        [Description("Configured switch alias.")] string @switch,
        [Description("VLAN id from 2 through 4094.")] int id,
        [Description("Post the change. Defaults to false, which returns the plan only.")] bool apply = false,
        CancellationToken cancellationToken = default)
    {
        return services.GetRequiredService<SwitchToolService>().DeleteVlanAsync(@switch, id, apply, cancellationToken);
    }

    [McpServerTool(Name = "vlan_set")]
    [Description("Plans a VLAN membership change. Port lists accept g1, g1-g8, ch1, and comma-separated combinations. Nothing is posted unless apply is true.")]
    public static Task<McpChangeResult> Set(
        IServiceProvider services,
        [Description("Configured switch alias.")] string @switch,
        [Description("VLAN id.")] int id,
        [Description("Ports to tag.")] string? tagged = null,
        [Description("Ports to leave untagged. Untagged ports are also egress members.")] string? untagged = null,
        [Description("Ports to forbid.")] string? forbidden = null,
        [Description("Ports to remove from the VLAN.")] string? none = null,
        [Description("Post the change. Defaults to false, which returns the plan only.")] bool apply = false,
        CancellationToken cancellationToken = default)
    {
        return services.GetRequiredService<SwitchToolService>().SetVlanAsync(@switch, id, tagged, untagged, forbidden, none, apply, cancellationToken);
    }

    [McpServerTool(Name = "vlan_port_list")]
    [Description("Lists PVID, frame type, and ingress filtering for every copper port and LAG.")]
    public static Task<IReadOnlyList<VlanPortSettings>> ListPorts(
        IServiceProvider services,
        [Description("Configured switch alias.")] string @switch,
        CancellationToken cancellationToken)
    {
        return services.GetRequiredService<SwitchToolService>().ListVlanPortsAsync(@switch, cancellationToken);
    }

    [McpServerTool(Name = "vlan_port_show")]
    [Description("Shows PVID, frame type, and ingress filtering for the selected ports.")]
    public static Task<IReadOnlyList<VlanPortSettings>> ShowPorts(
        IServiceProvider services,
        [Description("Configured switch alias.")] string @switch,
        [Description("Port, LAG, range, or list.")] string ports,
        CancellationToken cancellationToken)
    {
        return services.GetRequiredService<SwitchToolService>().ShowVlanPortsAsync(@switch, ports, cancellationToken);
    }

    [McpServerTool(Name = "vlan_port_set")]
    [Description("Plans PVID, frame type, or ingress filtering changes. Nothing is posted unless apply is true.")]
    public static Task<McpChangeResult> SetPorts(
        IServiceProvider services,
        [Description("Configured switch alias.")] string @switch,
        [Description("Port, LAG, range, or list.")] string ports,
        [Description("Port VLAN id.")] int? pvid = null,
        [Description("admit-all or tagged-only.")] string? frameType = null,
        [Description("on or off.")] string? ingressFiltering = null,
        [Description("Post the change. Defaults to false, which returns the plan only.")] bool apply = false,
        CancellationToken cancellationToken = default)
    {
        return services.GetRequiredService<SwitchToolService>().SetVlanPortsAsync(@switch, ports, pvid, frameType, ingressFiltering, apply, cancellationToken);
    }

    [McpServerTool(Name = "vlan_access")]
    [Description("Plans making the selected ports untagged members of one VLAN, setting their PVID, and removing them from other VLANs. Nothing is posted unless apply is true.")]
    public static Task<McpChangeResult> Access(
        IServiceProvider services,
        [Description("Configured switch alias.")] string @switch,
        [Description("Port, LAG, range, or list.")] string ports,
        [Description("Access VLAN id.")] int vlan,
        [Description("Post the change. Defaults to false, which returns the plan only.")] bool apply = false,
        CancellationToken cancellationToken = default)
    {
        return services.GetRequiredService<SwitchToolService>().AccessVlanAsync(@switch, ports, vlan, apply, cancellationToken);
    }
}
