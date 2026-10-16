using Neirijnck.Transfr.Core.Content;
using Neirijnck.Transfr.Core.References;

namespace Neirijnck.Transfr.Core.Planning;

/// <summary>
/// One field picked for a field-level copy of a node.
/// </summary>
/// <param name="Kind">The name or a property value.</param>
/// <param name="Alias">For a value: the property alias.</param>
/// <param name="Culture">The culture (null = invariant).</param>
/// <param name="Segment">The segment (null = none).</param>
public sealed record FieldChoice(FieldKind Kind, string? Alias, string? Culture, string? Segment)
{
    /// <summary>Reads a field as a request names it: a value needs an alias, a name has none.</summary>
    /// <returns>The field, or null with the reason it can't be read.</returns>
    public static (FieldChoice? Field, string? Error) Read(string? kind, string? alias, string? culture, string? segment)
    {
        if (!Enum.TryParse<FieldKind>(kind, ignoreCase: false, out var fieldKind) || !Enum.IsDefined(fieldKind))
        {
            return (null, $"Unknown field kind '{kind}'.");
        }

        return (fieldKind, string.IsNullOrWhiteSpace(alias)) switch
        {
            (FieldKind.Value, true) => (null, "A value field needs a property alias."),
            (FieldKind.Name, false) => (null, $"A name field takes no property alias ('{alias}')."),
            _ => (new FieldChoice(fieldKind, fieldKind == FieldKind.Value ? alias : null, culture, segment), null),
        };
    }

    /// <summary>Whether a difference between the two sides is in this field.</summary>
    public bool Covers(ContentDifference difference)
    {
        ArgumentNullException.ThrowIfNull(difference);
        return difference.Kind switch
        {
            ContentDifferenceKind.Name => this.CoversName(difference.Culture, difference.Segment),
            ContentDifferenceKind.Value => this.CoversValue(difference.Alias, difference.Culture, difference.Segment),
            _ => false,
        };
    }

    /// <summary>Whether a reference sits in this field (a reference inside a block sits in the property holding it).</summary>
    public bool Covers(ContentReference reference)
    {
        ArgumentNullException.ThrowIfNull(reference);
        return this.CoversValue(reference.PropertyAlias, reference.Culture, reference.Segment);
    }

    /// <summary>Whether this is the name of that variant.</summary>
    public bool CoversName(string? culture, string? segment) => this.Kind == FieldKind.Name && this.SameVariant(culture, segment);

    /// <summary>Whether this is that property value.</summary>
    public bool CoversValue(string? alias, string? culture, string? segment)
        => this.Kind == FieldKind.Value && string.Equals(this.Alias, alias, StringComparison.Ordinal) && this.SameVariant(culture, segment);

    // Culture names come back from Umbraco in one casing, but a picked field travels through the browser: compare them
    // the way cultures compare.
    private bool SameVariant(string? culture, string? segment)
        => string.Equals(this.Culture, culture, StringComparison.OrdinalIgnoreCase)
            && string.Equals(this.Segment, segment, StringComparison.OrdinalIgnoreCase);
}