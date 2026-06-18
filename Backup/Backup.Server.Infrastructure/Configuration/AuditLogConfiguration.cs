using Backup.Server.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Backup.Server.Infrastructure.Configuration;

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("AuditLogs");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Action)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.Details)
            .HasMaxLength(1000);

        builder.Property(x => x.OccurredAt)
            .IsRequired();

        builder.HasIndex(x => x.OccurredAt);
        builder.HasIndex(x => x.ActorId);
    }
}
