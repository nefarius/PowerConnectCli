namespace Nefarius.PowerConnect.Ews.Ews;

/// <summary>
///     One input, select, or textarea on an EWS page.
/// </summary>
public sealed class FormField
{
    /// <summary>
    ///     Control name as it appears in the markup.
    /// </summary>
    public string Name { get; init; } = "";

    /// <summary>
    ///     Current value.
    /// </summary>
    public string Value { get; init; } = "";

    /// <summary>
    ///     True when the control carries a <c>disabled</c> attribute. Browsers omit those on submit.
    /// </summary>
    public bool Disabled { get; init; }

    /// <summary>
    ///     Input type, or <c>select</c> / <c>textarea</c>.
    /// </summary>
    public string InputType { get; init; } = "text";

    /// <summary>
    ///     True for a checked checkbox or radio button.
    /// </summary>
    public bool Checked { get; init; }

    /// <summary>
    ///     Options for a select, in document order.
    /// </summary>
    public IReadOnlyList<SelectOption> Options { get; init; } = Array.Empty<SelectOption>();

    /// <summary>
    ///     Name with the EWS suffix removed (<c>$query</c>, <c>$repeat?N</c>, and so on).
    /// </summary>
    public string BaseName { get; init; } = "";

    /// <summary>
    ///     Row number when the name is a <c>$repeat</c> field.
    /// </summary>
    public int? RepeatRow { get; init; }

    /// <summary>
    ///     How the name is suffixed.
    /// </summary>
    public FieldRole Role { get; init; } = FieldRole.Plain;
}

/// <summary>
///     An option inside a select.
/// </summary>
public sealed class SelectOption
{
    /// <summary>
    ///     Option value attribute. Falls back to the visible text when the attribute is missing.
    /// </summary>
    public string Value { get; init; } = "";

    /// <summary>
    ///     Visible text.
    /// </summary>
    public string Text { get; init; } = "";

    /// <summary>
    ///     True when this option is selected.
    /// </summary>
    public bool Selected { get; init; }
}

/// <summary>
///     EWS naming role of a field.
/// </summary>
public enum FieldRole
{
    /// <summary>No recognized suffix.</summary>
    Plain,

    /// <summary>Current row, <c>$query</c>.</summary>
    Query,

    /// <summary>New row, <c>$add</c>.</summary>
    Add,

    /// <summary>Table row, <c>$repeat?N</c> or <c>$repeat11?N</c>.</summary>
    Repeat,

    /// <summary>Global scalar, <c>$scalar</c>.</summary>
    Scalar,

    /// <summary>UI-only selector, <c>$select</c>.</summary>
    Select,

    /// <summary>Type metadata, <c>$VT</c> or <c>$bnd</c>.</summary>
    Metadata,

    /// <summary>Table marker such as <c>$endVT</c>.</summary>
    Marker
}
