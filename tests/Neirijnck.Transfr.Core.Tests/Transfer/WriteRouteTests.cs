using Neirijnck.Transfr.Core.Planning;
using Neirijnck.Transfr.Core.Transfer;

namespace Neirijnck.Transfr.Core.Tests.Transfer;

/// <summary>
/// The writes as <see cref="TransferRequests"/> and <see cref="MediaTransferRequests"/> build them read back to their
/// operation, so a copy to here carries out exactly what a copy to a remote would send.
/// </summary>
public sealed class WriteRouteTests
{
    private const string Api = "/umbraco/management/api/v1";

    private static readonly Guid Key = new("22222222-2222-4222-8222-222222222222");
    private static readonly Guid Parent = new("11111111-1111-4111-8111-111111111111");
    private static readonly Guid PageType = new("e2450d61-29a3-4c3d-9f6e-1b9b0b3c0a01");
    private static readonly Guid Red = new("45cebd44-9840-4a3f-a411-dfebacfb497d");

    private static string Fixture(string name) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));

    private static PlanRow Document(PlanOutcome outcome, params string?[] publish)
        => new(PlanItemKind.Document, Key, Parent, "Page", PageType, outcome, [], publish, [], []);

    private static PlanRow Media(PlanOutcome outcome)
        => new(PlanItemKind.Media, Red, Parent, "Red", null, outcome, [], [], [], []);

    private static WriteRoute? Route(DocumentWrite write) => WriteRoute.Parse(write.Method, write.Path);

    [Fact]
    public void Parse_ReadsEveryDocumentWriteTheBuildersMake()
    {
        var source = Fixture("document-features.json");

        Assert.Equal(new WriteRoute(WriteOperation.CreateDocument, null), Route(TransferRequests.For(Document(PlanOutcome.Create), source)));
        Assert.Equal(new WriteRoute(WriteOperation.CreateAndPublishDocument, null), Route(TransferRequests.For(Document(PlanOutcome.Create, (string?)null), source)));
        Assert.Equal(new WriteRoute(WriteOperation.UpdateDocument, Key), Route(TransferRequests.For(Document(PlanOutcome.Update), source)));
        Assert.Equal(new WriteRoute(WriteOperation.UpdateAndPublishDocument, Key), Route(TransferRequests.For(Document(PlanOutcome.Update, (string?)null), source)));
        Assert.Equal(new WriteRoute(WriteOperation.SortDocuments, null), Route(TransferRequests.Sort(Parent, [Key])));
    }

    [Fact]
    public void Parse_ReadsEveryMediaWriteTheBuildersMake()
    {
        var source = Fixture("media-image.json");

        Assert.Equal(new WriteRoute(WriteOperation.CreateMedia, null), Route(MediaTransferRequests.For(Media(PlanOutcome.Create), source, null, Guid.NewGuid).Write));
        Assert.Equal(new WriteRoute(WriteOperation.UpdateMedia, Red), Route(MediaTransferRequests.For(Media(PlanOutcome.Update), source, source, Guid.NewGuid).Write));
    }

    [Theory]
    [InlineData("DELETE", Api + "/document/22222222-2222-4222-8222-222222222222")]
    [InlineData("POST", Api + "/document/22222222-2222-4222-8222-222222222222")]
    [InlineData("PUT", Api + "/document/22222222-2222-4222-8222-222222222222/publish")]
    [InlineData("PUT", Api + "/document/22222222-2222-4222-8222-222222222222/unpublish")]
    [InlineData("PUT", Api + "/document/22222222-2222-4222-8222-222222222222/move")]
    [InlineData("PUT", Api + "/document/22222222-2222-4222-8222-222222222222/move-to-recycle-bin")]
    [InlineData("PUT", Api + "/document/root")]
    [InlineData("PUT", Api + "/document")]
    [InlineData("PUT", Api + "/media/sort")]
    [InlineData("DELETE", Api + "/media/45cebd44-9840-4a3f-a411-dfebacfb497d")]
    [InlineData("POST", Api + "/temporary-file")]
    [InlineData("POST", "/umbraco/management/api/v2/document")]
    [InlineData("POST", "/document")]
    [InlineData("post", Api + "/document")]
    public void Parse_RefusesEveryOtherRequest(string method, string path)
    {
        Assert.Null(WriteRoute.Parse(method, path));
    }
}