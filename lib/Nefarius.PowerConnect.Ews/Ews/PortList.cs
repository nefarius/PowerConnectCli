using System.Globalization;

namespace Nefarius.PowerConnect.Ews.Ews;

/// <summary>
///     Q-BRIDGE PortList hex bitmaps. Bit index is <c>ifIndex - 1</c>, MSB-first inside each octet.
///     <c>g1</c>..<c>g16</c> are ifIndex 1..16. <c>ch1</c>..<c>ch8</c> are ifIndex 1000..1007.
/// </summary>
public static class PortList
{
    /// <summary>
    ///     Hex length used by VLAN membership fields on a 2816 (126 bytes, covering ifIndex 1008).
    /// </summary>
    public const int DefaultHexLength = 252;

    /// <summary>
    ///     Decodes a hex bitmap into known ports and LAGs whose bits are set.
    /// </summary>
    public static IReadOnlyList<PortName> Decode(string? hex)
    {
        byte[] bytes = ParseHex(hex);
        List<PortName> ports = new List<PortName>();
        int bitCount = bytes.Length * 8;
        for (int bitIndex = 0; bitIndex < bitCount; bitIndex++)
        {
            if (!IsSet(bytes, bitIndex))
            {
                continue;
            }

            if (PortName.TryFromIfIndex(bitIndex + 1, out PortName port))
            {
                ports.Add(port);
            }
        }

        return ports;
    }

    /// <summary>
    ///     Encodes ports into a lowercase hex bitmap of <paramref name="hexLength"/> characters.
    /// </summary>
    public static string Encode(IEnumerable<PortName> ports, int hexLength = DefaultHexLength)
    {
        if (hexLength < 2 || hexLength % 2 != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(hexLength), "Hex length must be a positive even number.");
        }

        byte[] bytes = new byte[hexLength / 2];
        foreach (PortName port in ports)
        {
            Set(bytes, port.IfIndex - 1);
        }

        return ToHex(bytes);
    }

    /// <summary>
    ///     Encodes port names into a lowercase hex bitmap.
    /// </summary>
    public static string Encode(IEnumerable<string> names, int hexLength = DefaultHexLength)
    {
        return Encode(names.Select(PortName.Parse), hexLength);
    }

    /// <summary>
    ///     Returns a copy of <paramref name="existing"/> with the given ifIndexes added and removed.
    /// </summary>
    public static string Apply(string? existing, IEnumerable<int> add, IEnumerable<int> remove)
    {
        int hexLength = string.IsNullOrWhiteSpace(existing) ? DefaultHexLength : existing.Trim().Length;
        if (hexLength % 2 != 0)
        {
            hexLength++;
        }

        byte[] bytes = ParseHex(existing, hexLength / 2);
        foreach (int ifIndex in add)
        {
            Set(bytes, ifIndex - 1);
        }

        foreach (int ifIndex in remove)
        {
            Clear(bytes, ifIndex - 1);
        }

        return ToHex(bytes);
    }

    /// <summary>
    ///     Updates egress, untagged, and forbidden bitmaps for one VLAN.
    ///     Untagged membership is also egress. Forbidden membership is removed from egress.
    ///     <paramref name="none"/> clears the port from all three.
    /// </summary>
    public static (string Egress, string Untagged, string Forbidden) UpdateMembership(
        string? egress,
        string? untagged,
        string? forbidden,
        IEnumerable<PortName> tagged,
        IEnumerable<PortName> untaggedPorts,
        IEnumerable<PortName> forbiddenPorts,
        IEnumerable<PortName> none)
    {
        HashSet<int> egressSet = Decode(egress).Select(port => port.IfIndex).ToHashSet();
        HashSet<int> untaggedSet = Decode(untagged).Select(port => port.IfIndex).ToHashSet();
        HashSet<int> forbiddenSet = Decode(forbidden).Select(port => port.IfIndex).ToHashSet();

        foreach (PortName port in none)
        {
            egressSet.Remove(port.IfIndex);
            untaggedSet.Remove(port.IfIndex);
            forbiddenSet.Remove(port.IfIndex);
        }

        foreach (PortName port in tagged)
        {
            egressSet.Add(port.IfIndex);
            untaggedSet.Remove(port.IfIndex);
            forbiddenSet.Remove(port.IfIndex);
        }

        foreach (PortName port in untaggedPorts)
        {
            egressSet.Add(port.IfIndex);
            untaggedSet.Add(port.IfIndex);
            forbiddenSet.Remove(port.IfIndex);
        }

        foreach (PortName port in forbiddenPorts)
        {
            egressSet.Remove(port.IfIndex);
            untaggedSet.Remove(port.IfIndex);
            forbiddenSet.Add(port.IfIndex);
        }

        int length = HexLength(egress, untagged, forbidden);
        return (
            EncodeIndexes(egressSet, length),
            EncodeIndexes(untaggedSet, length),
            EncodeIndexes(forbiddenSet, length));
    }

    /// <summary>
    ///     Encodes raw ifIndexes, including ones that are not a named port.
    /// </summary>
    public static string EncodeIndexes(IEnumerable<int> ifIndexes, int hexLength = DefaultHexLength)
    {
        if (hexLength < 2 || hexLength % 2 != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(hexLength), "Hex length must be a positive even number.");
        }

        byte[] bytes = new byte[hexLength / 2];
        foreach (int ifIndex in ifIndexes)
        {
            Set(bytes, ifIndex - 1);
        }

        return ToHex(bytes);
    }

    private static int HexLength(params string?[] values)
    {
        foreach (string? value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                string trimmed = value.Trim();
                return trimmed.Length % 2 == 0 ? trimmed.Length : trimmed.Length + 1;
            }
        }

        return DefaultHexLength;
    }

    private static byte[] ParseHex(string? hex, int? minimumBytes = null)
    {
        string trimmed = (hex ?? "").Trim();
        if (trimmed.Length == 0)
        {
            return new byte[minimumBytes ?? DefaultHexLength / 2];
        }

        if (trimmed.Length % 2 != 0)
        {
            trimmed = "0" + trimmed;
        }

        byte[] bytes = Convert.FromHexString(trimmed);
        if (minimumBytes.HasValue && bytes.Length < minimumBytes.Value)
        {
            byte[] grown = new byte[minimumBytes.Value];
            Buffer.BlockCopy(bytes, 0, grown, 0, bytes.Length);
            return grown;
        }

        return bytes;
    }

    private static bool IsSet(byte[] bytes, int bitIndex)
    {
        int byteIndex = bitIndex / 8;
        if (byteIndex < 0 || byteIndex >= bytes.Length)
        {
            return false;
        }

        int bitInByte = bitIndex % 8;
        int mask = 0x80 >> bitInByte;
        return (bytes[byteIndex] & mask) != 0;
    }

    private static void Set(byte[] bytes, int bitIndex)
    {
        int byteIndex = bitIndex / 8;
        if (byteIndex < 0 || byteIndex >= bytes.Length)
        {
            return;
        }

        bytes[byteIndex] |= (byte)(0x80 >> (bitIndex % 8));
    }

    private static void Clear(byte[] bytes, int bitIndex)
    {
        int byteIndex = bitIndex / 8;
        if (byteIndex < 0 || byteIndex >= bytes.Length)
        {
            return;
        }

        bytes[byteIndex] &= (byte)~(0x80 >> (bitIndex % 8));
    }

    private static string ToHex(byte[] bytes)
    {
        return Convert.ToHexString(bytes).ToLower(CultureInfo.InvariantCulture);
    }
}
