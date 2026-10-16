using Neirijnck.Transfr.Core.Planning;

namespace Neirijnck.Transfr.Core.Tests.Planning;

public sealed class FieldSelectionTests
{
    private static readonly Guid Page = new("22222222-2222-4222-8222-222222222222");
    private static readonly Guid Other = new("44444444-4444-4444-8444-444444444444");

    private static readonly FieldChoice Title = new(FieldKind.Value, "title", null, null);
    private static readonly FieldChoice Name = new(FieldKind.Name, null, "en-US", null);

    [Fact]
    public void Resolve_KeepsTheFieldsOfANodePickedOnlyByField_MergedOverItsPicks()
    {
        var fields = FieldSelection.Resolve([(Page, [Title]), (Page, [Title, Name]), (Other, null)], new HashSet<Guid> { Other });

        Assert.Equal([Page], fields.Keys);
        Assert.Equal([Title, Name], fields[Page]);
    }

    [Fact]
    public void Resolve_LetsTheWholeNodeWin_WhenTheSelectionAlsoHoldsItWhole()
    {
        // Picked whole, or below a node picked with everything below it: either way it is in the whole set.
        var fields = FieldSelection.Resolve([(Page, [Title]), (Page, null)], new HashSet<Guid> { Page });

        Assert.Empty(fields);
    }
}