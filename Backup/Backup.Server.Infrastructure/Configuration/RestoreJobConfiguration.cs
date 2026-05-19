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
        builder.Property(x => x.AgentId).IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.ErrorMessage).HasMaxLength(2000);

        builder.HasOne(x => x.Artifact)
            .WithMany()
            .HasForeignKey(x => x.ArtifactId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.AgentId, x.Status });
    }
}
