namespace Backup.Server.Api.Security;

public static class AuthConstants
{
    public const string JwtScheme = "Bearer";
    public const string AgentEnrollmentScheme = "AgentEnrollment";
    public const string AgentEnrollmentHeader = "X-Agent-Enrollment-Token";

    public const string TokenTypeClaim = "token_type";
    public const string UserTokenType = "user";
    public const string AgentTokenType = "agent";

    public const string ViewerRole = "Viewer";
    public const string OperatorRole = "Operator";
    public const string AdminRole = "Admin";
    public const string AgentRole = "Agent";

    public const string AdminReadPolicy = "AdminRead";
    public const string AdminWritePolicy = "AdminWrite";
    public const string UserManagementPolicy = "UserManagement";
    public const string AgentPolicy = "Agent";
    public const string AgentEnrollmentPolicy = "AgentEnrollment";
}
