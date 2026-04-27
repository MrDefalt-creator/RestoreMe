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

        builder.Property(x => x.Password)
            .HasMaxLength(500);

        builder.HasIndex(x => new { x.Engine, x.DatabaseName });

        builder.HasOne(x => x.Policy)
            .WithOne(x => x.DatabaseSettings)
            .HasForeignKey<BackupPolicyDatabaseSettings>(x => x.PolicyId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
