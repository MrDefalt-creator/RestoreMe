using Backup.Agent.Worker;
using Backup.Agent.Worker.ApiClients;
using Backup.Agent.Worker.Interfaces;
using Backup.Agent.Worker.Options;
using Backup.Agent.Worker.Services;
using Backup.Agent.Worker.State;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddOptions<ApiOptions>().Bind(builder.Configuration.GetSection(ApiOptions.SectionName));
builder.Services.AddOptions<AgentOptions>().Bind(builder.Configuration.GetSection(AgentOptions.SectionName));

// Persist DataProtection keys next to the agent state so the encrypted
// agent-state.json survives container recreation or a different host
// user. SetApplicationName isolates the key ring from any other
// RestoreMe component sharing the same directory.
var agentKeysDir = Path.Combine(AppContext.BaseDirectory, "state", "keys");
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
