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
        var compress = policy.CompressDumps;
        var safeDatabaseName = SanitizeFileName(settings.DatabaseName);
        var dumpPath = Path.Combine(
            Path.GetTempPath(),
            $"{safeDatabaseName}_{Guid.NewGuid():N}{(compress ? ".sql.zst" : ".sql")}");

        // No --file: pg_dump writes the plain dump to stdout, which we stream
        // (optionally through zstd) straight to the artifact on disk.
        var arguments = new List<string>
        {
            "--no-owner",
            "--no-privileges",
            "--format=plain",
            "--no-password",
        };

        if (!string.IsNullOrWhiteSpace(settings.Host))
        {
            arguments.Add("--host");
            arguments.Add(settings.Host);
        }

        if (settings.Port.HasValue)
        {
            arguments.Add("--port");
            arguments.Add(settings.Port.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        if (!string.IsNullOrWhiteSpace(settings.Username))
        {
            arguments.Add("--username");
            arguments.Add(settings.Username);
        }

        arguments.Add(settings.DatabaseName);

        var environment = new Dictionary<string, string?>();
        if (string.Equals(settings.AuthMode, "credentials", StringComparison.OrdinalIgnoreCase))
        {
            environment["PGPASSWORD"] = settings.Password;
        }

        await RunDumpAsync(
            _agentOptions.PostgreSqlDumpCommand,
            arguments,
            environment,
            dumpPath,
            compress,
            cancellationToken);

        return new PreparedBackupPayload(
            dumpPath,
            Path.GetFileName(dumpPath),
            compress ? "application/zstd" : "application/sql",
            true);
    }

    private async Task<PreparedBackupPayload> CreateMySqlDumpAsync(
        BackupPolicyDto policy,
        CancellationToken cancellationToken)
    {
        var settings = policy.DatabaseSettings!;
        var compress = policy.CompressDumps;
        var safeDatabaseName = SanitizeFileName(settings.DatabaseName);
        var dumpPath = Path.Combine(
            Path.GetTempPath(),
            $"{safeDatabaseName}_{Guid.NewGuid():N}{(compress ? ".sql.zst" : ".sql")}");

        var arguments = new List<string>();

        if (!string.IsNullOrWhiteSpace(settings.Host))
        {
            arguments.Add($"--host={settings.Host}");
        }

        if (settings.Port.HasValue)
        {
            arguments.Add($"--port={settings.Port.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
        }

        if (!string.IsNullOrWhiteSpace(settings.Username))
        {
            arguments.Add($"--user={settings.Username}");
        }

        // No --result-file: mysqldump writes to stdout, which we stream
        // (optionally through zstd) straight to the artifact on disk.
        arguments.Add("--single-transaction");
        arguments.Add("--routines");
        arguments.Add("--events");
        arguments.Add(settings.DatabaseName);

        var environment = new Dictionary<string, string?>
        {
            ["MYSQL_PWD"] = settings.Password
        };

        await RunDumpAsync(
            _agentOptions.MySqlDumpCommand,
            arguments,
            environment,
            dumpPath,
            compress,
            cancellationToken);

        return new PreparedBackupPayload(
            dumpPath,
            Path.GetFileName(dumpPath),
            compress ? "application/zstd" : "application/sql",
            true);
    }

    private static async Task RunDumpAsync(
        string command,
        IReadOnlyList<string> arguments,
        IReadOnlyDictionary<string, string?> environment,
        string destinationPath,
        bool compress,
        CancellationToken cancellationToken)
    {
        var executablePath = ResolveExecutablePath(command);

        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            RedirectStandardInput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        // ArgumentList does its own platform-appropriate quoting — args
        // with spaces, embedded quotes or odd characters end up as a
        // single argv entry to the child process instead of being split
        // by a half-correct manual escape.
        foreach (var arg in arguments)
        {
            process.StartInfo.ArgumentList.Add(arg);
        }

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

        // Drain stderr concurrently while we stream stdout to disk, so a chatty
        // dump can't fill the stderr pipe and deadlock the write.
        var standardErrorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await DumpArtifactWriter.WriteAsync(
            process.StandardOutput.BaseStream, destinationPath, compress, cancellationToken);

        await process.WaitForExitAsync(cancellationToken);
        var standardError = await standardErrorTask;

        if (process.ExitCode != 0)
        {
            // stderr may contain connection details; log it separately rather than
            // propagating through the exception chain where it could reach structured logs.
            System.Diagnostics.Debug.WriteLine($"[{executablePath}] stderr: {standardError}");
            // The failed dump may have produced a partial/empty artifact; drop it.
            TryDeleteFile(destinationPath);
            throw new InvalidOperationException(
                $"Dump process '{executablePath}' failed with exit code {process.ExitCode}.");
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Best-effort cleanup of a failed dump artifact.
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
}
