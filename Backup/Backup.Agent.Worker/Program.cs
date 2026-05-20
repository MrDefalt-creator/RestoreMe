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
//   <base>/state/agent-state.json — encrypted access token + agent id
//   <base>/state/keys/            — DataProtection key ring
// `--reset-state` or RESTOREME_RESET_STATE=1 wipes both before DI starts so
// the agent comes back as a clean slate (next enrollment cycle).
var stateDir = Path.Combine(AppContext.BaseDirectory, "state");
var stateFile = Path.Combine(stateDir, "agent-state.json");
var agentKeysDir = Path.Combine(stateDir, "keys");

if (startup.ResetState)
{
    if (File.Exists(stateFile))
    {
        File.Delete(stateFile);
        Console.WriteLine($"[restoreme-agent] reset-state: removed {stateFile}");
    }
    if (Directory.Exists(agentKeysDir))
    {
        Directory.Delete(agentKeysDir, recursive: true);
        Console.WriteLine($"[restoreme-agent] reset-state: removed {agentKeysDir}");
    }
}

var builder = Host.CreateApplicationBuilder(args);

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

builder.Services.AddOptions<ApiOptions>().Bind(builder.Configuration.GetSection(ApiOptions.SectionName));
builder.Services.AddOptions<AgentOptions>().Bind(builder.Configuration.GetSection(AgentOptions.SectionName));

// Persist DataProtection keys next to the agent state so the encrypted
// agent-state.json survives container recreation or a different host
// user. SetApplicationName isolates the key ring from any other
// RestoreMe component sharing the same directory.
Directory.CreateDirectory(agentKeysDir);
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(agentKeysDir))
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
