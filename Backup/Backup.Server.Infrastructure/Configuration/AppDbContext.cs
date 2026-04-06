using Backup.Server.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Backup.Server.Infrastructure.Configuration;

public class AppDbContext : DbContext
{
    public DbSet<Agent> Agents { get; set; }
    public DbSet<BackupJob> BackupJobs { get; set; }
    public DbSet<BackupPolicy> BackupPolicies { get; set; }
    public DbSet<BackupArtifact> BackupArtifacts { get; set; }
    
    public DbSet<PendingAgent>  PendingAgents { get; set; }

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
        Database.EnsureCreated();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}