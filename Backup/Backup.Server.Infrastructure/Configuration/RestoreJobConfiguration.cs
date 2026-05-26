using Backup.Server.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Backup.Server.Infrastructure.Configuration;

public class RestoreJobConfiguration : IEntityTypeConfiguration<RestoreJob>
{
    public void Configure(EntityTypeBuilder<RestoreJob> builder)
    {
        builder.ToTable("RestoreJobs");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Status).IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.ErrorMessage).HasMaxLength(2000);

        builder.Property(x => x.AgentNameSnapshot).HasMaxLength(100);
        builder.Property(x => x.ArtifactFileNameSnapshot).HasMaxLength(260);
        builder.Property(x => x.ArtifactObjectKeySnapshot).HasMaxLength(512);

        // SetNull (was Restrict) so deleting the source artifact / agent
        // with the "keep restore history" toggle on simply detaches the
        // restore row instead of refusing the delete entirely.
        builder.HasOne(x => x.Artifact)
            .WithMany()
            .HasForeignKey(x => x.ArtifactId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => new { x.AgentId, x.Status });
    }
}
