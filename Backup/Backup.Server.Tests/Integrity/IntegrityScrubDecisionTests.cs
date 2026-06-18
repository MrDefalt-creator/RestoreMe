using Backup.Server.Application.Services;

namespace Backup.Server.Tests.Integrity;

public sealed class IntegrityScrubDecisionTests
{
    [Fact]
    public void OverSizeCap_Skips()
    {
        var result = IntegrityScrubDecision.Evaluate(sizeBytes: 100, maxBytes: 50, expectedChecksum: "abc", computedChecksum: "abc");
        Assert.Equal(ScrubOutcome.Skipped, result);
    }

    [Fact]
    public void WithinCap_MatchingChecksum_Verifies()
    {
        var result = IntegrityScrubDecision.Evaluate(100, maxBytes: 200, expectedChecksum: "ABC", computedChecksum: "abc");
        Assert.Equal(ScrubOutcome.Verified, result);
    }

    [Fact]
    public void WithinCap_Mismatch_Fails()
    {
        var result = IntegrityScrubDecision.Evaluate(100, maxBytes: null, expectedChecksum: "abc", computedChecksum: "deadbeef");
        Assert.Equal(ScrubOutcome.Failed, result);
    }

    [Fact]
    public void NullComputed_Fails()
    {
        var result = IntegrityScrubDecision.Evaluate(100, maxBytes: null, expectedChecksum: "abc", computedChecksum: null);
        Assert.Equal(ScrubOutcome.Failed, result);
    }
}
