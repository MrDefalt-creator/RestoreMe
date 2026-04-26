namespace Backup.Agent.Worker.Interfaces;

public interface IArchiveService
{
    Task<string> CreateZipFromDirectoryAsync(string sourceDirectoryPath, CancellationToken cancellationToken);
}