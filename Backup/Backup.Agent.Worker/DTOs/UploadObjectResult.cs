namespace Backup.Agent.Worker.DTOs;

public record UploadObjectResult(
    long Size,
    string Checksum
    );