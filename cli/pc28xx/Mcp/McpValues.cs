using Nefarius.PowerConnect.Ews.Ews;

namespace Nefarius.PowerConnect.Cli.Mcp;

/// <summary>Parses MCP arguments before a switch is contacted.</summary>
internal static class McpValues
{
    public static IReadOnlyList<PortName> RequiredPorts(string? text)
    {
        return PortName.ParseSelection(text);
    }

    public static IReadOnlyList<PortName> OptionalPorts(string? text)
    {
        return PortName.ParseList(text);
    }

    public static bool OnOff(string text)
    {
        string normalized = text.Trim().ToLowerInvariant();
        if (normalized is "on" or "enable" or "true" or "1")
        {
            return true;
        }

        if (normalized is "off" or "disable" or "false" or "0")
        {
            return false;
        }

        throw new FormatException("Expected on or off, got '" + text + "'.");
    }

    public static bool AdminUp(string text)
    {
        if (text.Equals("up", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (text.Equals("down", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        throw new FormatException("Admin status must be up or down.");
    }

    public static int? Speed(int? megabits)
    {
        if (megabits == null)
        {
            return null;
        }

        if (megabits is not (10 or 100 or 1000))
        {
            throw new FormatException("Speed must be 10, 100, or 1000.");
        }

        return megabits;
    }

    public static string Duplex(string text)
    {
        string normalized = text.Trim().ToLowerInvariant();
        if (normalized is "full" or "half")
        {
            return normalized;
        }

        throw new FormatException("Duplex must be full or half.");
    }

    public static string FlowControl(string text)
    {
        string normalized = text.Trim().ToLowerInvariant();
        if (normalized is "on" or "off" or "autoneg")
        {
            return normalized;
        }

        throw new FormatException("Flow control must be on, off, or autoneg.");
    }

    public static string Mdix(string text)
    {
        string normalized = text.Trim().ToLowerInvariant();
        if (normalized is "auto" or "on" or "off")
        {
            return normalized;
        }

        throw new FormatException("MDI/MDIX must be auto, on, or off.");
    }

    public static string FrameType(string text)
    {
        string normalized = text.Trim().ToLowerInvariant();
        if (normalized is "admit-all" or "all")
        {
            return "admit-all";
        }

        if (normalized is "tagged-only" or "tagged")
        {
            return "tagged-only";
        }

        throw new FormatException("Frame type must be admit-all or tagged-only.");
    }

    public static string SecureMode(string text)
    {
        string normalized = text.Trim().ToLowerInvariant();
        if (normalized is "managed" or "unmanaged")
        {
            return normalized;
        }

        throw new FormatException("Secure mode must be managed or unmanaged.");
    }
}
