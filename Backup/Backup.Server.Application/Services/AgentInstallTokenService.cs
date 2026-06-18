using System.Security.Cryptography;
using Backup.Server.Application.Interfaces;
using Backup.Server.Domain.Entities;

namespace Backup.Server.Application.Services;

public sealed class GeneratedInstallToken
{
    public required string Token { get; init; }
    public required AgentInstallToken Record { get; init; }
}

public class AgentInstallTokenService
{
    // 32 bytes = 256 bits of entropy. Brute-forcing a single-use token is
    // not realistic at this size.
    private const int TokenBytes = 32;

    // Default TTL the admin's wizard requests. Hard cap is enforced at the
    // controller layer so a compromised caller can't ask for a year-long
    // token.
    public static readonly TimeSpan DefaultTtl = TimeSpan.FromMinutes(15);
    public static readonly TimeSpan MaxTtl = TimeSpan.FromMinutes(60);

    // Unused expired rows are kept this long before the cleanup service
    // removes them — gives ops a short window to see "token N expired
    // without being used" in audit dumps.
    public static readonly TimeSpan ExpiredGrace = TimeSpan.FromHours(1);

    private readonly IAgentInstallTokenRepository _repository;

    public AgentInstallTokenService(IAgentInstallTokenRepository repository)
    {
        _repository = repository;
    }

    public async Task<GeneratedInstallToken> GenerateAsync(
        Guid createdByUserId,
        string? preApprovedName,
        TimeSpan ttl,
        CancellationToken cancellationToken)
    {
        var bounded = ttl <= TimeSpan.Zero
            ? DefaultTtl
            : (ttl > MaxTtl ? MaxTtl : ttl);

        var bytes = new byte[TokenBytes];
        RandomNumberGenerator.Fill(bytes);
        var tokenString = Base64UrlEncode(bytes);
        var hash = SHA256.HashData(bytes);

        var record = new AgentInstallToken
        {
            Id = Guid.NewGuid(),
            TokenHash = hash,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.Add(bounded),
            CreatedByUserId = createdByUserId,
            PreApprovedName = string.IsNullOrWhiteSpace(preApprovedName) ? null : preApprovedName.Trim(),
        };

        await _repository.AddAsync(record, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        return new GeneratedInstallToken { Token = tokenString, Record = record };
    }

    public async Task<AgentInstallToken?> TryConsumeAsync(
        string presentedToken,
        string machineName,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(presentedToken))
        {
            return null;
        }

        byte[] tokenBytes;
        try
        {
            tokenBytes = Base64UrlDecode(presentedToken);
        }
        catch (FormatException)
        {
            // Token is not base64-url — it might be the legacy shared
            // enrollment token. Caller will fall back to that path.
            return null;
        }

        if (tokenBytes.Length != TokenBytes)
        {
            return null;
        }

        var hash = SHA256.HashData(tokenBytes);
        var record = await _repository.FindUsableByHashAsync(hash, cancellationToken);
        if (record is null)
        {
            return null;
        }

        // Atomic-ish consume — single SaveChanges. Concurrent requests with
        // the same token will race on SaveChanges; the unique TokenHash
        // index + DbUpdateConcurrencyException would catch it. For a
        // single-use admin-generated token, contention is not realistic.
        record.UsedAt = DateTime.UtcNow;
        record.UsedByMachineName = machineName;
        await _repository.SaveChangesAsync(cancellationToken);

        return record;
    }

    public Task<int> CleanupExpiredAsync(CancellationToken cancellationToken)
    {
        var cutoff = DateTime.UtcNow.Subtract(ExpiredGrace);
        return _repository.DeleteExpiredUnusedAsync(cutoff, cancellationToken);
    }

    private static string Base64UrlEncode(byte[] data)
    {
        return Convert.ToBase64String(data)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static byte[] Base64UrlDecode(string text)
    {
        var padded = text.Replace('-', '+').Replace('_', '/');
        switch (padded.Length % 4)
        {
            case 2: padded += "=="; break;
            case 3: padded += "="; break;
        }
        return Convert.FromBase64String(padded);
    }
}
