using Backup.Server.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Backup.Server.Infrastructure.Configuration;

public class BackupPolicyConfiguration : IEntityTypeConfiguration<BackupPolicy>
{
    public void Configure(EntityTypeBuilder<BackupPolicy> builder)
    {
        builder.ToTable("BackupPolicies");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(x => x.SourcePath)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(x => x.IsEnabled)
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.HasMany(x => x.Jobs)
            .WithOne(x => x.Policy)
            .HasForeignKey(x => x.PolicyId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}