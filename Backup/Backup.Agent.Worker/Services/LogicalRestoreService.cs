using System.ComponentModel;
using System.Runtime.InteropServices;
using Backup.Agent.Worker.Options;
using Backup.Shared.Contracts.DTOs.Policies;
using Microsoft.Extensions.Options;

namespace Backup.Agent.Worker.Services;

public class LogicalRestoreService
{
    private readonly AgentOptions _agentOptions;

    public LogicalRestoreService(IOptions<AgentOptions> agentOptions)
    {
        _agentOptions = agentOptions.Value;
    }

    public async Task RestoreAsync(
        string policyType,
        BackupPolicyDatabaseSettingsDto settings,
        string dumpFilePath,
        CancellationToken cancellationToken)
    {
        if (string.Equals(policyType, "postgres", StringComparison.OrdinalIgnoreCase))
        {
            await RestorePostgreSqlAsync(settings, dumpFilePath, cancellationToken);
        }
        else if (string.Equals(policyType, "mysql", StringComparison.OrdinalIgnoreCase))
        {
            await RestoreMySqlAsync(settings, dumpFilePath, cancellationToken);
        }
        else
        {
            throw new InvalidOperationException($"Unsupported logical policy type '{policyType}'.");
        }
    }

    private async Task RestorePostgreSqlAsync(
        BackupPolicyDatabaseSettingsDto settings,
        string dumpFilePath,
        CancellationToken cancellationToken)
    {
        var arguments = new List<string> { "--no-password" };

        if (!string.IsNullOrWhiteSpace(settings.Host))
        { arguments.Add("--host"); arguments.Add(QuoteArgument(settings.Host)); }

        if (settings.Port.HasValue)
        { arguments.Add("--port"); arguments.Add(settings.Port.Value.ToString()); }

        if (!string.IsNullOrWhiteSpace(settings.Username))
        { arguments.Add("--username"); arguments.Add(QuoteArgument(settings.Username)); }

        arguments.Add(QuoteArgument(settings.DatabaseName));

        var env = new Dictionary<string, string?>();
        if (string.Equals(settings.AuthMode, "credentials", StringComparison.OrdinalIgnoreCase))
            env["PGPASSWORD"] = settings.Password;

        await RunRestoreProcessAsync(_agentOptions.PostgreSqlRestoreCommand, string.Join(' ', arguments), env, dumpFilePath, cancellationToken);
    }

    private async Task RestoreMySqlAsync(
        BackupPolicyDatabaseSettingsDto settings,
        string dumpFilePath,
        CancellationToken cancellationToken)
    {
        var arguments = new List<string>();

        if (!string.IsNullOrWhiteSpace(settings.Host))
            arguments.Add($"--host={QuoteArgument(settings.Host)}");
        if (settings.Port.HasValue)
            arguments.Add($"--port={settings.Port.Value}");
        if (!string.IsNullOrWhiteSpace(settings.Username))
            arguments.Add($"--user={QuoteArgument(settings.Username)}");

        arguments.Add(QuoteArgument(settings.DatabaseName));

        var env = new Dictionary<string, string?> { ["MYSQL_PWD"] = settings.Password };

        await RunRestoreProcessAsync(_agentOptions.MySqlRestoreCommand, string.Join(' ', arguments), env, dumpFilePath, cancellationToken);
    }

    private static async Task RunRestoreProcessAsync(
        string command,
        string arguments,
        IReadOnlyDictionary<string, string?> environment,
        string inputFilePath,
        CancellationToken cancellationToken)
    {
        var executablePath = ResolveExecutablePath(command);

        using var process = new System.Diagnostics.Process();
        process.StartInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = executablePath,
            Arguments = arguments,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            RedirectStandardInput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var pair in environment)
        {
            if (!string.IsNullOrWhiteSpace(pair.Value))
                process.StartInfo.Environment[pair.Key] = pair.Value;
        }

        try { process.Start(); }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 2)
        {
            throw new InvalidOperationException($"Restore tool '{command}' was not found.", ex);
        }

        var stdinTask = Task.Run(async () =>
        {
            // Auto-detects and transparently decompresses zstd artifacts; plain
            // .sql dumps pass through unchanged.
            await using var source = DumpArtifactReader.OpenForRestore(inputFilePath);
            await source.CopyToAsync(process.StandardInput.BaseStream, cancellationToken);
            process.StandardInput.Close();
        }, cancellationToken);

        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(cancellationToken);
        await stdinTask;

        var stderr = await stderrTask;
        var stdout = await stdoutTask;

        if (process.ExitCode != 0)
        {
            System.Diagnostics.Debug.WriteLine($"[{executablePath}] stderr: {stderr} | stdout: {stdout}");
            throw new InvalidOperationException($"Restore process '{executablePath}' failed with exit code {process.ExitCode}.");
        }
    }

    private static string ResolveExecutablePath(string command)
    {
        if (string.IsNullOrWhiteSpace(command))
            throw new InvalidOperationException("Restore tool command is not configured.");

        if (Path.IsPathRooted(command) || command.Contains(Path.DirectorySeparatorChar) || command.Contains(Path.AltDirectorySeparatorChar))
        {
            if (File.Exists(command)) return command;
            throw new InvalidOperationException($"Configured restore tool path '{command}' does not exist.");
        }

        var pathValue = Environment.GetEnvironmentVariable("PATH");
        if (!string.IsNullOrWhiteSpace(pathValue))
        {
            var extensions = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? (Environment.GetEnvironmentVariable("PATHEXT")?.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [".EXE", ".CMD", ".BAT"])
                : [string.Empty];

            foreach (var dir in pathValue.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                foreach (var ext in extensions)
                {
                    var candidate = Path.Combine(dir, command.EndsWith(ext, StringComparison.OrdinalIgnoreCase) ? command : command + ext);
                    if (File.Exists(candidate)) return candidate;
                }
        }

        return command;
    }

    private static string QuoteArgument(string value) => $"\"{value.Replace("\"", "\\\"", StringComparison.Ordinal)}\"";
}
