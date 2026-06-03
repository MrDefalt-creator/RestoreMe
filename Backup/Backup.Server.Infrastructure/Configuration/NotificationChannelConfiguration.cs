using Backup.Server.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Backup.Server.Infrastructure.Configuration;

public class NotificationChannelConfiguration : IEntityTypeConfiguration<NotificationChannel>
{
    public void Configure(EntityTypeBuilder<NotificationChannel> builder)
    {
        builder.ToTable("NotificationChannels");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(x => x.Type)
            .IsRequired();

        builder.Property(x => x.IsEnabled)
            .IsRequired();

        // Settings is the encrypted JSON blob. The value converter
        // (DataProtection) stores ciphertext, which is meaningfully larger
        // than the plaintext (fixed header + IV + HMAC tag, then base64).
        // A char-length cap here would be measured against the plaintext on
        // input validation but enforced against the ciphertext in the
        // column, so the two never line up — a tight cap silently rejects
        // valid input. Use an unbounded column (Postgres `text`, identical
        // storage/perf to varchar for small values) and bound the *plaintext*
        // in NotificationChannelsService.ValidateSettings instead.
        builder.Property(x => x.Settings)
            .IsRequired();

        builder.Property(x => x.SubscribedEvents)
            .HasMaxLength(500);

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.HasIndex(x => x.Name)
            .IsUnique();

        builder.HasIndex(x => x.IsEnabled);
    }
}
