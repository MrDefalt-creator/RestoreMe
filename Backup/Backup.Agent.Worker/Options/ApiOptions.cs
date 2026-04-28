namespace Backup.Agent.Worker.Options;

public sealed record ApiOptions
{
    public const string SectionName = "Api";

    public string BaseUrl { get; init; } = string.Empty;
    public string EnrollmentToken { get; init; } = string.Empty;
}
