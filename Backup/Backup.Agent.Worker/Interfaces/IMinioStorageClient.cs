using Backup.Agent.Worker.DTOs;

namespace Backup.Agent.Worker.Interfaces;

public interface IMinioStorageClient
{
    Task<UploadObjectResult> UploadFileAsync(
        string uploadUrl,
        string filePath,
        string contentType,
        CancellationToken cancellationToken);
}