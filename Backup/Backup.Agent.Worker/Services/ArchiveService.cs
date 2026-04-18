using Backup.Agent.Worker.Interfaces;
using System.IO.Compression;

namespace Backup.Agent.Worker.Services;

public class ArchiveService : IArchiveService
{
    public async Task<string> CreateZipFromDirectoryAsync(string sourceDirectoryPath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        
        if (!Directory.Exists(sourceDirectoryPath))
        {
            throw new DirectoryNotFoundException(
                $"Directory '{sourceDirectoryPath}' was not found.");
        }
        
        string tempDirectory = Path.Combine(Path.GetTempPath(), "restorme");
        Directory.CreateDirectory(tempDirectory);
        
        string archiveFileName =
            $"{Path.GetFileName(sourceDirectoryPath)}_{DateTime.UtcNow:yyyyMMddHHmmss}.zip";
        
        string archivePath = Path.Combine(tempDirectory, archiveFileName);
        
        if (File.Exists(archivePath))
        {
            File.Delete(archivePath);
        }
        
        
        ZipFile.CreateFromDirectory(sourceDirectoryPath, archivePath, CompressionLevel.Optimal, includeBaseDirectory: true);

        return archivePath;
    }
    

}