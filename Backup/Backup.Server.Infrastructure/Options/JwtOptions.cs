namespace Backup.Server.Infrastructure.Options;

public class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; init; } = "RestoreMe";
    public string Audience { get; init; } = "RestoreMe";
    public string SigningKey { get; init; } = "ChangeMe-This-Is-Not-A-Secure-Production-Key";

    // Optional dedicated key for agent JWTs. When set, user tokens use
    // SigningKey and agent tokens use AgentSigningKey — so operators can
    // rotate one without invalidating the other. Empty/null falls back to
    // SigningKey (legacy behaviour, single key for both audiences).
    public string? AgentSigningKey { get; init; }

    public int UserTokenLifetimeMinutes { get; init; } = 480;
    public int AgentTokenLifetimeDays { get; init; } = 30;

    public int UserAccessTokenLifetimeMinutes { get; init; } = 15;
    public int RefreshLifetimeDays { get; init; } = 30;
}
