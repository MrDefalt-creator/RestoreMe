using Backup.Server.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Backup.Server.Infrastructure.Configuration;

public class BackupArtifactConfiguration : IEntityTypeConfiguration<BackupArtifact>
{
    public void Configure(EntityTypeBuilder<BackupArtifact> builder)
    {
        builder.ToTable("BackupArtifacts");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.ObjectKey)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(x => x.FileName)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(x => x.SizeBytes)
            .IsRequired();

        builder.Property(x => x.Checksum)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(x => x.CreatedAt)
            .IsRequired();
    }
}