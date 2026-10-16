using Neirijnck.Transfr.Core.Planning;

namespace Neirijnck.Transfr.Core.Tests.Planning;

public sealed class FieldChoiceTests
{
    [Fact]
    public void Read_TakesAValueWithItsAlias_AndANameWithout()
    {
        Assert.Equal((new FieldChoice(FieldKind.Value, "title", "en-US", null), null), FieldChoice.Read("Value", "title", "en-US", null));
        Assert.Equal((new FieldChoice(FieldKind.Name, null, "nl-BE", null), null), FieldChoice.Read("Name", null, "nl-BE", null));
    }

    [Theory]
    [InlineData("Value", null)]
    [InlineData("Value", " ")]
    [InlineData("Name", "title")]
    [InlineData("State", null)]
    [InlineData("value", "title")]
    [InlineData("5", "title")]
    [InlineData(null, "title")]
    public void Read_RefusesAFieldItCantRead(string? kind, string? alias)
    {
        var (field, error) = FieldChoice.Read(kind, alias, null, null);

        Assert.Null(field);
        Assert.NotNull(error);
    }
}