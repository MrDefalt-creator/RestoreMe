namespace Backup.Agent.Worker.Startup;

// Resolution order for the agent state directory:
//   1. --state-dir / RESTOREME_STATE_DIR (operator-provided)
//   2. OS-appropriate default, if we can create + write into it
//        Linux:   /var/lib/restoreme-agent/state
//        Windows: %ProgramData%\RestoreMe\Agent\state
//        macOS:   ~/Library/Application Support/RestoreMe/Agent/state
//   3. AppContext.BaseDirectory/state — fall-back for `dotnet run` from a
//      checkout where the OS default isn't writable without elevation.
public static class StateDirectoryResolver
{
    public static AgentStateLocation Resolve(AgentStartupOptions startup)
    {
        if (!string.IsNullOrWhiteSpace(startup.ExplicitStateDirectory))
        {
            var dir = Path.GetFullPath(startup.ExplicitStateDirectory);
            return Build(dir, startup.ExplicitStateDirectorySource ?? "explicit override");
        }

        var osDefault = GetOsDefault();
        if (osDefault != null && TryEnsureWritable(osDefault))
        {
            return Build(osDefault, "OS default");
        }

        var fallback = Path.Combine(AppContext.BaseDirectory, "state");
        return Build(fallback, "AppContext.BaseDirectory fallback");
    }

    private static AgentStateLocation Build(string directory, string source)
    {
        return new AgentStateLocation
        {
            Directory = directory,
            StateFilePath = Path.Combine(directory, "agent-state.json"),
            KeyRingDirectory = Path.Combine(directory, "keys"),
            Source = source,
        };
    }

    private static string? GetOsDefault()
    {
        if (OperatingSystem.IsLinux())
        {
            return "/var/lib/restoreme-agent/state";
        }

        if (OperatingSystem.IsWindows())
        {
            var programData = Environment.GetEnvironmentVariable("ProgramData");
            if (string.IsNullOrWhiteSpace(programData))
            {
                programData = @"C:\ProgramData";
            }
            return Path.Combine(programData, "RestoreMe", "Agent", "state");
        }

        if (OperatingSystem.IsMacOS())
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (string.IsNullOrWhiteSpace(home))
            {
                return null;
            }
            return Path.Combine(home, "Library", "Application Support", "RestoreMe", "Agent", "state");
        }

        return null;
    }

    private static bool TryEnsureWritable(string path)
    {
        try
        {
            Directory.CreateDirectory(path);
            var probe = Path.Combine(path, $".probe-{Guid.NewGuid():N}");
            File.WriteAllText(probe, string.Empty);
            File.Delete(probe);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
