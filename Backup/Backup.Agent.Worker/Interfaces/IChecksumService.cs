namespace Backup.Agent.Worker.Interfaces;

public interface IChecksumService
{
    Task<string> ComputeSha256Async(string filePath, CancellationToken cancellationToken);
}
