using System.Globalization;

using Nefarius.PowerConnect.Ews.Ews;

namespace Nefarius.PowerConnect.Ews.Common;

/// <summary>
///     Translations between CLI words and the integer enumerations the web UI posts.
/// </summary>
public static class MibValues
{
    /// <summary>1 = enable, 2 = disable. This is the common EWS boolean.</summary>
    public static string Enable(bool on)
    {
        return on ? "1" : "2";
    }

    /// <summary>True when the value is the enabled enumeration.</summary>
    public static bool IsEnabled(string? value)
    {
        return value == "1";
    }

    /// <summary>Parses on/off, enable/disable, true/false, 1/2.</summary>
    public static bool ParseOnOff(string text)
    {
        string value = text.Trim().ToLowerInvariant();
        if (value is "on" or "enable" or "enabled" or "true" or "1" or "yes")
        {
            return true;
        }

        if (value is "off" or "disable" or "disabled" or "false" or "2" or "no")
        {
            return false;
        }

        throw new FormatException("Expected on or off, got '" + text + "'.");
    }

    /// <summary>Admin speed in bits per second, matching <c>swIfSpeedAdminMode</c>.</summary>
    public static string Speed(int megabits)
    {
        return megabits switch
        {
            10 => "10000000",
            100 => "100000000",
            1000 => "1000000000",
            _ => throw new FormatException("Speed must be 10, 100, or 1000.")
        };
    }

    /// <summary>Display form of an admin speed value.</summary>
    public static string SpeedText(string? value)
    {
        return value switch
        {
            "10000000" => "10",
            "100000000" => "100",
            "1000000000" => "1000",
            "0" or null or "" => "auto",
            _ => value
        };
    }

    /// <summary>Duplex select values: half = 2, full = 3.</summary>
    public static string Duplex(string text)
    {
        return text.Trim().ToLowerInvariant() switch
        {
            "half" => "2",
            "full" => "3",
            _ => throw new FormatException("Duplex must be full or half.")
        };
    }

    /// <summary>Flow control: on = 1, off = 2, autoneg = 3.</summary>
    public static string FlowControl(string text)
    {
        return text.Trim().ToLowerInvariant() switch
        {
            "on" or "enable" => "1",
            "off" or "disable" => "2",
            "autoneg" or "auto" => "3",
            _ => throw new FormatException("Flow control must be on, off, or autoneg.")
        };
    }

    /// <summary>MDIX: on (MDIX) = 1, off (MDI) = 2, auto = 3.</summary>
    public static string Mdix(string text)
    {
        return text.Trim().ToLowerInvariant() switch
        {
            "on" or "mdix" => "1",
            "off" or "mdi" => "2",
            "auto" => "3",
            _ => throw new FormatException("MDIX must be auto, on, or off.")
        };
    }

    /// <summary>Formats a list of port names.</summary>
    public static string Join(IEnumerable<PortName> ports)
    {
        return string.Join(",", ports.Select(port => port.Name));
    }

    /// <summary>Invariant integer text.</summary>
    public static string Number(int value)
    {
        return value.ToString(CultureInfo.InvariantCulture);
    }
}
