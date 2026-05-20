namespace Backup.Agent.Worker.Startup;

// Captures values the operator passed via CLI or env at process start.
// When ExplicitServerUrl or ExplicitEnrollmentToken is set, those values
// win over any persisted state — so an operator who points the agent
// to a new backend with --server doesn't have to hunt down and delete
// the state file first.
public sealed class AgentStartupOptions
{
    public string? ExplicitServerUrl { get; init; }
    public string? ExplicitEnrollmentToken { get; init; }
    public bool ResetState { get; init; }

    public string? ExplicitServerSource { get; init; }

    public static AgentStartupOptions Build(string[] args)
    {
        var explicitServer = ReadFlag(args, "--server") ?? Environment.GetEnvironmentVariable("RESTOREME_SERVER");
        var explicitServerSource = ReadFlag(args, "--server") != null
            ? "command line (--server)"
            : Environment.GetEnvironmentVariable("RESTOREME_SERVER") != null
                ? "environment (RESTOREME_SERVER)"
                : null;

        var explicitToken = ReadFlag(args, "--enrollment-token")
            ?? Environment.GetEnvironmentVariable("RESTOREME_ENROLLMENT_TOKEN");

        var resetState = args.Any(a => string.Equals(a, "--reset-state", StringComparison.OrdinalIgnoreCase))
            || string.Equals(Environment.GetEnvironmentVariable("RESTOREME_RESET_STATE"), "1", StringComparison.Ordinal);

        return new AgentStartupOptions
        {
            ExplicitServerUrl = string.IsNullOrWhiteSpace(explicitServer) ? null : explicitServer.Trim(),
            ExplicitServerSource = explicitServerSource,
            ExplicitEnrollmentToken = string.IsNullOrWhiteSpace(explicitToken) ? null : explicitToken.Trim(),
            ResetState = resetState,
        };
    }

    private static string? ReadFlag(string[] args, string flag)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], flag, StringComparison.OrdinalIgnoreCase))
            {
                return args[i + 1];
            }
        }

        return null;
    }
}
