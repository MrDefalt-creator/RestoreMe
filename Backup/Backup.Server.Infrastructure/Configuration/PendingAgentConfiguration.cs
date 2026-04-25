using Backup.Server.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Backup.Server.Infrastructure.Configuration;

public class PendingAgentConfiguration : IEntityTypeConfiguration<PendingAgent>
{
    public void Configure(EntityTypeBuilder<PendingAgent> builder)
    {
        builder.ToTable("PendingAgents");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .ValueGeneratedNever();

        builder.Property(x => x.MachineName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.OsType)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.Version)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.Property(x => x.ApprovedAgentId)
            .IsRequired(false);

        builder.HasIndex(x => x.MachineName)
            .IsUnique();

        builder.HasIndex(x => new { x.Status, x.CreatedAt });
        
        builder.HasIndex(x => x.ApprovedAgentId)
            .IsUnique();
        
        builder.HasOne(x => x.ApprovedAgent)
            .WithOne(x => x.PendingAgent)
            .HasForeignKey<PendingAgent>(x => x.ApprovedAgentId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
