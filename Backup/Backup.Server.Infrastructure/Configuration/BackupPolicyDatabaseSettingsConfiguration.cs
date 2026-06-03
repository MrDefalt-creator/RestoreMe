using Backup.Server.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Backup.Server.Infrastructure.Configuration;

public class BackupPolicyDatabaseSettingsConfiguration : IEntityTypeConfiguration<BackupPolicyDatabaseSettings>
{
    public void Configure(EntityTypeBuilder<BackupPolicyDatabaseSettings> builder)
    {
        builder.ToTable("BackupPolicyDatabaseSettings");

        builder.HasKey(x => x.PolicyId);

        builder.Property(x => x.DatabaseName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.Host)
            .HasMaxLength(255);

        builder.Property(x => x.Username)
            .HasMaxLength(150);

        // Password is encrypted at rest via the EncryptedStringConverter, so
        // the column holds ciphertext (larger than the plaintext after the
        // DataProtection header + base64 expansion). A char cap here is
        // enforced against the ciphertext and would reject otherwise-valid
        // passwords, so leave the column unbounded (Postgres `text`) and let
        // input validation bound the plaintext.
        builder.Property(x => x.Password);

        builder.HasIndex(x => new { x.Engine, x.DatabaseName });

        builder.HasOne(x => x.Policy)
            .WithOne(x => x.DatabaseSettings)
            .HasForeignKey<BackupPolicyDatabaseSettings>(x => x.PolicyId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
