using Backup.Server.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Backup.Server.Infrastructure.Configuration;

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("RefreshTokens");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.TokenHash)
            .IsRequired()
            .HasMaxLength(64);

        // Also a SHA-256 hex value; keep it sized like TokenHash rather than
        // an unbounded text column.
        builder.Property(x => x.ReplacedByTokenHash).HasMaxLength(64);

        // Refresh lookups happen by hash on every rotation, so this pays off.
        builder.HasIndex(x => x.TokenHash).IsUnique();

        // Session listing / bulk-revoke queries filter by user and active status.
        builder.HasIndex(x => new { x.UserId, x.RevokedAtUtc });

        // Family revoke (logout, reuse-burn, single-session revoke) filters by
        // FamilyId — index it so those aren't table scans.
        builder.HasIndex(x => x.FamilyId);

        builder.Property(x => x.UserAgent).HasMaxLength(400);
        builder.Property(x => x.CreatedByIp).HasMaxLength(64);

        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
