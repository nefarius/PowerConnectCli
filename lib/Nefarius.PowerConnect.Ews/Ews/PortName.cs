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
    ///     Splits a comma, space, or semicolon separated list. Each item is one port or an inclusive range
    ///     such as <c>g1-g8</c>, <c>g1-8</c>, or <c>ch1-ch3</c>. Duplicates are dropped and the first
    ///     occurrence keeps its place.
    /// </summary>
    public static IReadOnlyList<PortName> ParseList(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Array.Empty<PortName>();
        }

        List<PortName> ports = new List<PortName>();
        HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string[] parts = text.Split(new[] { ',', ' ', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (string part in parts)
        {
            foreach (PortName port in Expand(part))
            {
                if (seen.Add(port.Name))
                {
                    ports.Add(port);
                }
            }
        }

        return ports;
    }

    /// <summary>
    ///     Parses a required port, LAG, range, or list. An empty value is an error.
    /// </summary>
    public static IReadOnlyList<PortName> ParseSelection(string? text)
    {
        IReadOnlyList<PortName> ports = ParseList(text);
        if (ports.Count == 0)
        {
            throw new FormatException("A port, LAG, range, or list is required (g1-g8, g1-g4,g6, ch1-ch2).");
        }

        return ports;
    }

    /// <summary>
    ///     Parses a required LAG, range, or list. A physical port in the selection is an error.
    /// </summary>
    public static IReadOnlyList<PortName> ParseLagSelection(string? text)
    {
        IReadOnlyList<PortName> ports = ParseSelection(text);
        foreach (PortName port in ports)
        {
            if (!port.IsLag)
            {
                throw new FormatException("'" + port.Name + "' is not a LAG (ch1-ch" + LagCount.ToString(CultureInfo.InvariantCulture) + ").");
            }
        }

        return ports;
    }

    private static IEnumerable<PortName> Expand(string part)
    {
        int dash = part.IndexOf('-');
        if (dash < 0)
        {
            yield return Parse(part);
            yield break;
        }

        if (dash == 0 || part.IndexOf('-', dash + 1) >= 0)
        {
            throw new FormatException($"'{part}' is not a port range. Use g1-g8 or ch1-ch3.");
        }

        string left = part[..dash];
        string right = part[(dash + 1)..];
        if (IsBareIndex(left))
        {
            throw new FormatException($"'{part}' is not a port range. Use g1-g8 or ch1-ch3.");
        }

        if (!TryParse(left, out PortName start))
        {
            throw new FormatException($"'{part}' is not a port range. Use g1-g8 or ch1-ch3.");
        }

        PortName end = EndOf(start, right, part);
        if (start.IsLag != end.IsLag)
        {
            throw new FormatException($"'{part}' mixes ports and LAGs.");
        }

        int from = Ordinal(start);
        int to = Ordinal(end);
        if (to < from)
        {
            throw new FormatException($"'{part}' is reversed. Put the lower port first.");
        }

        string prefix = start.IsLag ? "ch" : "g";
        for (int index = from; index <= to; index++)
        {
            yield return Parse(prefix + index.ToString(CultureInfo.InvariantCulture));
        }
    }

    private static PortName EndOf(PortName start, string right, string part)
    {
        if (IsBareIndex(right))
        {
            string prefix = start.IsLag ? "ch" : "g";
            if (!TryParse(prefix + right, out PortName sameKind))
            {
                throw new FormatException($"'{part}' is outside g1-g{PortCount.ToString(CultureInfo.InvariantCulture)} or ch1-ch{LagCount.ToString(CultureInfo.InvariantCulture)}.");
            }

            return sameKind;
        }

        if (!TryParse(right, out PortName end))
        {
            throw new FormatException($"'{part}' is outside g1-g{PortCount.ToString(CultureInfo.InvariantCulture)} or ch1-ch{LagCount.ToString(CultureInfo.InvariantCulture)}.");
        }

        return end;
    }

    private static bool IsBareIndex(string text)
    {
        return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out _);
    }

    private static int Ordinal(PortName port)
    {
        return port.IsLag ? port.IfIndex - LagBaseIfIndex + 1 : port.IfIndex;
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return Name;
    }
}
