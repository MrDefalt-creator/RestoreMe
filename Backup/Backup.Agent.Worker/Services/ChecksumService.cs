using Backup.Agent.Worker.Interfaces;
using System.Security.Cryptography;

namespace Backup.Agent.Worker.Services;

public class ChecksumService : IChecksumService
{
    public async Task<string> ComputeSha256Async(string filePath, CancellationToken cancellationToken)
    {
        using var sha256 = SHA256.Create();
        await using var stream = File.OpenRead(filePath);
        
        var hash = await sha256.ComputeHashAsync(stream);
        
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}