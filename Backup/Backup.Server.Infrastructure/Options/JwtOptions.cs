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

    // Grace window after a token is rotated during which re-presenting the
    // parent token is treated as a benign duplicate (a concurrent browser tab,
    // or a retried request whose Set-Cookie was lost) rather than token reuse.
    // Outside this window a replayed rotated token still burns the family.
    public int RefreshReuseGraceSeconds { get; init; } = 30;
}
