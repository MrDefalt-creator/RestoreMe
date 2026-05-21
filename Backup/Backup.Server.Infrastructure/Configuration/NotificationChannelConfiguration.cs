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

        // Settings is the encrypted JSON blob — exact size depends on the
        // channel type, but every payload is small. 4000 chars is the
        // largest "varchar" that maps to a single TOAST-free row on
        // Postgres and is well over the largest realistic ciphertext.
        builder.Property(x => x.Settings)
            .IsRequired()
            .HasMaxLength(4000);

        builder.Property(x => x.SubscribedEvents)
            .HasMaxLength(500);

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.HasIndex(x => x.Name)
            .IsUnique();

        builder.HasIndex(x => x.IsEnabled);
    }
}
