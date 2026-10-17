using System.Text;

namespace Nefarius.PowerConnect.Ews.Ews;

/// <summary>
///     Builds PowerConnect page URLs. Selectors look like
///     <c>?[Table]Query:key=value@key2=value2[Other]Filter:expr</c>.
/// </summary>
public static class EwsUrl
{
    /// <summary>
    ///     A table selector appended to a page URL.
    /// </summary>
    public sealed class Selector
    {
        /// <summary>
        ///     Table token inside brackets. Empty when the page uses a bare <c>Query:</c> or <c>Filter:</c>.
        /// </summary>
        public string Table { get; init; } = "";

        /// <summary>
        ///     <c>Query:</c> pairs, joined with <c>@</c>.
        /// </summary>
        public IReadOnlyList<KeyValuePair<string, string>> Query { get; init; } =
            Array.Empty<KeyValuePair<string, string>>();

        /// <summary>
        ///     Raw <c>Filter:</c> expression.
        /// </summary>
        public string? Filter { get; init; }
    }

    /// <summary>
    ///     Returns <c>/config/page.htm</c> plus any selectors.
    /// </summary>
    public static string Config(string page, params Selector[] selectors)
    {
        string file = page.EndsWith(".htm", StringComparison.OrdinalIgnoreCase) ? page : page + ".htm";
        if (file.StartsWith("/config/", StringComparison.OrdinalIgnoreCase))
        {
            file = file["/config/".Length..];
        }

        if (selectors.Length == 0)
        {
            return "/config/" + file;
        }

        StringBuilder builder = new StringBuilder();
        builder.Append("/config/");
        builder.Append(file);
        builder.Append('?');
        for (int index = 0; index < selectors.Length; index++)
        {
            Selector selector = selectors[index];
            if (!string.IsNullOrEmpty(selector.Table))
            {
                builder.Append('[');
                builder.Append(selector.Table);
                builder.Append(']');
            }

            if (selector.Query.Count > 0)
            {
                builder.Append("Query:");
                for (int pairIndex = 0; pairIndex < selector.Query.Count; pairIndex++)
                {
                    if (pairIndex > 0)
                    {
                        builder.Append('@');
                    }

                    KeyValuePair<string, string> pair = selector.Query[pairIndex];
                    builder.Append(pair.Key);
                    builder.Append('=');
                    builder.Append(pair.Value);
                }
            }

            if (!string.IsNullOrEmpty(selector.Filter))
            {
                builder.Append("Filter:");
                builder.Append(selector.Filter);
            }
        }

        return builder.ToString();
    }

    /// <summary>
    ///     Shorthand for a single-key query selector.
    /// </summary>
    public static string Query(string page, string table, string key, string value)
    {
        return Config(page, new Selector
        {
            Table = table,
            Query = new[] { new KeyValuePair<string, string>(key, value) }
        });
    }
}
