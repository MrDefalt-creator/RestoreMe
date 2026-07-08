using System.Text.Json;
using Backup.Shared.Contracts.DTOs.Policies;

namespace Backup.Server.Tests.Policies;

/// <summary>
/// Pins the agent-facing wire format: deployed agents deserialize the
/// "nexRunAt" JSON field (historical name), so the property must keep
/// serializing under that exact name regardless of its C# name.
/// </summary>
public class AgentPolicyDtoWireFormatTests
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    private static BackupPolicyDto SampleDto() => new(
        Guid.Parse("11111111-2222-3333-4444-555555555555"),
        "filesystem",
        "docs",
        "/data/docs",
        true,
        new DateTime(2026, 7, 8, 12, 0, 0, DateTimeKind.Utc),
        null);

    [Fact]
    public void SerializesNextRunUnderHistoricalNexRunAtName()
    {
        var json = JsonSerializer.Serialize(SampleDto(), Web);

        Assert.Contains("\"nexRunAt\":", json);
        Assert.DoesNotContain("\"nextRunAt\":", json);
    }

    [Fact]
    public void DeserializesHistoricalNexRunAtName()
    {
        var json = JsonSerializer.Serialize(SampleDto(), Web);

        var roundTripped = JsonSerializer.Deserialize<BackupPolicyDto>(json, Web);

        Assert.NotNull(roundTripped);
        Assert.Equal(SampleDto(), roundTripped);
    }
}
