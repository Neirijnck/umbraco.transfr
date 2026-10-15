using Neirijnck.Transfr.Core.Transfer;

namespace Neirijnck.Transfr.Core.Tests.Transfer;

/// <summary>
/// Runs against <c>GET document/{id}</c> responses captured from an Umbraco 17.7.0 test site (Fixtures/document-*.json).
/// </summary>
public sealed class DocumentStampTests
{
    private static string Fixture(string name) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));

    [Fact]
    public void LastUpdate_IsTheLatestSaveOfAnyVariant()
    {
        const string json = """
            {"variants":[{"culture":"en-US","updateDate":"2026-10-13T16:44:34.0418191+02:00"},
                         {"culture":"nl-BE","updateDate":"2026-10-14T09:00:00.5000000+00:00"}]}
            """;

        Assert.Equal(DateTimeOffset.Parse("2026-10-14T09:00:00.5+00:00", System.Globalization.CultureInfo.InvariantCulture), DocumentStamp.LastUpdate(json));
    }

    [Fact]
    public void States_NamesTheCultureOnlyForVariantContent()
    {
        Assert.Equal("en-US Published, nl-BE Published", DocumentStamp.States(Fixture("document-variant-page.json")));
        Assert.Equal("Draft", DocumentStamp.States(Fixture("document-draft-page.json")));
    }
}