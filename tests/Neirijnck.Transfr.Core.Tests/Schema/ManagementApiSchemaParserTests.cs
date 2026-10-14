using Neirijnck.Transfr.Core.Schema;

namespace Neirijnck.Transfr.Core.Tests.Schema;

public sealed class ManagementApiSchemaParserTests
{
    [Fact]
    public void ParseDocumentTypeSearch_ReadsKeysAndNames()
    {
        // The shape measured on an Umbraco 17.7.0 test site (item/document-type/search?query=transfr).
        const string json = """
            {"items":[{"isElement":true,"icon":"icon-picture","description":null,"name":"Transfr Image Block","id":"ab65b940-b08c-4cec-8cd3-9a201608564c","flags":[]},
                      {"isElement":false,"icon":"icon-document","description":null,"name":"Transfr Test Page","id":"e245cdaa-4176-4551-a6c4-ff3ead958d31","flags":[]}],
             "total":2}
            """;

        var found = ManagementApiSchemaParser.ParseDocumentTypeSearch(json);

        Assert.Equal(
            [(new Guid("ab65b940-b08c-4cec-8cd3-9a201608564c"), "Transfr Image Block"), (new Guid("e245cdaa-4176-4551-a6c4-ff3ead958d31"), "Transfr Test Page")],
            found);
    }

    [Fact]
    public void ParseAlias_ReadsADocumentTypesAlias()
    {
        var json = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "schema", "document-type-transfr-test-page.json"));

        Assert.Equal("transfrTestPage", ManagementApiSchemaParser.ParseAlias(json));
    }
}