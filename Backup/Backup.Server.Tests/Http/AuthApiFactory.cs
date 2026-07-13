using Backup.Server.Domain.Entities;
using Backup.Server.Domain.Enums;
using Backup.Server.Infrastructure.Configuration;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Backup.Server.Tests.Http;

/// <summary>
/// Boots the real API against an in-memory SQLite DB + ephemeral DataProtection,
/// with all background hosted services stripped. One instance == one isolated
/// database and one isolated rate-limiter budget, so tests never share state.
/// </summary>
public class AuthApiFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection;

    public AuthApiFactory()
    {
        // Held open for the factory's lifetime so the :memory: DB survives across
        // the many scopes the app opens per request.
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Development, not Testing: the prod-config guard and the Secure-cookie
        // flag both key off !IsDevelopment(). Development skips the guard (dev
        // signing key is fine) and leaves cookies non-Secure so they round-trip
        // over TestServer's plain http. The SQLite schema path is keyed on the
        // provider name in Program.cs, not the environment.
        builder.UseEnvironment("Development");

        builder.ConfigureTestServices(services =>
        {
            // Swap Npgsql AppDbContext -> shared in-memory SQLite. EF Core 10 keeps
            // the provider wiring in IDbContextOptionsConfiguration<T> as well as
            // DbContextOptions<T>, so every AppDbContext/EF-options descriptor must
            // go or EF sees two providers registered.
            var efDescriptors = services.Where(d =>
                (d.ServiceType.FullName?.Contains("AppDbContext", StringComparison.Ordinal) ?? false) ||
                d.ServiceType == typeof(DbContextOptions))
                .ToList();
            foreach (var d in efDescriptors)
            {
                services.Remove(d);
            }
            services.AddDbContext<AppDbContext>(o => o.UseSqlite(_connection));

            // Deterministic in-process key ring (matches the unit-test fixture).
            services.Replace(ServiceDescriptor.Singleton<IDataProtectionProvider, EphemeralDataProtectionProvider>());

            // Remove all background workers (Retention/IntegrityScrub/AgentHealthSweep/
            // MinioBucketInitializer/AgentInstallTokenCleanup) — they hit live
            // Postgres/MinIO on StartAsync, which isn't present under test.
            services.RemoveAll<IHostedService>();
        });
    }

    /// <summary>Inserts an active user with a real password hash and returns its id.</summary>
    public async Task<Guid> SeedUserAsync(string username, string password, AppUserRole role = AppUserRole.Admin)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<AppUser>>();

        var user = new AppUser
        {
            Id = Guid.NewGuid(),
            Username = username,
            NormalizedUsername = username.ToUpperInvariant(),
            Role = role,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        };
        user.PasswordHash = hasher.HashPassword(user, password);
        db.AppUsers.Add(user);
        await db.SaveChangesAsync();
        return user.Id;
    }

    /// <summary>Runs <paramref name="work"/> inside a fresh DI scope (e.g. to read DB/audit rows).</summary>
    public async Task<T> InScopeAsync<T>(Func<AppDbContext, Task<T>> work)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await work(db);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _connection.Dispose();
        }
    }
}
