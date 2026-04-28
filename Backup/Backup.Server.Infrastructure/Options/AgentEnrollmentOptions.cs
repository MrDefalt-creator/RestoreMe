namespace Backup.Server.Infrastructure.Options;

public class AgentEnrollmentOptions
{
    public const string SectionName = "AgentEnrollment";

    public string EnrollmentToken { get; init; } = "change-me-enrollment-token";
}
