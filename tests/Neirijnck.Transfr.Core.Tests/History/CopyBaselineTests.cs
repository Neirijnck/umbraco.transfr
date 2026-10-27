using Neirijnck.Transfr.Core.History;

namespace Neirijnck.Transfr.Core.Tests.History;

public sealed class CopyBaselineTests
{
    private static readonly DateTimeOffset Remote = new(2026, 10, 27, 10, 15, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Local = new(2026, 10, 27, 9, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(CopyDirection.ToRemote)]
    [InlineData(CopyDirection.FromRemote)]
    public void TargetUpdate_AfterACopyTheSameWay_IsWhatThatCopyLeftOnItsTarget(CopyDirection direction)
    {
        var latest = new RecordedCopy(direction, TargetUpdate: Remote, SourceUpdate: Local);

        Assert.Equal(Remote, CopyBaseline.TargetUpdate(latest, direction));
    }

    [Fact]
    public void TargetUpdate_PullingAfterAPush_IsThePushSourceHere()
    {
        var push = new RecordedCopy(CopyDirection.ToRemote, TargetUpdate: Remote, SourceUpdate: Local);

        Assert.Equal(Local, CopyBaseline.TargetUpdate(push, CopyDirection.FromRemote));
    }

    [Fact]
    public void TargetUpdate_PushingAfterAPull_IsThePullSourceOnTheRemote()
    {
        var pull = new RecordedCopy(CopyDirection.FromRemote, TargetUpdate: Local, SourceUpdate: Remote);

        Assert.Equal(Remote, CopyBaseline.TargetUpdate(pull, CopyDirection.ToRemote));
    }

    [Fact]
    public void TargetUpdate_TheOtherWayAfterACopyWithoutItsSource_IsUnknown()
    {
        // Rows recorded before both sides were kept: no baseline, so a difference is a conflict.
        var oldPush = new RecordedCopy(CopyDirection.ToRemote, TargetUpdate: Remote, SourceUpdate: null);

        Assert.Null(CopyBaseline.TargetUpdate(oldPush, CopyDirection.FromRemote));
        Assert.Equal(Remote, CopyBaseline.TargetUpdate(oldPush, CopyDirection.ToRemote));
    }
}