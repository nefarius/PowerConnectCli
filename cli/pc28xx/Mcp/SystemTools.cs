using System.ComponentModel;

using Microsoft.Extensions.DependencyInjection;

using ModelContextProtocol.Server;

namespace Nefarius.PowerConnect.Cli.Mcp;

/// <summary>System identity, secure mode, jumbo frames, and reset.</summary>
[McpServerToolType]
public static class SystemTools
{
    [McpServerTool(Name = "system_asset")]
    [Description("Reads the switch name, contact, location, MAC, service tag, and serial number.")]
    public static Task<IReadOnlyDictionary<string, string>> Asset(
        IServiceProvider services,
        [Description("Configured switch alias.")] string @switch,
        CancellationToken cancellationToken)
    {
        return services.GetRequiredService<SwitchToolService>().AssetAsync(@switch, cancellationToken);
    }

    [McpServerTool(Name = "system_versions")]
    [Description("Reads firmware, boot, and hardware versions.")]
    public static Task<IReadOnlyDictionary<string, string>> Versions(
        IServiceProvider services,
        [Description("Configured switch alias.")] string @switch,
        CancellationToken cancellationToken)
    {
        return services.GetRequiredService<SwitchToolService>().VersionsAsync(@switch, cancellationToken);
    }

    [McpServerTool(Name = "system_set_asset_tag")]
    [Description("Plans an asset-tag write. Nothing is posted unless apply is true.")]
    public static Task<McpChangeResult> SetAssetTag(
        IServiceProvider services,
        [Description("Configured switch alias.")] string @switch,
        [Description("Asset tag.")] string tag,
        [Description("Post the change. Defaults to false, which returns the plan only.")] bool apply = false,
        CancellationToken cancellationToken = default)
    {
        return services.GetRequiredService<SwitchToolService>().SetAssetTagAsync(@switch, tag, apply, cancellationToken);
    }

    [McpServerTool(Name = "system_secure_mode")]
    [Description("Reads secure mode. unmanaged means the web UI is disabled.")]
    public static Task<SecureModeInfo> SecureMode(
        IServiceProvider services,
        [Description("Configured switch alias.")] string @switch,
        CancellationToken cancellationToken)
    {
        return services.GetRequiredService<SwitchToolService>().SecureModeAsync(@switch, cancellationToken);
    }

    [McpServerTool(Name = "system_set_secure_mode")]
    [Description("Plans a secure-mode change. unmanaged ends management on the network and is posted only when apply and i_understand are both true.")]
    public static Task<McpChangeResult> SetSecureMode(
        IServiceProvider services,
        [Description("Configured switch alias.")] string @switch,
        [Description("managed or unmanaged.")] string mode,
        [Description("Post the change. Defaults to false, which returns the plan only.")] bool apply = false,
        [Description("Required together with apply when mode is unmanaged.")] bool i_understand = false,
        CancellationToken cancellationToken = default)
    {
        return services.GetRequiredService<SwitchToolService>().SetSecureModeAsync(@switch, mode, apply, i_understand, cancellationToken);
    }

    [McpServerTool(Name = "system_jumbo")]
    [Description("Reads the current jumbo-frame state and the state that takes effect after reset.")]
    public static Task<IReadOnlyDictionary<string, string>> Jumbo(
        IServiceProvider services,
        [Description("Configured switch alias.")] string @switch,
        CancellationToken cancellationToken)
    {
        return services.GetRequiredService<SwitchToolService>().JumboAsync(@switch, cancellationToken);
    }

    [McpServerTool(Name = "system_set_jumbo")]
    [Description("Plans the jumbo-frame state used after the next reset. Nothing is posted unless apply is true.")]
    public static Task<McpChangeResult> SetJumbo(
        IServiceProvider services,
        [Description("Configured switch alias.")] string @switch,
        [Description("on or off.")] string state,
        [Description("Post the change. Defaults to false, which returns the plan only.")] bool apply = false,
        CancellationToken cancellationToken = default)
    {
        return services.GetRequiredService<SwitchToolService>().SetJumboAsync(@switch, state, apply, cancellationToken);
    }

    [McpServerTool(Name = "system_reset")]
    [Description("Plans a switch reset. The reset is posted only when apply and i_understand are both true.")]
    public static Task<McpChangeResult> Reset(
        IServiceProvider services,
        [Description("Configured switch alias.")] string @switch,
        [Description("Post the reset. Defaults to false, which returns the plan only.")] bool apply = false,
        [Description("Required together with apply.")] bool i_understand = false,
        CancellationToken cancellationToken = default)
    {
        return services.GetRequiredService<SwitchToolService>().ResetAsync(@switch, apply, i_understand, cancellationToken);
    }
}
