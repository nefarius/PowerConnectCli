using System.Text.RegularExpressions;

using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;

namespace Nefarius.PowerConnect.Ews.Ews;

/// <summary>
///     A parsed EWS configuration page and the form fields the browser would submit.
/// </summary>
public sealed class FormPage
{
    private static readonly Regex RepeatName = new(
        @"^(?<base>.+?)\$repeat\d*\?(?<row>\d+)$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex RoleSuffix = new(
        @"\$(?<role>query|add|scalar|select|vt|bnd|default|help|endvt|endrepeat|endquery|endadd|current|querystatic|vtcurrent|vtn)$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly List<FormField> _fields;

    private FormPage(string action, List<FormField> fields)
    {
        Action = action;
        _fields = fields;
    }

    /// <summary>
    ///     Form action path.
    /// </summary>
    public string Action { get; }

    /// <summary>
    ///     Path that was requested, set by <see cref="Http.SwitchClient"/>.
    /// </summary>
    public string RequestPath { get; set; } = "";

    /// <summary>
    ///     Every named control, including disabled ones.
    /// </summary>
    public IReadOnlyList<FormField> Fields => _fields;

    /// <summary>
    ///     Parses an HTML document. AngleSharp reads the controls; names keep their original spelling.
    /// </summary>
    public static FormPage Parse(string html)
    {
        HtmlParser parser = new HtmlParser();
        IHtmlDocument document = parser.ParseDocument(html);
        IHtmlFormElement? form = document.QuerySelector("form") as IHtmlFormElement;
        string action = form?.GetAttribute("action") ?? "";
        List<FormField> fields = new List<FormField>();

        foreach (IElement element in document.QuerySelectorAll("input, select, textarea"))
        {
            string? name = element.GetAttribute("name");
            if (string.IsNullOrEmpty(name))
            {
                continue;
            }

            string tag = element.TagName.ToLowerInvariant();
            string inputType = tag == "input"
                ? (element.GetAttribute("type") ?? "text").ToLowerInvariant()
                : tag;
            bool disabled = element.HasAttribute("disabled");
            List<SelectOption> options = new List<SelectOption>();
            string value = element.GetAttribute("value") ?? "";
            bool isChecked = element.HasAttribute("checked");

            if (element is IHtmlSelectElement select)
            {
                bool anySelected = false;
                foreach (IHtmlOptionElement option in select.Options)
                {
                    bool selected = option.HasAttribute("selected") || option.IsSelected;
                    string optionValue = option.HasAttribute("value") ? option.Value : option.Text.Trim();
                    string optionText = option.Text.Trim();
                    if (selected)
                    {
                        anySelected = true;
                    }

                    options.Add(new SelectOption
                    {
                        Value = optionValue.Trim(),
                        Text = optionText,
                        Selected = selected
                    });
                }

                if (!anySelected && options.Count > 0)
                {
                    options[0] = new SelectOption
                    {
                        Value = options[0].Value,
                        Text = options[0].Text,
                        Selected = true
                    };
                }

                SelectOption? chosen = options.FirstOrDefault(option => option.Selected);
                value = chosen?.Value ?? "";
            }

            (string baseName, int? row, FieldRole role) = Classify(name);
            fields.Add(new FormField
            {
                Name = name,
                Value = value.Trim(),
                Disabled = disabled,
                InputType = inputType,
                Checked = isChecked || string.Equals(inputType, "radio", StringComparison.Ordinal) && isChecked,
                Options = options,
                BaseName = baseName,
                RepeatRow = row,
                Role = role
            });
        }

        return new FormPage(action, fields);
    }

    /// <summary>
    ///     First field with this name, ignoring case.
    /// </summary>
    public FormField? Find(string name)
    {
        foreach (FormField field in _fields)
        {
            if (string.Equals(field.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return field;
            }
        }

        return null;
    }

    /// <summary>
    ///     Value of the named field, or null when it is absent.
    /// </summary>
    public string? ValueOf(string name)
    {
        return Find(name)?.Value;
    }

    /// <summary>
    ///     Value of <c>base$query</c>.
    /// </summary>
    public string? Query(string baseName)
    {
        return ValueOf(baseName + "$query");
    }

    /// <summary>
    ///     Rows grouped by <c>$repeat?N</c> / <c>$repeat11?N</c>.
    /// </summary>
    public IReadOnlyList<FormRow> Rows()
    {
        Dictionary<int, Dictionary<string, FormField>> rows = new Dictionary<int, Dictionary<string, FormField>>();
        foreach (FormField field in _fields)
        {
            if (field.RepeatRow is not int row)
            {
                continue;
            }

            if (!rows.TryGetValue(row, out Dictionary<string, FormField>? map))
            {
                map = new Dictionary<string, FormField>(StringComparer.OrdinalIgnoreCase);
                rows[row] = map;
            }

            map[field.BaseName] = field;
        }

        return rows
            .OrderBy(pair => pair.Key)
            .Select(pair => new FormRow(pair.Key, pair.Value))
            .ToList();
    }

    /// <summary>
    ///     Metadata for a column, from its <c>$VT</c> or <c>$bnd</c> field.
    /// </summary>
    public ValueTypeMetadata? Metadata(string baseName)
    {
        FormField? field = Find(baseName + "$VT") ?? Find(baseName + "$bnd");
        return ValueTypeMetadata.Parse(field?.Value);
    }

    /// <summary>
    ///     Visible choices of a select, by control name.
    /// </summary>
    public IReadOnlyList<SelectOption> Options(string name)
    {
        return Find(name)?.Options ?? Array.Empty<SelectOption>();
    }

    /// <summary>
    ///     Builds the POST body the way the browser does: every enabled control, plus any mutated field
    ///     even when it was disabled. Checkboxes and radios are included only when checked, unless mutated.
    /// </summary>
    public IReadOnlyList<KeyValuePair<string, string>> BuildSubmission(
        IReadOnlyDictionary<string, string>? mutations,
        IReadOnlyCollection<string>? omit = null)
    {
        Dictionary<string, string> changes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (mutations != null)
        {
            foreach (KeyValuePair<string, string> pair in mutations)
            {
                changes[pair.Key] = pair.Value;
            }
        }

        List<KeyValuePair<string, string>> body = new List<KeyValuePair<string, string>>();
        HashSet<string> emitted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (IGrouping<string, FormField> group in _fields.GroupBy(field => field.Name, StringComparer.OrdinalIgnoreCase))
        {
            string name = group.First().Name;
            if (omit != null && omit.Contains(name, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            if (changes.TryGetValue(name, out string? mutated))
            {
                body.Add(new KeyValuePair<string, string>(name, mutated));
                emitted.Add(name);
                continue;
            }

            foreach (FormField field in group)
            {
                if (field.Disabled)
                {
                    continue;
                }

                if (IsCheckable(field) && !field.Checked)
                {
                    continue;
                }

                body.Add(new KeyValuePair<string, string>(field.Name, field.Value));
            }

            emitted.Add(name);
        }

        foreach (KeyValuePair<string, string> pair in changes)
        {
            if (!emitted.Contains(pair.Key))
            {
                body.Add(pair);
            }
        }

        return body;
    }

    /// <summary>
    ///     True when the page is the GoAhead "Document Error" response or carries a filled <c>errorCollector</c>.
    /// </summary>
    public string? ErrorMessage()
    {
        FormField? collector = Find("errorCollector") ?? Find("statRmonErrorCollector");
        if (collector != null && !string.IsNullOrWhiteSpace(collector.Value))
        {
            return collector.Value.Trim();
        }

        return null;
    }

    /// <summary>
    ///     Throws when the HTML is a Document Error page or the parsed form reports an error.
    /// </summary>
    public static void EnsureNoDocumentError(string html)
    {
        if (html.Contains("Document Error", StringComparison.OrdinalIgnoreCase))
        {
            throw new Nefarius.PowerConnect.Ews.Http.SwitchException("The switch returned a Document Error page.");
        }
    }

    private static bool IsCheckable(FormField field)
    {
        return field.InputType is "checkbox" or "radio";
    }

    private static (string BaseName, int? Row, FieldRole Role) Classify(string name)
    {
        Match repeat = RepeatName.Match(name);
        if (repeat.Success)
        {
            return (repeat.Groups["base"].Value, int.Parse(repeat.Groups["row"].Value, System.Globalization.CultureInfo.InvariantCulture), FieldRole.Repeat);
        }

        Match suffix = RoleSuffix.Match(name);
        if (!suffix.Success)
        {
            return (name, null, FieldRole.Plain);
        }

        string role = suffix.Groups["role"].Value.ToLowerInvariant();
        string baseName = name[..suffix.Index];
        FieldRole kind = role switch
        {
            "query" => FieldRole.Query,
            "add" => FieldRole.Add,
            "scalar" => FieldRole.Scalar,
            "select" => FieldRole.Select,
            "vt" or "bnd" or "vtcurrent" or "vtn" => FieldRole.Metadata,
            _ => FieldRole.Marker
        };
        return (baseName, null, kind);
    }
}

/// <summary>
///     One <c>$repeat</c> row, keyed by column base name.
/// </summary>
public sealed class FormRow
{
    /// <summary>
    ///     Creates a row.
    /// </summary>
    public FormRow(int index, IReadOnlyDictionary<string, FormField> fields)
    {
        Index = index;
        Fields = fields;
    }

    /// <summary>1-based row number from the field name.</summary>
    public int Index { get; }

    /// <summary>Columns keyed by base name.</summary>
    public IReadOnlyDictionary<string, FormField> Fields { get; }

    /// <summary>Value of a column, or null.</summary>
    public string? Value(string baseName)
    {
        return Fields.TryGetValue(baseName, out FormField? field) ? field.Value : null;
    }
}
