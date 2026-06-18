using Backup.Agent.Worker;
using Backup.Agent.Worker.ApiClients;
using Backup.Agent.Worker.Interfaces;
using Backup.Agent.Worker.Options;
using Backup.Agent.Worker.Services;
using Backup.Agent.Worker.Startup;
using Backup.Agent.Worker.State;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;

var startup = AgentStartupOptions.Build(args);

// State directory layout:
//   <state-dir>/agent-state.json — encrypted access token + agent id
//   <state-dir>/keys/            — DataProtection key ring
// `--state-dir` / RESTOREME_STATE_DIR overrides the location; otherwise we
// pick an OS-appropriate default (writable check ensures dev `dotnet run`
// from a checkout falls back to AppContext.BaseDirectory).
// `--reset-state` or RESTOREME_RESET_STATE=1 wipes both before DI starts so
// the agent comes back as a clean slate (next enrollment cycle).
var stateLocation = StateDirectoryResolver.Resolve(startup);
Console.WriteLine(
    $"[restoreme-agent] state directory: {stateLocation.Directory} (source: {stateLocation.Source})");

if (startup.ResetState)
{
    if (File.Exists(stateLocation.StateFilePath))
    {
        File.Delete(stateLocation.StateFilePath);
        Console.WriteLine($"[restoreme-agent] reset-state: removed {stateLocation.StateFilePath}");
    }
    if (Directory.Exists(stateLocation.KeyRingDirectory))
    {
        Directory.Delete(stateLocation.KeyRingDirectory, recursive: true);
        Console.WriteLine($"[restoreme-agent] reset-state: removed {stateLocation.KeyRingDirectory}");
    }
}

var builder = Host.CreateApplicationBuilder(args);

// Lift the process into a Windows Service when SCM started us. No-op
// when running interactively or on non-Windows, so this stays safe for
// `dotnet run` and the Linux/systemd path.
builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "RestoreMe Agent";
});

// Fold operator-supplied overrides into IConfiguration so existing code that
// reads ApiOptions.EnrollmentToken / ApiOptions.BaseUrl picks them up. The
// in-memory layer is appended last so it wins over appsettings.json.
var overrides = new Dictionary<string, string?>();
if (!string.IsNullOrWhiteSpace(startup.ExplicitServerUrl))
{
    overrides["Api:BaseUrl"] = startup.ExplicitServerUrl;
}
if (!string.IsNullOrWhiteSpace(startup.ExplicitEnrollmentToken))
{
    overrides["Api:EnrollmentToken"] = startup.ExplicitEnrollmentToken;
}
if (overrides.Count > 0)
{
    builder.Configuration.AddInMemoryCollection(overrides);
}

builder.Services.AddSingleton(startup);
builder.Services.AddSingleton(stateLocation);

builder.Services.AddOptions<ApiOptions>().Bind(builder.Configuration.GetSection(ApiOptions.SectionName));
builder.Services.AddOptions<AgentOptions>().Bind(builder.Configuration.GetSection(AgentOptions.SectionName));

// Persist DataProtection keys next to the agent state so the encrypted
// agent-state.json survives container recreation or a different host
// user. SetApplicationName isolates the key ring from any other
// RestoreMe component sharing the same directory.
Directory.CreateDirectory(stateLocation.KeyRingDirectory);
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(stateLocation.KeyRingDirectory))
    .SetApplicationName("RestoreMe.Agent");

builder.Services.AddSingleton<IAgentState, FileAgentStore>();
builder.Services.AddSingleton<IApiEndpointResolver, ApiEndpointResolver>();
builder.Services.AddTransient<AgentAccessTokenHandler>();

static void ConfigureBaseAddress(IServiceProvider sp, HttpClient client)
{
    // Sync path — no GetAwaiter().GetResult() in factory delegates.
    var endpoint = sp.GetRequiredService<IApiEndpointResolver>().Resolve();
    client.BaseAddress = new Uri(endpoint.BaseUrl);
}

builder.Services.AddHttpClient<IAgentApiClient, AgentApiClient>(ConfigureBaseAddress)
    .AddHttpMessageHandler<AgentAccessTokenHandler>()
    .AddStandardResilienceHandler();

builder.Services.AddHttpClient<IBackupApiClient, BackupApiClient>(ConfigureBaseAddress)
    .AddHttpMessageHandler<AgentAccessTokenHandler>()
    .AddStandardResilienceHandler();

builder.Services.AddHttpClient<IMinioStorageClient, MinioStorageClient>();
builder.Services.AddSingleton<IArchiveService, ArchiveService>();
builder.Services.AddSingleton<IChecksumService, ChecksumService>();
builder.Services.AddSingleton<ILogicalBackupService, LogicalBackupService>();
builder.Services.AddTransient<IBackupExecutor, BackupExecuter>();
builder.Services.AddSingleton<LogicalRestoreService>();
builder.Services.AddTransient<IRestoreExecutor, RestoreExecuter>();

builder.Services.AddHttpClient<IRestoreApiClient, RestoreApiClient>(ConfigureBaseAddress)
    .AddHttpMessageHandler<AgentAccessTokenHandler>()
    .AddStandardResilienceHandler();

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
