using Neirijnck.Transfr.Core.Planning;
using Neirijnck.Transfr.Core.Transfer;

namespace Neirijnck.Transfr.Core.Tests.Transfer;

public sealed class PlanCheckTests
{
    private static readonly Guid A = new("aaaaaaaa-0000-4000-8000-000000000001");
    private static readonly Guid B = new("bbbbbbbb-0000-4000-8000-000000000002");
    private static readonly Guid M = new("cccccccc-0000-4000-8000-000000000003");

    private static PlanRow Document(Guid key, PlanOutcome outcome, string fingerprint)
        => new(PlanItemKind.Document, key, null, key == A ? "A" : "B", null, outcome, [], [], [], [], fingerprint);

    [Fact]
    public void Compare_FindsNothingWhenThePlanIsAsTheDryRunShowedIt()
    {
        var plan = new[] { Document(A, PlanOutcome.Create, "1|absent"), Document(B, PlanOutcome.Unchanged, "2|2") };

        Assert.Empty(PlanCheck.Compare(plan, [new(A, PlanOutcome.Create, "1|absent"), new(B, PlanOutcome.Unchanged, "2|2")]));
    }

    [Fact]
    public void Compare_RefusesADocumentSavedOnEitherSideSinceTheDryRun()
    {
        var drift = PlanCheck.Compare([Document(A, PlanOutcome.Update, "1|3")], [new(A, PlanOutcome.Update, "1|2")]);

        Assert.Equal([new PlanDrift(A, "A", PlanDriftReason.Saved, PlanOutcome.Update, PlanOutcome.Update)], drift);
    }

    [Fact]
    public void Compare_RefusesAnOutcomeThatChanged_EvenWithTheSameFingerprint()
    {
        var drift = PlanCheck.Compare([Document(A, PlanOutcome.Blocked, "1|absent")], [new(A, PlanOutcome.Create, "1|absent")]);

        Assert.Equal([new PlanDrift(A, "A", PlanDriftReason.OutcomeChanged, PlanOutcome.Create, PlanOutcome.Blocked)], drift);
    }

    [Fact]
    public void Compare_RefusesDocumentsAddedToOrGoneFromThePlan()
    {
        var drift = PlanCheck.Compare([Document(A, PlanOutcome.Create, "1|absent")], [new(B, PlanOutcome.Create, "2|absent")]);

        Assert.Equal(
            [
                new PlanDrift(A, "A", PlanDriftReason.NotInDryRun, null, PlanOutcome.Create),
                new PlanDrift(B, null, PlanDriftReason.NoLongerPlanned, PlanOutcome.Create, null),
            ],
            drift);
    }

    [Fact]
    public void Compare_ChecksMediaRowsLikeDocuments()
    {
        // Media are written too, so a media item saved on either side since the dry run refuses the transfer.
        var media = new PlanRow(PlanItemKind.Media, M, null, "Blue", null, PlanOutcome.Update, [], [], [], [], "2|3");

        Assert.Empty(PlanCheck.Compare([media], [new(M, PlanOutcome.Update, "2|3")]));
        Assert.Equal([PlanDriftReason.Saved], PlanCheck.Compare([media], [new(M, PlanOutcome.Update, "2|1")]).Select(drift => drift.Reason));
        Assert.Equal([PlanDriftReason.NotInDryRun], PlanCheck.Compare([media], []).Select(drift => drift.Reason));
    }
}