using System.Globalization;

namespace Nefarius.PowerConnect.Ews.Ews;

/// <summary>
///     A physical port (<c>g1</c>..<c>g16</c>) or LAG (<c>ch1</c>..<c>ch8</c>) and its ifIndex.
/// </summary>
public readonly record struct PortName
{
    /// <summary>Number of copper ports on a 2816.</summary>
    public const int PortCount = 16;

    /// <summary>Number of LAGs.</summary>
    public const int LagCount = 8;

    /// <summary>ifIndex of <c>ch1</c>.</summary>
    public const int LagBaseIfIndex = 1000;

    /// <summary>
    ///     Creates a port name.
    /// </summary>
    public PortName(string name, int ifIndex)
    {
        Name = name;
        IfIndex = ifIndex;
    }

    /// <summary>Canonical name such as <c>g5</c> or <c>ch2</c>.</summary>
    public string Name { get; }

    /// <summary>Interface index used by the web UI.</summary>
    public int IfIndex { get; }

    /// <summary>True for <c>ch*</c> names.</summary>
    public bool IsLag => Name.StartsWith("ch", StringComparison.Ordinal);

    /// <summary>
    ///     Parses <c>gN</c>, <c>chN</c>, or a bare ifIndex.
    /// </summary>
    public static PortName Parse(string text)
    {
        if (!TryParse(text, out PortName port))
        {
            throw new FormatException($"'{text}' is not a port (g1-g16) or LAG (ch1-ch8).");
        }

        return port;
    }

    /// <summary>
    ///     Parses <c>gN</c>, <c>chN</c>, or a bare ifIndex.
    /// </summary>
    public static bool TryParse(string? text, out PortName port)
    {
        port = default;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        string value = text.Trim().ToLowerInvariant();
        if (value.StartsWith("gigabitethernet", StringComparison.Ordinal))
        {
            value = "g" + value["gigabitethernet".Length..];
        }

        if (value.StartsWith("ch", StringComparison.Ordinal) &&
            int.TryParse(value[2..], NumberStyles.Integer, CultureInfo.InvariantCulture, out int lag) &&
            lag >= 1 && lag <= LagCount)
        {
            port = new PortName("ch" + lag.ToString(CultureInfo.InvariantCulture), LagBaseIfIndex + lag - 1);
            return true;
        }

        if (value.StartsWith("g", StringComparison.Ordinal) &&
            int.TryParse(value[1..], NumberStyles.Integer, CultureInfo.InvariantCulture, out int index) &&
            index >= 1 && index <= PortCount)
        {
            port = new PortName("g" + index.ToString(CultureInfo.InvariantCulture), index);
            return true;
        }

        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int ifIndex) &&
            TryFromIfIndex(ifIndex, out port))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    ///     Maps an ifIndex back to a port name.
    /// </summary>
    public static bool TryFromIfIndex(int ifIndex, out PortName port)
    {
        if (ifIndex >= 1 && ifIndex <= PortCount)
        {
            port = new PortName("g" + ifIndex.ToString(CultureInfo.InvariantCulture), ifIndex);
            return true;
        }

        if (ifIndex >= LagBaseIfIndex && ifIndex < LagBaseIfIndex + LagCount)
        {
            int lag = ifIndex - LagBaseIfIndex + 1;
            port = new PortName("ch" + lag.ToString(CultureInfo.InvariantCulture), ifIndex);
            return true;
        }

        port = default;
        return false;
    }

    /// <summary>
    ///     Splits a comma or space separated port list.
    /// </summary>
    public static IReadOnlyList<PortName> ParseList(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Array.Empty<PortName>();
        }

        List<PortName> ports = new List<PortName>();
        string[] parts = text.Split(new[] { ',', ' ', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (string part in parts)
        {
            ports.Add(Parse(part));
        }

        return ports;
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return Name;
    }
}
