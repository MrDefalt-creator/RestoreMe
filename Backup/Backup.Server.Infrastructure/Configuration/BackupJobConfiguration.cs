using Backup.Server.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Backup.Server.Infrastructure.Configuration;

public class BackupJobConfiguration : IEntityTypeConfiguration<BackupJob>
{
    public void Configure(EntityTypeBuilder<BackupJob> builder)
    {
        builder.ToTable("BackupJobs");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Status)
            .IsRequired();

        builder.Property(x => x.StartedAt)
            .IsRequired();

        builder.Property(x => x.ErrorMessage)
            .HasMaxLength(2000);

        builder.Property(x => x.AgentNameSnapshot)
            .HasMaxLength(100);

        builder.Property(x => x.PolicyNameSnapshot)
            .HasMaxLength(100);

        builder.HasIndex(x => x.StartedAt);

        builder.HasIndex(x => new { x.AgentId, x.StartedAt });

        builder.HasIndex(x => new { x.PolicyId, x.StartedAt });

        builder.HasIndex(x => x.Status);

        // SetNull instead of Cascade so deleting an agent / policy with the
        // "keep history" option simply detaches the job. The snapshot
        // columns preserve display strings.
        builder.HasOne(x => x.Agent)
            .WithMany()
            .HasForeignKey(x => x.AgentId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.Policy)
            .WithMany()
            .HasForeignKey(x => x.PolicyId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(x => x.Artifacts)
            .WithOne(x => x.Job)
            .HasForeignKey(x => x.JobId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
