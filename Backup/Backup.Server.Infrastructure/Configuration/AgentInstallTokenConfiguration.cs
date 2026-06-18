using Backup.Server.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Backup.Server.Infrastructure.Configuration;

public class AgentInstallTokenConfiguration : IEntityTypeConfiguration<AgentInstallToken>
{
    public void Configure(EntityTypeBuilder<AgentInstallToken> builder)
    {
        builder.ToTable("AgentInstallTokens");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.TokenHash)
            .IsRequired()
            .HasMaxLength(32);

        // Look-ups happen by hash during enrollment, so the index pays off.
        builder.HasIndex(x => x.TokenHash).IsUnique();

        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.ExpiresAt).IsRequired();
        builder.Property(x => x.CreatedByUserId).IsRequired();

        builder.Property(x => x.UsedByMachineName).HasMaxLength(255);
        builder.Property(x => x.PreApprovedName).HasMaxLength(255);

        // Cleanup service scans by ExpiresAt for unused expired rows.
        builder.HasIndex(x => x.ExpiresAt);
    }
}
