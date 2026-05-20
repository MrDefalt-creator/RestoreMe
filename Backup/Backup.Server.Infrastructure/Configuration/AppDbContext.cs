using Backup.Server.Domain.Entities;
using Backup.Server.Infrastructure.Persistence;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace Backup.Server.Infrastructure.Configuration;

public class AppDbContext : DbContext
{
    private readonly IDataProtector _policyPasswordProtector;

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

    public AppDbContext(DbContextOptions<AppDbContext> options, IDataProtectionProvider dataProtection)
        : base(options)
    {
        _policyPasswordProtector = dataProtection.CreateProtector("BackupPolicyDbPassword.v1");
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

        base.OnModelCreating(modelBuilder);
    }
}
