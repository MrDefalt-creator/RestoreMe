using Backup.Agent.Worker.Services;

namespace Backup.Agent.Worker.Tests;

public sealed class RestoreVerifyTests
{
    [Fact]
    public void EmptyExpected_AllowsRestore() // legacy artifact, no checksum
        => Assert.True(RestoreChecksumGate.ShouldProceed(expected: null, computed: "anything"));

    [Fact]
    public void Match_AllowsRestore()
        => Assert.True(RestoreChecksumGate.ShouldProceed(expected: "ABC", computed: "abc"));

    [Fact]
    public void Mismatch_BlocksRestore()
        => Assert.False(RestoreChecksumGate.ShouldProceed(expected: "abc", computed: "deadbeef"));
}
