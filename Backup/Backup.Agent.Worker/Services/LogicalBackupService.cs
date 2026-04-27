using System.Diagnostics;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Backup.Agent.Worker.DTOs;
using Backup.Agent.Worker.Interfaces;
using Backup.Agent.Worker.Options;
using Backup.Shared.Contracts.DTOs.Policies;
using Microsoft.Extensions.Options;

namespace Backup.Agent.Worker.Services;

public class LogicalBackupService : ILogicalBackupService
{
    private readonly AgentOptions _agentOptions;

    public LogicalBackupService(IOptions<AgentOptions> agentOptions)
    {
        _agentOptions = agentOptions.Value;
    }

    public async Task<PreparedBackupPayload> CreateDumpAsync(
        BackupPolicyDto policy,
        CancellationToken cancellationToken)
    {
        if (policy.DatabaseSettings == null)
        {
            throw new InvalidOperationException("Database settings are required for logical backup policies.");
        }

        return policy.Type switch
        {
            "postgres" => await CreatePostgreSqlDumpAsync(policy, cancellationToken),
            "mysql" => await CreateMySqlDumpAsync(policy, cancellationToken),
            _ => throw new InvalidOperationException($"Unsupported logical policy type '{policy.Type}'.")
        };
    }

    private async Task<PreparedBackupPayload> CreatePostgreSqlDumpAsync(
        BackupPolicyDto policy,
        CancellationToken cancellationToken)
    {
        var settings = policy.DatabaseSettings!;
        var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        var safeDatabaseName = SanitizeFileName(settings.DatabaseName);
        var dumpPath = Path.Combine(
            Path.GetTempPath(),
            $"{safeDatabaseName}_{timestamp}.sql");

        var arguments = new List<string>
        {
            "--no-owner",
            "--no-privileges",
            "--format=plain",
            "--no-password",
            "--file", QuoteArgument(dumpPath)
        };

        if (!string.IsNullOrWhiteSpace(settings.Host))
        {
            arguments.Add("--host");
            arguments.Add(QuoteArgument(settings.Host));
        }

        if (settings.Port.HasValue)
        {
            arguments.Add("--port");
            arguments.Add(settings.Port.Value.ToString());
        }

        if (!string.IsNullOrWhiteSpace(settings.Username))
        {
            arguments.Add("--username");
            arguments.Add(QuoteArgument(settings.Username));
        }

        arguments.Add(QuoteArgument(settings.DatabaseName));

        var environment = new Dictionary<string, string?>();
        if (string.Equals(settings.AuthMode, "credentials", StringComparison.OrdinalIgnoreCase))
        {
            environment["PGPASSWORD"] = settings.Password;
        }

        await RunProcessAsync(
            _agentOptions.PostgreSqlDumpCommand,
            string.Join(' ', arguments),
            environment,
            cancellationToken);

        return new PreparedBackupPayload(
            dumpPath,
            Path.GetFileName(dumpPath),
            "application/sql",
            true);
    }

    private async Task<PreparedBackupPayload> CreateMySqlDumpAsync(
        BackupPolicyDto policy,
        CancellationToken cancellationToken)
    {
        var settings = policy.DatabaseSettings!;
        var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        var safeDatabaseName = SanitizeFileName(settings.DatabaseName);
        var dumpPath = Path.Combine(
            Path.GetTempPath(),
            $"{safeDatabaseName}_{timestamp}.sql");

        var arguments = new List<string>();

        if (!string.IsNullOrWhiteSpace(settings.Host))
        {
            arguments.Add($"--host={QuoteArgument(settings.Host)}");
        }

        if (settings.Port.HasValue)
        {
            arguments.Add($"--port={settings.Port.Value}");
        }

        if (!string.IsNullOrWhiteSpace(settings.Username))
        {
            arguments.Add($"--user={QuoteArgument(settings.Username)}");
        }

        arguments.Add($"--result-file={QuoteArgument(dumpPath)}");
        arguments.Add("--single-transaction");
        arguments.Add("--routines");
        arguments.Add("--events");
        arguments.Add(QuoteArgument(settings.DatabaseName));

        var environment = new Dictionary<string, string?>
        {
            ["MYSQL_PWD"] = settings.Password
        };

        await RunProcessAsync(
            _agentOptions.MySqlDumpCommand,
            string.Join(' ', arguments),
            environment,
            cancellationToken);

        return new PreparedBackupPayload(
            dumpPath,
            Path.GetFileName(dumpPath),
            "application/sql",
            true);
    }

    private static async Task RunProcessAsync(
        string command,
        string arguments,
        IReadOnlyDictionary<string, string?> environment,
        CancellationToken cancellationToken)
    {
        var executablePath = ResolveExecutablePath(command);

        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
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
            {
                process.StartInfo.Environment[pair.Key] = pair.Value;
            }
        }

        try
        {
            process.Start();
            process.StandardInput.Close();
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 2)
        {
            throw new InvalidOperationException(
                $"Dump tool '{command}' was not found. Configure an absolute path in agent settings or make sure the utility is available in the process environment.",
                ex);
        }

        var standardErrorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        var standardOutputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(cancellationToken);

        var standardError = await standardErrorTask;
        var standardOutput = await standardOutputTask;

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Dump process '{executablePath}' failed with exit code {process.ExitCode}. Error: {standardError}. Output: {standardOutput}");
        }
    }

    private static string ResolveExecutablePath(string command)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            throw new InvalidOperationException("Dump tool command is not configured.");
        }

        if (Path.IsPathRooted(command) || command.Contains(Path.DirectorySeparatorChar) || command.Contains(Path.AltDirectorySeparatorChar))
        {
            if (File.Exists(command))
            {
                return command;
            }

            throw new InvalidOperationException($"Configured dump tool path '{command}' does not exist.");
        }

        var fromPath = TryResolveFromPath(command);
        if (fromPath != null)
        {
            return fromPath;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var fromKnownInstall = TryResolveFromKnownWindowsInstall(command);
            if (fromKnownInstall != null)
            {
                return fromKnownInstall;
            }
        }

        return command;
    }

    private static string? TryResolveFromPath(string command)
    {
        var pathValue = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(pathValue))
        {
            return null;
        }

        var extensions = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? (Environment.GetEnvironmentVariable("PATHEXT")?.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
               ?? [".EXE", ".CMD", ".BAT"])
            : [string.Empty];

        foreach (var directory in pathValue.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            foreach (var extension in extensions)
            {
                var candidate = Path.Combine(directory, command.EndsWith(extension, StringComparison.OrdinalIgnoreCase) ? command : command + extension);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }

        return null;
    }

    private static string? TryResolveFromKnownWindowsInstall(string command)
    {
        var executableName = command.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? command
            : $"{command}.exe";

        var roots = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
        }
        .Where(path => !string.IsNullOrWhiteSpace(path))
        .Distinct(StringComparer.OrdinalIgnoreCase);

        if (command.Equals("pg_dump", StringComparison.OrdinalIgnoreCase))
        {
            return TryResolveLatestVersionedBinary(roots, "PostgreSQL", executableName);
        }

        if (command.Equals("mysqldump", StringComparison.OrdinalIgnoreCase))
        {
            return TryResolveLatestVersionedBinary(roots, "MySQL", executableName)
                   ?? TryResolveLatestVersionedBinary(roots, "MariaDB", executableName);
        }

        return null;
    }

    private static string? TryResolveLatestVersionedBinary(IEnumerable<string> roots, string vendorDirectory, string executableName)
    {
        foreach (var root in roots)
        {
            var baseDirectory = Path.Combine(root, vendorDirectory);
            if (!Directory.Exists(baseDirectory))
            {
                continue;
            }

            var candidates = Directory.GetDirectories(baseDirectory)
                .Select(directory => new
                {
                    Directory = directory,
                    Version = ParseVersionOrFallback(Path.GetFileName(directory))
                })
                .OrderByDescending(item => item.Version)
                .Select(item => Path.Combine(item.Directory, "bin", executableName));

            var match = candidates.FirstOrDefault(File.Exists);
            if (match != null)
            {
                return match;
            }
        }

        return null;
    }

    private static Version ParseVersionOrFallback(string? value)
    {
        return Version.TryParse(value, out var version)
            ? version
            : new Version(0, 0);
    }

    private static string SanitizeFileName(string input)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        return string.Concat(input.Select(ch => invalidChars.Contains(ch) ? '_' : ch));
    }

    private static string QuoteArgument(string value)
    {
        return $"\"{value.Replace("\"", "\\\"", StringComparison.Ordinal)}\"";
    }
}
