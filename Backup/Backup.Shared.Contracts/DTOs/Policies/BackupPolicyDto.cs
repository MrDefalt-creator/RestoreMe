using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Backup.Shared.Contracts.DTOs.Policies;

public record BackupPolicyDto(
    [Required] Guid Id,
    [Required] string Type,
    [Required] string Name,
    [Required] string SourcePath,
    [Required] bool IsEnabled,
    // Deployed agents deserialize the historical "nexRunAt" field name;
    // the alias keeps the wire format stable across the C# rename.
    [property: JsonPropertyName("nexRunAt")] [Required] DateTime NextRunAt,
    BackupPolicyDatabaseSettingsDto? DatabaseSettings
    );
