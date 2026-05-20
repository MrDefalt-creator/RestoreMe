using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Backup.Server.Infrastructure.Persistence;

// Encrypts a string at rest via ASP.NET Core DataProtection. Reads that
// fail to Unprotect (typically: legacy plaintext rows from before the
// converter was introduced) fall through to the cipher text as-is so
// existing data keeps round-tripping. The next save re-stores the value
// encrypted, so the column self-heals as policies are touched.
public sealed class EncryptedStringConverter : ValueConverter<string?, string?>
{
    public EncryptedStringConverter(IDataProtector protector)
        : base(
            plaintext => plaintext == null ? null : protector.Protect(plaintext),
            cipher => DecryptOrPassThrough(protector, cipher))
    {
    }

    private static string? DecryptOrPassThrough(IDataProtector protector, string? cipher)
    {
        if (cipher == null)
        {
            return null;
        }

        try
        {
            return protector.Unprotect(cipher);
        }
        catch (CryptographicException)
        {
            // Legacy plaintext row (or value protected with a different
            // purpose / key). Return as-is so the read doesn't blow up;
            // the next SaveChanges will re-encrypt it.
            return cipher;
        }
    }
}
