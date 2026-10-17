using System.Globalization;
using System.Text.RegularExpressions;

namespace Nefarius.PowerConnect.Ews.Ews;

/// <summary>
///     Parsed <c>$VT</c> / <c>$bnd</c> type metadata from an EWS form.
/// </summary>
public sealed class ValueTypeMetadata
{
    private static readonly Regex Pair = new(
        @"(?<key>[A-Za-z0-9]+)=(?<value>[^;]*)",
        RegexOptions.Compiled);

    /// <summary>
    ///     Raw metadata string.
    /// </summary>
    public string Raw { get; init; } = "";

    /// <summary>
    ///     EWS type code. <c>101</c> is a PortList bitmap.
    /// </summary>
    public int? Type { get; init; }

    /// <summary>
    ///     Access code. <c>1</c> is read-only, <c>2</c> is read-write.
    /// </summary>
    public int? Access { get; init; }

    /// <summary>
    ///     Inclusive numeric range when the metadata carries <c>Range0</c>.
    /// </summary>
    public (long Min, long Max)? Range { get; init; }

    /// <summary>
    ///     Parses a metadata value. Returns null when the text is not metadata.
    /// </summary>
    public static ValueTypeMetadata? Parse(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw) || !raw.Contains("Type=", StringComparison.Ordinal))
        {
            return null;
        }

        Dictionary<string, string> pairs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in Pair.Matches(raw))
        {
            pairs[match.Groups["key"].Value] = match.Groups["value"].Value;
        }

        (long Min, long Max)? range = null;
        if (pairs.TryGetValue("Range0", out string? rangeText))
        {
            Match rangeMatch = Regex.Match(rangeText, @"\[(?<min>-?\d+),(?<max>-?\d+)\]");
            if (rangeMatch.Success)
            {
                range = (
                    long.Parse(rangeMatch.Groups["min"].Value, CultureInfo.InvariantCulture),
                    long.Parse(rangeMatch.Groups["max"].Value, CultureInfo.InvariantCulture));
            }
        }

        return new ValueTypeMetadata
        {
            Raw = raw,
            Type = pairs.TryGetValue("Type", out string? typeText) &&
                   int.TryParse(typeText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int type)
                ? type
                : null,
            Access = pairs.TryGetValue("Access", out string? accessText) &&
                     int.TryParse(accessText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int access)
                ? access
                : null,
            Range = range
        };
    }
}
