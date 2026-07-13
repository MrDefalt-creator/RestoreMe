using System.Net;
using Backup.Server.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Backup.Server.Tests.Http;

public sealed class LockoutTests
{
    [Fact]
    public async Task Five_failures_lock_the_account_even_for_the_correct_password()
    {
        await using var factory = new AuthApiFactory();
        var userId = await factory.SeedUserAsync("bob", "Right-pass-1!", AppUserRole.Operator);
        var client = factory.CreateClient();

        // 5 wrong-password attempts (threshold = MaxFailedAttemptsBeforeLockout).
        for (var i = 0; i < 5; i++)
        {
            var bad = await AuthTestHelpers.LoginAsync(client, "bob", "definitely-wrong");
            Assert.Equal(HttpStatusCode.Unauthorized, bad.StatusCode);
        }

        // 6th attempt with the CORRECT password is still rejected: account is locked.
        var locked = await AuthTestHelpers.LoginAsync(client, "bob", "Right-pass-1!");
        Assert.Equal(HttpStatusCode.Unauthorized, locked.StatusCode);

        // The lockout was audit-logged.
        var lockoutLogged = await factory.InScopeAsync(db =>
            db.AuditLogs.AnyAsync(a => a.Action == "auth.lockout" && a.TargetId == userId));
        Assert.True(lockoutLogged);
    }
}
