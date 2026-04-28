namespace Backup.Server.Infrastructure.Options;

public class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; init; } = "RestoreMe";
    public string Audience { get; init; } = "RestoreMe";
    public string SigningKey { get; init; } = "ChangeMe-This-Is-Not-A-Secure-Production-Key";
    public int UserTokenLifetimeMinutes { get; init; } = 480;
    public int AgentTokenLifetimeDays { get; init; } = 30;
}
