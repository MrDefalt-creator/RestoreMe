using Backup.Server.Domain.Entities;
using Backup.Server.Infrastructure.Persistence;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace Backup.Server.Infrastructure.Configuration;

public class AppDbContext : DbContext
{
    private readonly IDataProtector _policyPasswordProtector;
    private readonly IDataProtector _notificationSettingsProtector;

    public DbSet<AppUser> AppUsers { get; set; }
    public DbSet<Agent> Agents { get; set; }
    public DbSet<BackupJob> BackupJobs { get; set; }
    public DbSet<BackupPolicy> BackupPolicies { get; set; }
    public DbSet<BackupPolicyDatabaseSettings> BackupPolicyDatabaseSettings { get; set; }
    public DbSet<BackupArtifact> BackupArtifacts { get; set; }

    public DbSet<PendingAgent> PendingAgents { get; set; }
    public DbSet<AuditLog> AuditLogs { get; set; }
    public DbSet<RestoreJob> RestoreJobs { get; set; }
    public DbSet<AgentInstallToken> AgentInstallTokens { get; set; }
    public DbSet<NotificationChannel> NotificationChannels { get; set; }
    public DbSet<IntegrityScrubSettings> IntegrityScrubSettings { get; set; }

    public AppDbContext(DbContextOptions<AppDbContext> options, IDataProtectionProvider dataProtection)
        : base(options)
    {
        _policyPasswordProtector = dataProtection.CreateProtector("BackupPolicyDbPassword.v1");
        _notificationSettingsProtector = dataProtection.CreateProtector("NotificationChannelSettings.v1");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // Encrypt the DB-credential password column at rest. Applied here
        // instead of in BackupPolicyDatabaseSettingsConfiguration because
        // the converter needs an IDataProtector from DI.
        modelBuilder.Entity<BackupPolicyDatabaseSettings>()
            .Property(x => x.Password)
            .HasConversion(new EncryptedStringConverter(_policyPasswordProtector));

        // Notification channel Settings JSON carries at least one secret
        // for every channel type (bot tokens, webhook URLs, shared HMAC
        // secrets). Encrypt the whole blob — partial-leak attacks are
        // worse than the trivial CPU cost of one Protect/Unprotect call.
        modelBuilder.Entity<NotificationChannel>()
            .Property(x => x.Settings)
            .HasConversion(new RequiredEncryptedStringConverter(_notificationSettingsProtector));

        base.OnModelCreating(modelBuilder);
    }
}
