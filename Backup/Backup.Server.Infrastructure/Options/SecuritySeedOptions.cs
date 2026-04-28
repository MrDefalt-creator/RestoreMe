namespace Backup.Server.Infrastructure.Options;

public class SecuritySeedOptions
{
    public const string SectionName = "SecuritySeed";

    public List<SecuritySeedUserOptions> Users { get; init; } = [];
}

public class SecuritySeedUserOptions
{
    public string Username { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
    public string Role { get; init; } = "viewer";
}
